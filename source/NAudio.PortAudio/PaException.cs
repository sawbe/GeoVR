using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NAudio.PortAudio
{
    public class PaException : Exception
    {
        /// <summary>
        /// Error code (from the native PortAudio library).  Use `PortAudio.GetErrorText()` for some more details.
        /// </summary>
        public ErrorCode ErrorCode { get; private set; }

        /// <summary>
        /// Creates a new PortAudio error.
        /// </summary>
        public PaException(ErrorCode ec) : base()
        {
            this.ErrorCode = ec;
        }

        /// <summary>
        /// Creates a new PortAudio error with a message attached.
        /// </summary>
        /// <param name="message">Message to send</param>
        public PaException(ErrorCode ec, string message)
            : base(message)
        {
            this.ErrorCode = ec;
        }

        /// <summary>
        /// Creates a new PortAudio error with a message attached and an inner error.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="inner">The exception that occured inside of this one</param>
        public PaException(ErrorCode ec, string message, Exception inner)
            : base(message, inner)
        {
            this.ErrorCode = ec;
        }
    }
}
