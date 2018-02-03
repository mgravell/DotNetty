// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
       * Configuration related elements for the {@link Http2HeadersDecoder} interface
       */
    public interface Http2HeadersDecoderConfiguration
    {
        /**
              * Represents the value for
              * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>.
              * This method should only be called by Netty (not users) as a result of a receiving a {@code SETTINGS} frame.
              */
        void maxHeaderTableSize(long max);

        /**
              * Represents the value for
              * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>. The initial value
              * returned by this method must be {@link Http2CodecUtil#DEFAULT_HEADER_TABLE_SIZE}.
              */
        long maxHeaderTableSize();

        /**
              * Configure the maximum allowed size in bytes of each set of headers.
              * <p>
              * This method should only be called by Netty (not users) as a result of a receiving a {@code SETTINGS} frame.
              * @param max <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
              *      If this limit is exceeded the implementation should attempt to keep the HPACK header tables up to date
              *      by processing data from the peer, but a {@code RST_STREAM} frame will be sent for the offending stream.
              * @param goAwayMax Must be {@code >= max}. A {@code GO_AWAY} frame will be generated if this limit is exceeded
              *                  for any particular stream.
              * @throws Http2Exception if limits exceed the RFC's boundaries or {@code max > goAwayMax}.
              */
        void maxHeaderListSize(long max, long goAwayMax);

        /**
              * Represents the value for
              * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
              */
        long maxHeaderListSize();

        /**
              * Represents the upper bound in bytes for a set of headers before a {@code GO_AWAY} should be sent.
              * This will be {@code <=}
              * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
              */
        long maxHeaderListSizeGoAway();
    }
}