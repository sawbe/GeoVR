using System;
using System.Collections.Generic;
using System.Text;

namespace GeoVR.Shared
{
    public class SoundcardStoppedEventArgs : EventArgs
    {
        public int SoundcardIndex { get; }
        public EventArgs StoppedEvent { get; }

        public SoundcardStoppedEventArgs(int soundcardIndex, EventArgs stoppedEvent)
        {
            SoundcardIndex = soundcardIndex;
            StoppedEvent = stoppedEvent;
        }
    }
}
