// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /**
    * Decodes HPACK-encoded headers blocks into {@link Http2Headers}.
    */
    public interface Http2HeadersDecoder
    {
        /**
           * Decodes the given headers block and returns the headers.
           */
        Http2Headers decodeHeaders(int streamId, IByteBuffer headerBlock);

        /**
           * Get the {@link Configuration} for this {@link Http2HeadersDecoder}
           */
        Http2HeadersDecoderConfiguration configuration();
    }
}