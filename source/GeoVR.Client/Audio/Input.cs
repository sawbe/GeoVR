using Concentus;
using Concentus.Enums;
using Concentus.Structs;
using NAudio.CoreAudioApi;
using NAudio.PortAudio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public class Input : IInput
    {
        private const int maxEncodedBufferSize = 1275;
        private const int frameSize = 960;
        private readonly int sampleRate;

        private int inputDevice;
        private string inputDeviceName;
        private readonly IOpusEncoder encoder;
        private PaCapture capture;
        private InputVolumeStreamEventArgs inputVolumeStreamArgs;
        private OpusDataAvailableEventArgs opusDataAvailableArgs;

        private readonly short[] recordedBuffer = new short[frameSize];
        private readonly byte[] encodedDataBuffer = new byte[maxEncodedBufferSize];
        private int encodedDataLength;

        private uint audioSequenceCounter = 0;

        private float maxSampleInput = 0;
        private float sampleInput = 0;
        private int sampleCount = 0;
        private readonly int sampleCountPerEvent = 4800;
        private readonly float maxDb = 0;
        private readonly float minDb = -40;

        public event EventHandler<OpusDataAvailableEventArgs> OpusDataAvailable;
        public event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream;

        public string DeviceName => inputDeviceName;
        public bool Started { get; private set; }
        public long OpusBytesEncoded { get; set; }
        public float Volume { get; set; } = 1;
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Creates an Input. Start must be called to begin capture
        /// </summary>
        /// <param name="audioDevice">Capture device</param>
        /// <param name="sampleRate">Audio sample rate</param>
        public Input(int audioDevice, int sampleRate)
        {
            if (audioDevice < 0)
                throw new ArgumentNullException(nameof(audioDevice));

            inputDevice = audioDevice;
            inputDeviceName = Util.GetDeviceInfo(audioDevice).name;
            this.sampleRate = sampleRate;
            encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = 16 * 1024;
            WaveFormat = new WaveFormat(sampleRate, 16, 1);
        }

        /// <summary>
        /// Starts input capture with specified device
        /// </summary>
        /// <param name="audioDevice">Capture device</param>
        /// <exception cref="Exception">Input already started</exception>
        public void Start(int audioDevice)
        {
            if (Started)
                throw new Exception("Input already started");

            inputDevice = audioDevice;
            Start();
        }

        /// <summary>
        /// Starts input capture
        /// </summary>
        /// <exception cref="Exception">Input already started</exception>
        public void Start()
        {
            if (Started)
                throw new Exception("Input already started");

            capture = new PaCapture(WaveFormat, inputDevice);
            capture.DataAvailable += Capture_DataAvailable;

            inputVolumeStreamArgs = new InputVolumeStreamEventArgs() { DeviceName = inputDeviceName, PeakRaw = 0, PeakDB = float.NegativeInfinity, PeakVU = 0 };
            opusDataAvailableArgs = new OpusDataAvailableEventArgs();

            capture.StartRecording();

            Started = true;
        }

        /// <summary>
        /// Stop input capture
        /// </summary>
        public void Stop()
        {
            if (!Started)
                throw new Exception("Input not started");

            Started = false;

            try
            {
                capture.StopRecording();
            }
            catch (PaException) { }
            finally
            {
                capture.Dispose();
                capture = null;
            }
        }

        public void AddSamples(byte[] buffer, int offfset, int count)
        {
            throw new NotImplementedException("Use SampleInput instead");
        }

        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || e.BytesRecorded < capture.FrameBufferSizeBytes)
                return;

            int newSamples = e.BytesRecorded / 2;
            int framesRecorded = newSamples / frameSize;

            for(int f = 0; f < framesRecorded; f++)
            {
                for(int n = 0; n < frameSize; n++)
                {
                    recordedBuffer[n] = BitConverter.ToInt16(e.Buffer, (f * frameSize) + (n * 2));
                }

                ProcessBuffer();
            }
        }

        private void ProcessBuffer()
        {
            Span<short> samples = new Span<short>(recordedBuffer, 0, frameSize);

            float value = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                value = samples[i] * Volume;
                if (value > short.MaxValue)
                    value = short.MaxValue;
                if (value < short.MinValue)
                    value = short.MinValue;
                samples[i] = (short)value;
            }

            encodedDataLength = encoder.Encode(samples, frameSize, encodedDataBuffer, encodedDataBuffer.Length);
            OpusBytesEncoded += encodedDataLength;

            //Calculate max and raise event if needed
            if (InputVolumeStream != null)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    sampleInput = samples[i];
                    sampleInput = Math.Abs(sampleInput);
                    if (sampleInput > maxSampleInput)
                        maxSampleInput = sampleInput;
                }
                sampleCount += samples.Length;
                if (sampleCount >= sampleCountPerEvent)
                {
                    inputVolumeStreamArgs.PeakRaw = maxSampleInput / short.MaxValue;
                    inputVolumeStreamArgs.PeakDB = (float)(20 * Math.Log10(inputVolumeStreamArgs.PeakRaw));
                    float db = inputVolumeStreamArgs.PeakDB;
                    if (db < minDb)
                        db = minDb;
                    if (db > maxDb)
                        db = maxDb;
                    float ratio = (db - minDb) / (maxDb - minDb);
                    if (ratio < 0.30)
                        ratio = 0;
                    if (ratio > 1.0)
                        ratio = 1;
                    inputVolumeStreamArgs.PeakVU = ratio;
                    InputVolumeStream(this, inputVolumeStreamArgs);
                    sampleCount = 0;
                    maxSampleInput = 0;
                }
            }

            if (OpusDataAvailable != null)
            {
                byte[] trimmedBuff = new byte[encodedDataLength];
                Buffer.BlockCopy(encodedDataBuffer, 0, trimmedBuff, 0, encodedDataLength);
                opusDataAvailableArgs.Audio = trimmedBuff;
                opusDataAvailableArgs.SequenceCounter = audioSequenceCounter++;
                OpusDataAvailable(this, opusDataAvailableArgs);
            }
        }
    }

    public class OpusDataAvailableEventArgs
    {
        public uint SequenceCounter { get; set; }
        public byte[] Audio { get; set; }
    }

    public class InputVolumeStreamEventArgs
    {
        public string DeviceName { get; set; }
        public float PeakRaw { get; set; }
        public float PeakDB { get; set; }
        public float PeakVU { get; set; }
    }
}
