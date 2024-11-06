using System;
using System.Collections.Generic;
using System.Text;

namespace NAudio.PortAudio
{
    public enum WasapiStreamCategory
    {
        eAudioCategoryOther = 0,
        eAudioCategoryCommunications = 3,
        eAudioCategoryAlerts = 4,
        eAudioCategorySoundEffects = 5,
        eAudioCategoryGameEffects = 6,
        eAudioCategoryGameMedia = 7,
        eAudioCategoryGameChat = 8,
        eAudioCategorySpeech = 9,
        eAudioCategoryMovie = 10,
        eAudioCategoryMedia = 11
    }
}
