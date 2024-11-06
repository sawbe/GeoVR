using System;
using System.Collections.Generic;
using System.Text;

namespace NAudio.PortAudio
{
    public enum HostApiType
    {
        InDevelopment = 0, /* use while developing support for a new host API */
        DirectSound = 1,
        MME = 2,
        ASIO = 3,
        SoundManager = 4,
        CoreAudio = 5,
        OSS = 7,
        ALSA = 8,
        AL = 9,
        BeOS = 10,
        WDMKS = 11,
        JACK = 12,
        WASAPI = 13,
        AudioScienceHPI = 14,
        AudioIO = 15,
        PulseAudio = 16,
        Sndio = 17
    }
}
