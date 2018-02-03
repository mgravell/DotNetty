// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /**
     * HTTP/2 GOAWAY frame.
     *
     * <p>The last stream identifier <em>must not</em> be set by the application, but instead the
     * relative {@link #extraStreamIds()} should be used. The {@link #lastStreamId()} will only be
     * set for incoming GOAWAY frames by the HTTP/2 codec.
     *
     * <p>Graceful shutdown as described in the HTTP/2 spec can be accomplished by calling
     * {@code #setExtraStreamIds(Integer.MAX_VALUE)}.
     */

    public interface Http2GoAwayFrame : Http2Frame, IByteBufferHolder
    {
        /**
           * The reason for beginning closure of the connection. Represented as an HTTP/2 error code.
           */
        ulong errorCode();

        /**
           * The number of IDs to reserve for the receiver to use while GOAWAY is in transit. This allows
           * for new streams currently en route to still be created, up to a point, which allows for very
           * graceful shutdown of both sides.
           */
        int extraStreamIds();

        /**
           * Sets the number of IDs to reserve for the receiver to use while GOAWAY is in transit.
           *
           * @see #extraStreamIds
           * @return {@code this}
           */
        Http2GoAwayFrame setExtraStreamIds(int extraStreamIds);

        /**
           * Returns the last stream identifier if set, or {@code -1} else.
           */
        int lastStreamId();
    }
}