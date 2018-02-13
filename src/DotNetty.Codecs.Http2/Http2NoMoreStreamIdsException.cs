namespace DotNetty.Codecs.Http2
{
    using System;

    /**
 * This exception is thrown when there are no more stream IDs available for the current connection
 */

    public class Http2NoMoreStreamIdsException : Http2Exception
    {
        static readonly string ERROR_MESSAGE = "No more streams can be created on this connection";

        public Http2NoMoreStreamIdsException()
            : base(Http2Error.PROTOCOL_ERROR, ERROR_MESSAGE, ShutdownHint.GRACEFUL_SHUTDOWN)
        {
        }

        public Http2NoMoreStreamIdsException(Exception cause)
            : base(Http2Error.PROTOCOL_ERROR, ERROR_MESSAGE, cause, ShutdownHint.GRACEFUL_SHUTDOWN)
        {
        }
    }
}