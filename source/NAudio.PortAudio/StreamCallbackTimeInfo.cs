using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NAudio.PortAudio
{
    /// <summary>
    /// Timing information for the buffers passed to the stream callback.
    ///
    /// Time values are expressed in seconds and are synchronised with the time base used by Pa_GetStreamTime() for the associated stream.
    ///
    /// @see PaStreamCallback, Pa_GetStreamTime
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StreamCallbackTimeInfo
    {
        /// <summary>
        /// The time when the first sample of the input buffer was captured at the ADC input
        /// </summary>
        public double inputBufferAdcTime;

        /// <summary>
        /// The time when the stream callback was invoked
        /// </summary>
        public double currentTime;

        /// <summary>
        ///  The time when the first sample of the output buffer will output the DAC
        /// </summary>
        public double outputBufferDacTime;
    }
}
