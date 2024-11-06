using NAudio.CoreAudioApi;
using NAudio.PortAudio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{

    public static class ClientAudioUtilities
    {
        private static HostApiType GetBestHostType(bool input)
        {
            var api = Util.DefaultHostApiType;
            var testApi = api;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                testApi = HostApiType.WASAPI;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                testApi = HostApiType.ALSA;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                testApi = HostApiType.CoreAudio;

            if (input)
            {
                if (Util.GetHostInputDevices(testApi).Count > 0)
                    api = testApi;
            }
            else
            {
                if (Util.GetHostOutputDevices(HostApiType.WASAPI).Count > 0)
                    api = testApi;
            }
            return api;
        }
        /// <summary>
        /// Get ordered list of available device names for:
        /// 1. Windows - WASAPI
        /// 2. MACOSX  - CoreAudio
        /// 3. Linux   - ALSA
        /// Or the system default if no devices for the above.
        /// </summary>
        /// <returns>List of device names</returns>
        public static IList<string> GetInputDevices()
        {
            return Util.GetHostInputDeviceNames(GetBestHostType(true));
        }
        /// <summary>
        /// Get ordered list of available device names for a specific
        /// audio Host API eg. WASAPI, JACK
        /// </summary>
        /// <param name="type">Audio Host API Type</param>
        /// <returns>List of device names</returns>
        public static IList<string> GetInputDevices(HostApiType type)
        {
            return Util.GetHostInputDeviceNames(type);
        }
        public static int? MapInputDevice(string name)
        {
            var i = Util.MapInputDevice(GetBestHostType(true), name);
            if (i < 0)
                return null;
            return i;
        }
        public static int? MapOutputDevice(string name)
        {
            var i = Util.MapOutputDevice(GetBestHostType(false), name);
            if (i < 0)
                return null;
            return i;
        }
        public static int? MapInputDevice(HostApiType type, string name)
        {
            var i = Util.MapInputDevice(type, name);
            if (i < 0)
                return null;
            return i;
        }
        public static int? MapOutputDevice(HostApiType type, string name)
        {
            var i = Util.MapOutputDevice(type, name);
            if (i < 0)
                return null;
            return i;
        }
        /// <summary>
        /// Get ordered list of available device names for:
        /// 1. Windows - WASAPI
        /// 2. MACOSX  - CoreAudio
        /// 3. Linux   - ALSA
        /// Or the system default if no devices for the above.
        /// </summary>
        /// <returns>List of device names</returns>
        public static IList<string> GetOutputDevices()
        {
            return Util.GetHostOutputDeviceNames(GetBestHostType(true));
        }
        /// <summary>
        /// Get ordered list of available device names for a specific
        /// audio Host API eg. WASAPI, JACK
        /// </summary>
        /// <param name="type">Audio Host API Type</param>
        /// <returns>List of device names</returns>
        public static IList<string> GetOutputDevices(HostApiType type)
        {
            return Util.GetHostOutputDeviceNames(type);
        }

        public static short[] ConvertBytesTo16BitPCM(byte[] input, int length)
        {
            int inputSamples = length / 2; // 16 bit input, so 2 bytes per sample
            short[] output = new short[inputSamples];
            int outputIndex = 0;
            for (int n = 0; n < inputSamples; n++)
            {
                output[outputIndex++] = BitConverter.ToInt16(input, n * 2);
            }
            return output;
        }
    }
}
