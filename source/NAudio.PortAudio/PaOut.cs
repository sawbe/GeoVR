using NAudio.Wave;
using System.Runtime.InteropServices;
using static NAudio.PortAudio.Native;

namespace NAudio.PortAudio
{
    public class PaOut : IWavePlayer, IWavePosition
    {
        private const int framesPerBuffer = 64;

        private IWaveProvider waveProvider;
        private int deviceIndex = -1;
        private double latency = 0.2;
        private IntPtr stream;
        private StreamParameters parameters;
        private Native.StreamCallback streamCallback;
        private Native.StreamFinishedCallback streamFinishedCallback;
        private int bytesPerFrame;
        private byte[] buffer;

        public float Volume { get => 1f; set => throw new NotImplementedException(); }

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public WaveFormat OutputWaveFormat => waveProvider.WaveFormat;

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public PaOut()
        {
            streamCallback = new Native.StreamCallback(OutputCallback);
            streamFinishedCallback = new Native.StreamFinishedCallback(OutputStoppedCallback);
            latency = Util.GetDeviceInfo(deviceIndex).defaultLowOutputLatency;
        }
        public PaOut(int deviceIndex)
        {
            this.deviceIndex = deviceIndex;
            streamCallback = new Native.StreamCallback(OutputCallback);
            streamFinishedCallback = new Native.StreamFinishedCallback(OutputStoppedCallback);
            latency = Util.GetDeviceInfo(deviceIndex).defaultLowOutputLatency;
        }

        public void Dispose()
        {
            if (PlaybackState != PlaybackState.Stopped)
                Stop();
        }

        public long GetPosition()
        {
            if (PlaybackState == PlaybackState.Stopped)
                return 0;

            var time = Native.Pa_GetStreamTime(stream);

            return (long)(time * OutputWaveFormat.AverageBytesPerSecond);
        }

        public void Init(IWaveProvider waveProvider)
        {
            var err = Native.Pa_Initialize();
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            if (deviceIndex < 0)
                deviceIndex = Util.DefaultOutputDevice;

            var devInfo = Util.GetDeviceInfo(deviceIndex);

            this.waveProvider = waveProvider;

            var sampleFormat = SampleFormat.Float32;
            if (OutputWaveFormat.Encoding == WaveFormatEncoding.Pcm)
            {
                switch (OutputWaveFormat.BitsPerSample)
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
            else if (OutputWaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new Exception("Unsupported format");

            bool usingWasapi = Util.GetHostInfo(devInfo.hostApi).type == HostApiType.WASAPI;

            parameters = new StreamParameters()
            {
                channelCount = OutputWaveFormat.Channels > devInfo.maxOutputChannels ? devInfo.maxOutputChannels : OutputWaveFormat.Channels,
                device = deviceIndex,
                hostApiSpecificStreamInfo = usingWasapi ? Util.WasapiInfo : IntPtr.Zero,
                sampleFormat = sampleFormat,
                suggestedLatency = latency,
            };

            bytesPerFrame = OutputWaveFormat.Channels * OutputWaveFormat.BitsPerSample / 8;
            buffer = new byte[framesPerBuffer * bytesPerFrame];

            err = Native.Pa_OpenStream(out stream, IntPtr.Zero, ref parameters, OutputWaveFormat.SampleRate, framesPerBuffer, StreamFlags.NoFlag, streamCallback, IntPtr.Zero);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            err = Native.Pa_SetStreamFinishedCallback(stream, streamFinishedCallback);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);
        }

        public void Pause()
        {
            if (stream == IntPtr.Zero)
                throw new Exception("Call init first");

            var err = Native.Pa_StopStream(stream);
            if (err != ErrorCode.NoError)
                throw new PaException(err);

            PlaybackState = PlaybackState.Paused;
        }

        public void Play()
        {
            if (stream == IntPtr.Zero)
                throw new Exception("Call init first");

            var err = Native.Pa_StartStream(stream);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            PlaybackState = PlaybackState.Playing;  
        }

        public void Stop()
        {
            Pause();
            var err = Native.Pa_CloseStream(stream);
            if (err != ErrorCode.NoError)
                Util.ThrowException(err);

            err = Native.Pa_Terminate();

            PlaybackState = PlaybackState.Stopped;
        }

        private void OutputStoppedCallback(IntPtr userData)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }

        private StreamCallbackResult OutputCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
        {
            int requiredLen = (int)frameCount * bytesPerFrame;
            int len = waveProvider.Read(buffer, 0, requiredLen);

            if (len == 0)
                return StreamCallbackResult.Complete;

            while (len < requiredLen)
                buffer[len++] = 0;

            Marshal.Copy(buffer, 0, output, requiredLen);

            return StreamCallbackResult.Continue;
        }
    }
}