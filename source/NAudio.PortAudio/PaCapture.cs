using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NAudio.PortAudio
{
    public class PaCapture : IWaveIn
    {
        private const int opusFrameSize = 960;
        private int deviceIndex = -1;
        private IntPtr stream;
        private bool streamStarted = false;
        private double latency = 0.02;
        private uint framesPerBuffer = 960;
        private int bytesPerFrame = 2;
        private StreamParameters parameters;
        private readonly Native.StreamCallback streamCallback;
        private readonly Native.StreamFinishedCallback streamFinishedCallback;
        private byte[] buffer = new byte[1920];

        public WaveFormat WaveFormat { get; set; }
        public int FrameBufferSizeBytes => (int)framesPerBuffer * (WaveFormat.BitsPerSample / 8);
        public int FrameBufferSize => (int)framesPerBuffer;

        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;

        public PaCapture(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
            streamCallback = new Native.StreamCallback(InputCallback);
            streamFinishedCallback = new Native.StreamFinishedCallback(InputStoppedCallback);
            Init();
        }
        public PaCapture(WaveFormat waveFormat, int deviceIndex)
        {
            WaveFormat = waveFormat;
            this.deviceIndex = deviceIndex;
            streamCallback = new Native.StreamCallback(InputCallback);
            streamFinishedCallback = new Native.StreamFinishedCallback(InputStoppedCallback);
            Init();
        }

        public void Dispose()
        {
            try
            {
                StopRecording();
            }
            catch { }

            if (stream != IntPtr.Zero)
            {
                var err = Native.Pa_CloseStream(stream);
                if (err != ErrorCode.NoError)
                    throw new PaException(err);
                stream = IntPtr.Zero;
            }
            Native.Pa_Terminate();
        }

        public void StartRecording()
        {
            if (stream == IntPtr.Zero)
                throw new Exception("Call init first");

            var err = Native.Pa_StartStream(stream);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            streamStarted = true;
        }

        public void StopRecording()
        {
            if (stream == IntPtr.Zero || !streamStarted)
                return;

            streamStarted = false;

            var err = Native.Pa_StopStream(stream);
            if (err == ErrorCode.TimedOut)//abort
                err = Native.Pa_AbortStream(stream);

            if (err != ErrorCode.NoError)
                throw new PaException(err);

            RecordingStopped?.Invoke(this, new StoppedEventArgs());
        }

        private void Init()
        {
            var err = Native.Pa_Initialize();
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            if (deviceIndex < 0)
                deviceIndex = Util.DefaultInputDevice;

            var devInfo = Util.GetDeviceInfo(deviceIndex);

            var sampleFormat = SampleFormat.Float32;
            if (WaveFormat.Encoding == WaveFormatEncoding.Pcm)
            {
                switch (WaveFormat.BitsPerSample)
                {
                    case 8:
                        sampleFormat = SampleFormat.Int8;
                        break;
                    case 16:
                        sampleFormat = SampleFormat.Int16;
                        break;
                    case 24:
                        sampleFormat = SampleFormat.Int24;
                        break;
                    case 32:
                        sampleFormat = SampleFormat.Int32;
                        break;
                    default:
                        throw new Exception("Unsupported format");
                }
            }
            else if (WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new Exception("Unsupported format");

            bool usingWasapi = Util.GetHostInfo(devInfo.hostApi).type == HostApiType.WASAPI;

            if(latency < devInfo.defaultLowInputLatency)
                latency = devInfo.defaultLowInputLatency;

            framesPerBuffer = GetOpusCompatibleFrameBufferSize(WaveFormat.SampleRate * latency);
            latency = framesPerBuffer / (double)WaveFormat.SampleRate;
            bytesPerFrame = WaveFormat.Channels * WaveFormat.BitsPerSample / 8;

            File.WriteAllText("debugSaw.txt", $"FrameSize: {framesPerBuffer}\nLatency: {latency}\nSample Rate: {WaveFormat.SampleRate}");

            parameters = new StreamParameters()
            {
                channelCount = WaveFormat.Channels > devInfo.maxInputChannels ? devInfo.maxInputChannels : WaveFormat.Channels,
                device = deviceIndex,
                hostApiSpecificStreamInfo = usingWasapi ? Util.WasapiInfo : IntPtr.Zero,
                sampleFormat = sampleFormat,
                suggestedLatency = latency,
            };

            err = Native.Pa_OpenStream(out stream, ref parameters, IntPtr.Zero, WaveFormat.SampleRate, framesPerBuffer, StreamFlags.NoFlag, streamCallback, IntPtr.Zero);
            if (err == ErrorCode.InvalidChannelCount)
                throw new Exception($"Invalid channels.. max {devInfo.maxInputChannels} requested {parameters.channelCount}");
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            err = Native.Pa_SetStreamFinishedCallback(stream, streamFinishedCallback);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);
        }

        private uint GetOpusCompatibleFrameBufferSize(double suggestedSize)
        {
            if (suggestedSize <= opusFrameSize)
                return opusFrameSize;
            else
                return (uint)Math.Ceiling(suggestedSize / opusFrameSize) * opusFrameSize;
        }

        private void InputStoppedCallback(IntPtr userData)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs());
        }

        private StreamCallbackResult InputCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
        {
            if (frameCount == 0)
                return StreamCallbackResult.Continue;

            int bytes = (int)frameCount * bytesPerFrame;

            if (buffer.Length < bytes)
                buffer = new byte[bytes];

            Marshal.Copy(input, buffer, 0, bytes);

            DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, bytes));

            return StreamCallbackResult.Continue;
        }
    }
}
