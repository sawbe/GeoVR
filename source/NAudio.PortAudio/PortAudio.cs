using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NAudio.PortAudio
{
    internal static partial class Native
    {
        public const string PortAudioDLL = "portaudio";

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetVersion();

        [DllImport(PortAudioDLL)]
        public static extern IntPtr Pa_GetVersionInfo();           // Originally returns `const PaVersionInfo *`

        [DllImport(PortAudioDLL)]
        public static extern IntPtr Pa_GetErrorText([MarshalAs(UnmanagedType.I4)] ErrorCode errorCode);     // Orignially returns `const char *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_Initialize();

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_Terminate();

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetDefaultHostApi();

        [DllImport(PortAudioDLL)]
        public static extern IntPtr Pa_GetHostApiInfo(int index); //Returns PaHostApiInfo *

        [DllImport(PortAudioDLL)]
        public static extern int Pa_HostApiTypeIdToHostApiIndex(HostApiType hostApiType);

        [DllImport(PortAudioDLL)]
        public static extern int Pa_HostApiDeviceIndexToDeviceIndex(int hostApiIndex, int hostApiDeviceIndex);

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetHostApiCount();

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetDefaultOutputDevice();

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetDefaultInputDevice();

        [DllImport(PortAudioDLL)]
        public static extern IntPtr Pa_GetDeviceInfo(int device);   // Originally returns `const PaDeviceInfo *`

        [DllImport(PortAudioDLL)]
        public static extern int Pa_GetDeviceCount();

        [DllImport(PortAudioDLL)]
        public static extern void Pa_Sleep(System.Int32 msec);

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_OpenStream(
            out IntPtr stream,                          // `PaStream **`
            ref StreamParameters inputParameters,                     // `const PaStreamParameters *`
            ref StreamParameters outputParameters,                    // `const PaStreamParameters *`
            double sampleRate,
            uint framesPerBuffer,
            StreamFlags streamFlags,
            StreamCallback streamCallback,                      // `PaStreamCallback *`
            IntPtr userData                             // `void *`
        );

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_OpenStream(
            out IntPtr stream,                          // `PaStream **`
            IntPtr inputParameters,                     // `const PaStreamParameters *`
            ref StreamParameters outputParameters,                    // `const PaStreamParameters *`
            double sampleRate,
            uint framesPerBuffer,
            StreamFlags streamFlags,
            StreamCallback streamCallback,                      // `PaStreamCallback *`
            IntPtr userData                             // `void *`
        );

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_OpenStream(
            out IntPtr stream,                          // `PaStream **`
            ref StreamParameters inputParameters,                     // `const PaStreamParameters *`
            IntPtr outputParameters,                    // `const PaStreamParameters *`
            double sampleRate,
            uint framesPerBuffer,
            StreamFlags streamFlags,
            StreamCallback streamCallback,                      // `PaStreamCallback *`
            IntPtr userData                             // `void *`
        );

        [DllImport(PortAudioDLL)]
        public static extern ErrorCode Pa_IsFormatSupported(
            ref StreamParameters inputParameters,
            ref StreamParameters outputParameters,
            double sampleRate);

        [DllImport(PortAudioDLL)]
        public static extern ErrorCode Pa_IsFormatSupported(
            ref StreamParameters inputParameters,
            IntPtr outputParameters,
            double sampleRate);

        [DllImport(PortAudioDLL)]
        public static extern ErrorCode Pa_IsFormatSupported(
            IntPtr inputParameters,
            ref StreamParameters outputParameters,
            double sampleRate);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I4)]
        public delegate StreamCallbackResult StreamCallback(
            IntPtr input, IntPtr output,                // Originally `const void *, void *`
            uint frameCount,
            ref StreamCallbackTimeInfo timeInfo,        // Originally `const PaStreamCallbackTimeInfo*`
            StreamCallbackFlags statusFlags,
            IntPtr userData                             // Orignially `void *`
        );

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_CloseStream(IntPtr stream);       // `PaStream *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_SetStreamFinishedCallback(
            IntPtr stream,                                                  // `PaStream *`
            StreamFinishedCallback streamFinishedCallback                                   // `PaStreamFinishedCallback *`
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void StreamFinishedCallback(
            IntPtr userData                         // Originally `void *`
        );

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_StartStream(IntPtr stream);       // `PaStream *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_StopStream(IntPtr stream);        // `PaStream *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_AbortStream(IntPtr stream);       // `PaStream *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_IsStreamStopped(IntPtr stream);   // `PaStream *`

        [DllImport(PortAudioDLL)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern ErrorCode Pa_IsStreamActive(IntPtr stream);    // `PaStream *`

        [DllImport(PortAudioDLL)]
        public static extern double Pa_GetStreamTime(IntPtr stream);        // `PaStream *`

        [DllImport(PortAudioDLL)]
        public static extern double Pa_GetStreamCpuLoad(IntPtr stream);     // `PaStream *`

    }

    public static class Util
    {
        private static readonly object _lock = new object();
        private static bool initialised = false;
        private static IntPtr wasapiInfo = IntPtr.Zero;
        private const uint WinWasapiAutoConvert = (1 << 6);
        public static bool IsPortAudioInitialised => initialised;

        internal static void ThrowException(ErrorCode error)
        {
            Terminate();
            throw new PaException(error, Marshal.PtrToStringAnsi(Native.Pa_GetErrorText(error)) ?? "Unknown");
        }

        internal static IntPtr WasapiInfo
        {
            get
            {
                if(wasapiInfo == IntPtr.Zero)
                {
                    var wasapiFlags = new WasapiStreamInfo();
                    wasapiFlags.hostApiType = HostApiType.WASAPI;
                    wasapiFlags.version = 1;
                    wasapiFlags.flags = WinWasapiAutoConvert;
                    wasapiFlags.threadPriority = WasapiThreadPriority.eThreadPriorityAudio;
                    wasapiFlags.hostProcessInput = IntPtr.Zero;
                    wasapiFlags.hostProcessOutput = IntPtr.Zero;
                    wasapiFlags.streamCategory = WasapiStreamCategory.eAudioCategoryGameChat;
                    wasapiFlags.streamOption = WasapiStreamOption.eStreamOptionNone;
                    wasapiFlags.size = Marshal.SizeOf(wasapiFlags);

                    wasapiInfo = Marshal.AllocHGlobal(wasapiFlags.size);
                    Marshal.StructureToPtr(wasapiFlags, wasapiInfo, false);
                }

                return wasapiInfo;
            }
        }

        private static bool InitIfRequired()
        {
            lock (_lock)
            {
                if (initialised)
                    return false;

                var err = Native.Pa_Initialize();
                initialised = err == ErrorCode.NoError;
                return initialised;
            }
        }

        private static void Terminate()
        {
            lock (_lock)
            {
                if (!initialised)
                    return;

                Native.Pa_Terminate();
                initialised = false;
            }
        }

        /// <summary>
        /// Retrieve the index of the default output device. The result can be
        /// used in the outputDevice parameter to Pa_OpenStream().
        ///
        /// @return The default output device index for the default host API, or paNoDevice
        /// if no default output device is available or an error was encountered.
        ///
        /// @note
        /// On the PC, the user can specify a default device by
        /// setting an environment variable. For example, to use device #1.
        /// <pre>
        /// set PA_RECOMMENDED_OUTPUT_DEVICE=1
        /// </pre>
        /// The user should first determine the available device ids by using
        /// the supplied application "pa_devs".
        /// </summary>
        public static int DefaultOutputDevice
        {
            get 
            {
                var terminate = InitIfRequired(); 
                var dev = Native.Pa_GetDefaultOutputDevice();
                if (terminate)
                    Terminate();
                return dev;
            }
        }

        /// <summary>
        /// Retrieve the index of the default input device. The result can be
        /// used in the inputDevice parameter to Pa_OpenStream().
        ///
        /// @return The default input device index for the default host API, or paNoDevice
        /// if no default input device is available or an error was encountered.
        /// </summary>
        public static int DefaultInputDevice
        {
            get
            {
                var terminate = InitIfRequired();
                var dev = Native.Pa_GetDefaultInputDevice();
                if (terminate)
                    Terminate();
                return dev;
            }
        }

        /// <summary>
        /// Retrieve a pointer to a PaDeviceInfo structure containing information
        /// about the specified device.
        /// @return A pointer to an immutable PaDeviceInfo structure. If the device
        /// parameter is out of range the function returns NULL.
        ///
        /// @param device A valid device index in the range 0 to (Pa_GetDeviceCount()-1)
        ///
        /// @note PortAudio manages the memory referenced by the returned pointer,
        /// the client must not manipulate or free the memory. The pointer is only
        /// guaranteed to be valid between calls to Pa_Initialize() and Pa_Terminate().
        ///
        /// @see PaDeviceInfo, PaDeviceIndex
        /// </summary>
        public static DeviceInfo GetDeviceInfo(int device)
        {
            var terminate = InitIfRequired();
            var devptr = Native.Pa_GetDeviceInfo(device);
            var deviceinfo = Marshal.PtrToStructure<DeviceInfo>(devptr);
            if (terminate)
                Terminate();
            return deviceinfo;
        }

        /// <summary>
        /// Retrieve the number of available devices. The number of available devices
        /// may be zero.
        ///
        /// @return A non-negative value indicating the number of available devices
        /// or, a PaErrorCode (which are always negative) if PortAudio is not initialized
        /// or an error is encountered.
        /// </summary>
        public static int DeviceCount
        {
            get
            {
                var terminate = InitIfRequired();
                var dev = Native.Pa_GetDeviceCount();
                if (terminate)
                    Terminate();
                return dev;
            }
        }

        public static int HostApiCount
        {
            get
            {
                var terminate = InitIfRequired();
                var dev = Native.Pa_GetHostApiCount();
                if (terminate)
                    Terminate();
                return dev;
            }
        }

        public static IList<DeviceInfo> GetAllDevices()
        {
            var terminate = InitIfRequired();
            var devCount = DeviceCount;
            if (devCount <= 0)
                return Array.Empty<DeviceInfo>();

            DeviceInfo[] devs = new DeviceInfo[devCount];
            for (int i = 0; i < devCount; i++)
            {
                devs[i] = GetDeviceInfo(i);
            }
            if (terminate)
                Terminate();
            return devs;
        }

        public static IList<HostApiType> GetAvailableHostApis()
        {
            var terminate = InitIfRequired();
            var hostCount = HostApiCount;
            if (hostCount <= 0)
                return Array.Empty<HostApiType>();

            HostApiType[] hostApis = new HostApiType[hostCount];
            for(int i = 0; i< hostCount; i++)
            {
                hostApis[i] = GetHostInfo(i).type;
            }

            if (terminate)
                Terminate();
            return hostApis;
        }

        public static HostApiType DefaultHostApiType
        {
            get
            {
                var terminate = InitIfRequired();
                var hostIndex = Native.Pa_GetDefaultHostApi();
                if (hostIndex < 0)
                    throw new Exception("No Host API's or not Initialized.");

                var host = GetHostInfo(hostIndex);
                if (terminate)
                    Terminate();

                return host.type;
            }
        }

        public static HostApiInfo GetHostInfo(HostApiType type)
        {
            var terminate = InitIfRequired();
            var hostIndex = Native.Pa_HostApiTypeIdToHostApiIndex(type);
            if (hostIndex < 0)
                return default(HostApiInfo);

            var host = GetHostInfo(hostIndex);
            if (terminate)
                    Terminate();

            return host;
        }

        public static HostApiInfo GetHostInfo(int hostIndex)
        {
            var terminate = InitIfRequired();
            var hostInfo = Marshal.PtrToStructure<HostApiInfo>(Native.Pa_GetHostApiInfo(hostIndex));
            if (terminate)
                Terminate();
            return hostInfo;
        }

        public static IList<DeviceInfo> GetHostDevices(HostApiType type)
        {
            var terminate = InitIfRequired();
            var hostIndex = Native.Pa_HostApiTypeIdToHostApiIndex(type);
            if(hostIndex < 0)
                return Array.Empty<DeviceInfo>();

            var hostInfo = GetHostInfo(type);
            if (hostInfo.deviceCount <= 0)
                return Array.Empty<DeviceInfo>();

            DeviceInfo[] devs = new DeviceInfo[hostInfo.deviceCount];
            for(int i = 0; i < hostInfo.deviceCount; i++)
            {
                int devIndex = Native.Pa_HostApiDeviceIndexToDeviceIndex(hostIndex, i);
                devs[i] = GetDeviceInfo(devIndex);
            }

            if (terminate)
                Terminate();

            return devs;
        }

        public static int MapInputDevice(HostApiType type, string deviceName)
        {
            var terminate = InitIfRequired();
            var hostIndex = Native.Pa_HostApiTypeIdToHostApiIndex(type);
            var hostDeviceIndex = GetHostDevices(type).ToList().FindIndex(d => d.name == deviceName && d.IsInput());
            var dev = Native.Pa_HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
            if (terminate)
                Terminate();

            return dev;
        }

        public static int MapOutputDevice(HostApiType type, string deviceName)
        {
            var terminate = InitIfRequired();
            var hostIndex = Native.Pa_HostApiTypeIdToHostApiIndex(type);
            var hostDeviceIndex = GetHostDevices(type).ToList().FindIndex(d => d.name == deviceName && d.IsOutput());
            var dev = Native.Pa_HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
            if (terminate)
                Terminate();

            return dev;
        }

        public static int MapDevice(HostApiType type, int hostDeviceIndex)
        {
            var terminate = InitIfRequired();
            var hostIndex = Native.Pa_HostApiTypeIdToHostApiIndex(type);
            var dev = Native.Pa_HostApiDeviceIndexToDeviceIndex(hostIndex, hostDeviceIndex);
            if (terminate)
                Terminate();

            return dev;
        }

        public static IList<DeviceInfo> GetHostInputDevices(HostApiType type) => GetHostDevices(type).Where(d=>d.IsInput()).ToArray();
        public static IList<string> GetHostInputDeviceNames(HostApiType type) => GetHostInputDevices(type).Select(d => d.name).ToArray();

        public static IList<DeviceInfo> GetHostOutputDevices(HostApiType type) => GetHostDevices(type).Where(d => d.IsOutput()).ToArray();
        public static IList<string> GetHostOutputDeviceNames(HostApiType type) => GetHostOutputDevices(type).Select(d => d.name).ToArray();
    }
}
