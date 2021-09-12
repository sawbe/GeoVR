using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public class Output
    {
        private MMDevice outputDevice;
        private WasapiOut waveOut;

        public bool Started { get; private set; }

        public string DeviceName => outputDevice.FriendlyName;

        /// <summary>
        /// Creates an Output. Call Start(ISampleProvider) to begin playback
        /// </summary>
        /// <param name="output">WASAPI Render device</param>
        public Output(MMDevice output)
        {
            outputDevice = output;
        }

        /// <summary>
        /// Starts audio output to the specified device
        /// </summary>
        /// <param name="audioDevice">WASAPI Render device</param>
        /// <param name="sampleProvider">Audio to play</param>
        /// <exception cref="Exception">Output already started</exception>
        public void Start(MMDevice audioDevice, ISampleProvider sampleProvider)
        {
            if (Started)
                throw new Exception("Output already started");

            outputDevice = audioDevice;
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

            waveOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, 20);
            waveOut.Init(sampleProvider);
            waveOut.Play();

            Started = true;
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
