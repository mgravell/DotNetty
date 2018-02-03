// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * A single stream within a HTTP/2 connection. To be used with the {@link Http2FrameCodec}.
    */
    public interface Http2FrameStream
    {
        /**
         * Returns the stream identifier.
         *
         * <p>Use {@link Http2CodecUtil#isStreamIdValid(int)} to check if the stream has already been assigned an
         * identifier.
         */
        int id();

        /**
         * Returns the state of this stream.
         */
        Http2StreamState state();
    }
}