// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
     * Represents an exception that can be isolated to a single stream (as opposed to the entire connection).
     */
    public class StreamException : Http2Exception
    {
        static readonly long serialVersionUID = 602472544416984384L;
        readonly int _streamId;

        public StreamException(int streamId, Http2Error error, string message)
            : base(error, message, ShutdownHint.NO_SHUTDOWN)
        {
            this._streamId = streamId;
        }

        public StreamException(int streamId, Http2Error error, string message, Exception cause)
            : base(error, message, cause, ShutdownHint.NO_SHUTDOWN)
        {
            this._streamId = streamId;
        }

        public int streamId()
        {
            return this._streamId;
        }
    }
}