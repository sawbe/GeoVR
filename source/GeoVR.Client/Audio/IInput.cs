using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public interface IInput
    {
        event EventHandler<OpusDataAvailableEventArgs> OpusDataAvailable;
        event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream;

        string DeviceName { get; }
        bool Started { get; }
        long OpusBytesEncoded { get; set; }
        float Volume { get; set; }

        void Start();
        void Start(MMDevice device);
        void Stop();
        void AddSamples(byte[] buffer, int offset, int count);
    }
}
