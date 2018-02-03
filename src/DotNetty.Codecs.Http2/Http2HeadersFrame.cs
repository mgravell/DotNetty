// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Threading;

    /**
  * HTTP/2 HEADERS frame.
  */
    public interface Http2HeadersFrame : Http2StreamFrame
    {
        /**
           * A complete header list. CONTINUATION frames are automatically handled.
           */
        Http2Headers headers();

        /**
           * Frame padding to use. Must be non-negative and less than 256.
           */
        int padding();

        /**
           * Returns {@code true} if the END_STREAM flag ist set.
           */
        bool isEndStream();
    }
}