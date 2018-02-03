// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /**
         * Determine if a header name/value pair is treated as
         * <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
         * If the object can be dynamically modified and shared across multiple connections it may need to be thread safe.
         */
    public interface SensitivityDetector
    {
        /**
         * Determine if a header {@code name}/{@code value} pair should be treated as
         * <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
         * @param name The name for the header.
         * @param value The value of the header.
         * @return {@code true} if a header {@code name}/{@code value} pair should be treated as
         * <a href="http://tools.ietf.org/html/draft-ietf-httpbis-header-compression-12#section-7.1.3">sensitive</a>.
         * {@code false} otherwise.
         */
        bool isSensitive(ICharSequence name, ICharSequence value);
    }
}