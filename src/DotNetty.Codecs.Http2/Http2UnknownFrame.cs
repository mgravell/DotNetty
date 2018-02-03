// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    public interface Http2UnknownFrame : Http2Frame, IByteBufferHolder
    {
        Http2FrameStream stream();

        /**
         * Set the {@link Http2FrameStream} object for this frame.
         */
        Http2UnknownFrame stream(Http2FrameStream stream);

        byte frameType();

        Http2Flags flags();

    }
}