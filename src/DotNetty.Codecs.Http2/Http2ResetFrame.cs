// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /** HTTP/2 RST_STREAM frame. */
    public interface Http2ResetFrame : Http2StreamFrame
    {
        /**
         * The reason for resetting the stream. Represented as an HTTP/2 error code.
         */
        long errorCode();
    }
}