using Concentus.Enums;
using Concentus.Structs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public class Input
    {
        private const int frameSize = 960;
        private readonly int sampleRate;

        private MMDevice inputDevice;
        private readonly OpusEncoder encoder;
        private ResamplingWasapiCapture wasapiCapture;
        private InputVolumeStreamEventArgs inputVolumeStreamArgs;
        private OpusDataAvailableEventArgs opusDataAvailableArgs;

        public event EventHandler<OpusDataAvailableEventArgs> OpusDataAvailable;
        public event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream;

        public string DeviceName => inputDevice?.FriendlyName;
        public bool Started { get; private set; }
        public long OpusBytesEncoded { get; set; }
        public float Volume { get; set; } = 1;

        /// <summary>
        /// Creates an Input. Start must be called to begin capture
        /// </summary>
        /// <param name="audioDevice">WASAPI Capture device</param>
        /// <param name="sampleRate">Audio sample rate</param>
        public Input(MMDevice audioDevice, int sampleRate)
        {
            if (audioDevice is null)
                throw new ArgumentNullException(nameof(audioDevice));

            inputDevice = audioDevice;
            this.sampleRate = sampleRate;
            encoder = OpusEncoder.Create(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = 16 * 1024;
        }

        /// <summary>
        /// Starts input capture with specified device
        /// </summary>
        /// <param name="audioDevice">Capture device</param>
        /// <exception cref="Exception">Input already started</exception>
        public void Start(MMDevice audioDevice)
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

            wasapiCapture = new ResamplingWasapiCapture(inputDevice, true, 20)
            {
                WaveFormat = new WaveFormat(sampleRate, 16, 1)
            };
            wasapiCapture.DataAvailable += WasapiCapture_DataAvailable;

            inputVolumeStreamArgs = new InputVolumeStreamEventArgs() { DeviceName = inputDevice.FriendlyName, PeakRaw = 0, PeakDB = float.NegativeInfinity, PeakVU = 0 };
            opusDataAvailableArgs = new OpusDataAvailableEventArgs();

            wasapiCapture.StartRecording();

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

            wasapiCapture.StopRecording();
            wasapiCapture.Dispose();
            wasapiCapture = null;
        }

        private readonly byte[] recordedBuffer = new byte[frameSize * 2];
        private readonly byte[] encodedDataBuffer = new byte[1275];
        private int encodedDataLength;
        private uint audioSequenceCounter = 0;
        private float maxSampleInput = 0;
        private float sampleInput = 0;
        private int sampleCount = 0;
        private int bufferOffset = 0;
        private readonly int sampleCountPerEvent = 4800;
        private readonly float maxDb = 0;
        private readonly float minDb = -40;
        private void WasapiCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || e.BytesRecorded < frameSize)
                return;

            int offset = 0;
            int halfFrameCount = e.BytesRecorded / frameSize;
            for (int i = 0; i < halfFrameCount; i++)
            {
                Buffer.BlockCopy(e.Buffer, offset, recordedBuffer, bufferOffset, frameSize);
                bufferOffset += frameSize;
                if (bufferOffset >= frameSize * 2)
                {
                    ProcessBuffer();
                    bufferOffset = 0;
                }
                offset += frameSize;
            }
        }

        private void ProcessBuffer()
        {
            var samples = ClientAudioUtilities.ConvertBytesTo16BitPCM(recordedBuffer);
            if (samples.Length != frameSize)
                throw new Exception("Incorrect number of samples.");

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

            encodedDataLength = encoder.Encode(samples, 0, frameSize, encodedDataBuffer, 0, encodedDataBuffer.Length);
            OpusBytesEncoded += encodedDataLength;

            //Calculate max and raise event if needed
            if (InputVolumeStream != null)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    sampleInput = samples[i];
                    sampleInput = System.Math.Abs(sampleInput);
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
