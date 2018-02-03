// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * Configuration related elements for the {@link Http2HeadersEncoder} interface
    */
    public interface Http2HeadersEncoderConfiguration
    {
        /**
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>.
         * This method should only be called by Netty (not users) as a result of a receiving a {@code SETTINGS} frame.
         */
        void maxHeaderTableSize(long max);

        /**
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_HEADER_TABLE_SIZE</a>.
         * The initial value returned by this method must be {@link Http2CodecUtil#DEFAULT_HEADER_TABLE_SIZE}.
         */
        long maxHeaderTableSize();

        /**
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
         * This method should only be called by Netty (not users) as a result of a receiving a {@code SETTINGS} frame.
         */
        void maxHeaderListSize(long max);

        /**
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
         */
        long maxHeaderListSize();
    }
}