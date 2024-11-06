using Concentus;
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
    public class SampleInput : IInput
    {
        private const int frameSize = 960;
        private readonly int sampleRate;
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

        private readonly IOpusEncoder encoder;
        private InputVolumeStreamEventArgs inputVolumeStreamArgs;
        private OpusDataAvailableEventArgs opusDataAvailableArgs;

        public event EventHandler<OpusDataAvailableEventArgs> OpusDataAvailable;
        public event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream;

        public string DeviceName => "SampleInput";
        public bool Started { get; private set; }
        public long OpusBytesEncoded { get; set; }
        public float Volume { get; set; } = 1;
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Creates an Input. Start must be called to begin capture
        /// </summary>
        /// <param name="sampleRate">Audio sample rate</param>
        public SampleInput(int sampleRate)
        {
            this.sampleRate = sampleRate;
            encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = 16 * 1024;
            inputVolumeStreamArgs = new InputVolumeStreamEventArgs() { DeviceName = "SampleInput", PeakRaw = 0, PeakDB = float.NegativeInfinity, PeakVU = 0 };
            opusDataAvailableArgs = new OpusDataAvailableEventArgs();
            WaveFormat = new WaveFormat(sampleRate, 16, 1);
        }

        public void AddSamples(byte[] buffer, int offset, int count)
        {
            int length = count - offset;
            if (length <= 0 || length < frameSize)
                return;

            int halfFrameoffset = 0;
            int halfFrameCount = length / frameSize;
            for (int i = 0; i < halfFrameCount; i++)
            {
                Buffer.BlockCopy(buffer, halfFrameoffset, recordedBuffer, bufferOffset, frameSize);
                bufferOffset += frameSize;
                if (bufferOffset >= frameSize * 2)
                {
                    ProcessBuffer();
                    bufferOffset = 0;
                }
                halfFrameoffset += frameSize;
            }
        }

        private void ProcessBuffer()
        {
            var samples = ClientAudioUtilities.ConvertBytesTo16BitPCM(recordedBuffer, recordedBuffer.Length);
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

            encodedDataLength = encoder.Encode(samples, frameSize, encodedDataBuffer, encodedDataBuffer.Length);
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

        public void Start()
        {
            Started = true;
        }

        public void Start(int device)
        {
            Start();
        }

        public void Stop()
        {
            Started = false;
        }
    }
}
