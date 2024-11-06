using NAudio.Wave;
using System;

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
        WaveFormat WaveFormat { get; }

        void Start();
        void Start(int device);
        void Stop();
        void AddSamples(byte[] buffer, int offset, int count);
    }
}
