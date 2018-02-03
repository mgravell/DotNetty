// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    
    /**
     * Configuration specific to {@link Http2FrameReader}
     */
    public interface Http2FrameReaderConfiguration
    {
        /**
         * Get the {@link Http2HeadersDecoder.Configuration} for this {@link Http2FrameReader}
         */
        Http2HeadersDecoderConfiguration headersConfiguration();

        /**
         * Get the {@link Http2FrameSizePolicy} for this {@link Http2FrameReader}
         */
        Http2FrameSizePolicy frameSizePolicy();

    }
}