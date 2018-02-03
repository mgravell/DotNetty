// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    public interface Http2DataFrame : Http2StreamFrame, IByteBufferHolder
    {
        /**
           * Frame padding to use. Will be non-negative and less than 256.
           */
        int padding();

        /**
           * Returns the number of bytes that are flow-controlled initialy, so even if the {@link #content()} is consumed
           * this will not change.
           */
        int initialFlowControlledBytes();

        /**
           * Returns {@code true} if the END_STREAM flag ist set.
           */
        bool isEndStream();
    }
}