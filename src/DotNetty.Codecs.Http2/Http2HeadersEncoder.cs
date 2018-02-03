// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DotNetty.Buffers;
using DotNetty.Codecs.Http2;

public interface Http2HeadersEncoder
{
    /**
     * Encodes the given headers and writes the output headers block to the given output buffer.
     *
     * @param streamId  the identifier of the stream for which the headers are encoded.
     * @param headers the headers to be encoded.
     * @param buffer the buffer to receive the encoded headers.
     */
    void encodeHeaders(int streamId, Http2Headers headers, IByteBuffer buffer);

    /**
     * Get the {@link Configuration} for this {@link Http2HeadersEncoder}
     */
    Http2HeadersEncoderConfiguration configuration();
}

