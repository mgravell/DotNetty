// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
     * HTTP/2 WINDOW_UPDATE frame.
     */
    public interface Http2WindowUpdateFrame : Http2StreamFrame
    {
        /**
     * Number of bytes to increment the HTTP/2 stream's or connection's flow control window.
     */
        int windowSizeIncrement();
    }
}