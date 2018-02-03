// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * A frame whose meaning <em>may</em> apply to a particular stream, instead of the entire connection. It is still
    * possible for this frame type to apply to the entire connection. In such cases, the {@link #stream()} must return
    * {@code null}. If the frame applies to a stream, the {@link Http2FrameStream#id()} must be greater than zero.
    */
    public interface Http2StreamFrame : Http2Frame
    {
        /**
         * Set the {@link Http2FrameStream} object for this frame.
         */
        Http2StreamFrame stream(Http2FrameStream stream);

        /**
         * Returns the {@link Http2FrameStream} object for this frame, or {@code null} if the frame has yet to be associated
         * with a stream.
         */
        Http2FrameStream stream();
    }
}