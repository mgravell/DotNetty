// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace DotNetty.Codecs.Http2
{
    public interface Http2FrameReader : IDisposable
    {
        /**
     * Attempts to read the next frame from the input buffer. If enough data is available to fully
     * read the frame, notifies the listener of the read frame.
     */
        void readFrame(IChannelHandlerContext ctx, IByteBuffer input, Http2FrameListener listener);

        /**
     * Get the configuration related elements for this {@link Http2FrameReader}
     */
        Http2FrameReaderConfiguration configuration();
    }
}