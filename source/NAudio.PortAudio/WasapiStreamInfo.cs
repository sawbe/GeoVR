using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NAudio.PortAudio
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WasapiStreamInfo
    {
        public int size;
        public HostApiType hostApiType;
        public uint version;
        public uint flags;
        public uint channelMask;
        public IntPtr hostProcessOutput;
        public IntPtr hostProcessInput;
        public WasapiThreadPriority threadPriority;
        public WasapiStreamCategory streamCategory;
        public WasapiStreamOption streamOption;
    }
}
