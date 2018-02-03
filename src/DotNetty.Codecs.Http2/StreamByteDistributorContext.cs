// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
   * State information for the stream, indicating the number of bytes that are currently
   * streamable. This is provided to the {@link #updateStreamableBytes(StreamState)} method.
   */
    public interface StreamByteDistributorContext
    {
        /**
       * Gets the stream this state is associated with.
       */
        Http2Stream stream();

        /**
              * Get the amount of bytes this stream has pending to send. The actual amount written must not exceed
              * {@link #windowSize()}!
              * @return The amount of bytes this stream has pending to send.
              * @see Http2CodecUtil#streamableBytes(StreamState)
              */
        long pendingBytes();

        /**
              * Indicates whether or not there are frames pending for this stream.
              */
        bool hasFrame();

        /**
              * The size (in bytes) of the stream's flow control window. The amount written must not exceed this amount!
              * <p>A {@link StreamByteDistributor} needs to know the stream's window size in order to avoid allocating bytes
              * if the window size is negative. The window size being {@code 0} may also be significant to determine when if
              * an stream has been given a chance to write an empty frame, and also enables optimizations like not writing
              * empty frames in some situations (don't write headers until data can also be written).
              * @return the size of the stream's flow control window.
              * @see Http2CodecUtil#streamableBytes(StreamState)
              */
        int windowSize();
    }
}