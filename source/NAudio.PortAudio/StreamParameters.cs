using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NAudio.PortAudio
{
    /// <summary>
    /// Parameters for one direction (input or output) of a stream.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StreamParameters
    {
        /// <summary>
        /// A valid device index in the range 0 to (Pa_GetDeviceCount()-1)
        /// specifying the device to be used or the special constant
        /// paUseHostApiSpecificDeviceSpecification which indicates that the actual
        /// device(s) to use are specified in hostApiSpecificStreamInfo.
        /// This field must not be set to paNoDevice.
        /// </summary>
        public int device;

        /// <summary>
        /// The number of channels of sound to be delivered to the
        /// stream callback or accessed by Pa_ReadStream() or Pa_WriteStream().
        /// It can range from 1 to the value of maxInputChannels in the
        /// PaDeviceInfo record for the device specified by the device parameter.
        /// </summary>
        public int channelCount;

        /// <summary>
        /// The sample format of the buffer provided to the stream callback,
        /// a_ReadStream() or Pa_WriteStream(). It may be any of the formats described
        /// by the PaSampleFormat enumeration.
        /// </summary>
        public SampleFormat sampleFormat;

        /// <summary>
        /// The desired latency in seconds. Where practical, implementations should
        /// configure their latency based on these parameters, otherwise they may
        /// choose the closest viable latency instead. Unless the suggested latency
        /// is greater than the absolute upper limit for the device implementations
        /// should round the suggestedLatency up to the next practical value - ie to
        /// provide an equal or higher latency than suggestedLatency wherever possible.
        /// Actual latency values for an open stream may be retrieved using the
        /// inputLatency and outputLatency fields of the PaStreamInfo structure
        /// returned by Pa_GetStreamInfo().
        /// @see default*Latency in PaDeviceInfo, *Latency in PaStreamInfo
        /// </summary>
        public double suggestedLatency;

        /// <summary>
        /// An optional pointer to a host api specific data structure
        /// containing additional information for device setup and/or stream processing.
        /// hostApiSpecificStreamInfo is never required for correct operation,
        /// if not used it should be set to NULL.
        /// </summary>
        public IntPtr hostApiSpecificStreamInfo;    // Originally `void *`

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("StreamParameters [");
            sb.AppendLine($"  device={device}");
            sb.AppendLine($"  channelCount={channelCount}");
            sb.AppendLine($"  sampleFormat={sampleFormat}");
            sb.AppendLine($"  suggestedLatency={suggestedLatency}");
            sb.AppendLine($"  hostApiSpecificStreamInfo?=[{hostApiSpecificStreamInfo != IntPtr.Zero}]");
            sb.AppendLine("]");
            return sb.ToString();
        }
    }
}
