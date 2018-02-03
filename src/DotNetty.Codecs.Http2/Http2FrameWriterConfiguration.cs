// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * A writer responsible for marshaling HTTP/2 frames to the channel. All of the write methods in
    * this interface write to the context, but DO NOT FLUSH. To perform a flush, you must separately
    * call {@link IChannelHandlerContext#flush()}.
    */
    public interface Http2FrameWriterConfiguration
    {
        /**
                 * Get the {@link Http2HeadersEncoder.Configuration} for this {@link Http2FrameWriter}
                 */
        Http2HeadersEncoderConfiguration headersConfiguration();

        /**
              * Get the {@link Http2FrameSizePolicy} for this {@link Http2FrameWriter}
              */
        Http2FrameSizePolicy frameSizePolicy();
    }
}