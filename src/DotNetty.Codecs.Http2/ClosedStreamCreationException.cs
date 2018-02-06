// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
   * Used when a stream creation attempt fails but may be because the stream was previously closed.
   */
    public sealed class ClosedStreamCreationException : Http2Exception
    {
        static readonly long serialVersionUID = -6746542974372246206L;

        public ClosedStreamCreationException(Http2Error error)
            : base(error)
        {
        }

        public ClosedStreamCreationException(Http2Error error, string message)
            : base(error, message)
        {
        }

        public ClosedStreamCreationException(Http2Error error, string message, Exception cause)
            : base(error, message, cause)
        {
        }
    }
}