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
        //private WaveOutEvent waveOut;
        private WasapiOut waveOut;

        public bool Started { get; private set; }

        public void Start(MMDevice outputDevice, ISampleProvider sampleProvider)
        {
            if (Started)
                throw new Exception("Output already started");

            waveOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, 20);
            waveOut.Init(sampleProvider);
            waveOut.Play();

            Started = true;
        }

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
