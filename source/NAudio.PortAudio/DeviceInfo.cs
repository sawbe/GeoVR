using System.Runtime.InteropServices;
using System.Text;

namespace NAudio.PortAudio
{
    /// <summary>
    /// A structure providing information and capabilities of PortAudio devices.
    /// Devices may support input, output or both input and output.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfo
    {
        public int structVersion;           // this is struct version 2

        [MarshalAs(UnmanagedType.LPStr)]
        public string name;                 // Originally: `const char *`

        public int hostApi;        // note this is a host API index, not a type id

        public int maxInputChannels;
        public int maxOutputChannels;

        // Default latency values for interactive performance.
        public double defaultLowInputLatency;
        public double defaultLowOutputLatency;

        // Default latency values for robust non-interactive applications (eg. playing sound files).
        public double defaultHighInputLatency;
        public double defaultHighOutputLatency;

        public double defaultSampleRate;
    }

    public static class DeviceInfoExtensions
    {
        public static bool IsInput(this DeviceInfo info) => info.maxInputChannels > 0;
        public static bool IsOutput(this DeviceInfo info) => info.maxOutputChannels > 0;
    }
}
