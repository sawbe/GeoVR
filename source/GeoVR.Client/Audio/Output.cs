using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.PortAudio;

namespace GeoVR.Client
{
    public class Output
    {
        private int outputIndex;
        private string outputDeviceName;
        private PaOut waveOut;

        public bool Started { get; private set; }

        public string DeviceName => outputDeviceName;
        public event EventHandler Stopped;

        /// <summary>
        /// Creates an Output. Call Start(ISampleProvider) to begin playback
        /// </summary>
        /// <param name="output">PortAudio Output Device Index</param>
        public Output(int outputIndex)
        {
            this.outputIndex = outputIndex;
            outputDeviceName = Util.GetDeviceInfo(outputIndex).name;
        }

        /// <summary>
        /// Starts audio output to the specified device
        /// </summary>
        /// <param name="audioDevice">WASAPI Render device</param>
        /// <param name="sampleProvider">Audio to play</param>
        /// <exception cref="Exception">Output already started</exception>
        public void Start(int outputIndex, ISampleProvider sampleProvider)
        {
            if (Started)
                throw new Exception("Output already started");

            this.outputIndex = outputIndex;
            outputDeviceName = Util.GetDeviceInfo(outputIndex).name;
            Start(sampleProvider);
        }

        /// <summary>
        /// Starts audio output
        /// </summary>
        /// <param name="sampleProvider"></param>
        /// <exception cref="Exception">Output already started</exception>
        public void Start(ISampleProvider sampleProvider)
        {
            if (Started)
                throw new Exception("Output already started");

            waveOut = new PaOut(outputIndex);
            waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            waveOut.Init(sampleProvider);
            waveOut.Play();

            Started = true;
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Started = false;
            Stopped?.Invoke(this, e);
        }

        /// <summary>
        /// Stops audio playback
        /// </summary>
        public void Stop()
        {
            if (!Started)
                throw new Exception("Output not started");

            Started = false;

            waveOut.Stop();
            waveOut.Dispose();
            waveOut = null;
        }
    }
}
