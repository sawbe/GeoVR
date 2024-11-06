using System;
using System.Collections.Generic;
using System.Text;

namespace NAudio.PortAudio
{
    public enum WasapiThreadPriority
    {
        eThreadPriorityNone = 0,
        eThreadPriorityAudio,            //!< Default for Shared mode.
        eThreadPriorityCapture,
        eThreadPriorityDistribution,
        eThreadPriorityGames,
        eThreadPriorityPlayback,
        eThreadPriorityProAudio,        //!< Default for Exclusive mode.
        eThreadPriorityWindowManager
    }
}
