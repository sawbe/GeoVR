using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NAudio.PortAudio
{
    [StructLayout(LayoutKind.Sequential)]
    public struct HostApiInfo
    {
        /** this is struct version 1 */
        public int structVersion;
        /** The well known unique identifier of this host API @see PaHostApiTypeId */
        public HostApiType type;
        /** A textual description of the host API for display on user interfaces. Encoded as UTF-8. */
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;

        /**  The number of devices belonging to this host API. This field may be
         used in conjunction with Pa_HostApiDeviceIndexToDeviceIndex() to enumerate
         all devices for this host API.
         @see Pa_HostApiDeviceIndexToDeviceIndex
        */
        public int deviceCount;

        /** The default input device for this host API. The value will be a
         device index ranging from 0 to (Pa_GetDeviceCount()-1), or paNoDevice
         if no default input device is available.
        */
        public int defaultInputDevice;

        /** The default output device for this host API. The value will be a
         device index ranging from 0 to (Pa_GetDeviceCount()-1), or paNoDevice
         if no default output device is available.
        */
        public int defaultOutputDevice;
    }
}
