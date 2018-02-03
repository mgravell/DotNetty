// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
       * Object that performs the writing of the bytes that have been allocated for a stream.
       */
    public interface Http2StreamWriter
    {
        /**
              * Writes the allocated bytes for this stream.
              * <p>
              * Any {@link Throwable} thrown from this method is considered a programming error.
              * A {@code GOAWAY} frame will be sent and the will be connection closed.
              * @param stream the stream for which to perform the write.
              * @param numBytes the number of bytes to write.
              */
        void write(Http2Stream stream, int numBytes);
    }
}