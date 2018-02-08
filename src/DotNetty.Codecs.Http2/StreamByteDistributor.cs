// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
    * An object (used by remote flow control) that is responsible for distributing the bytes to be
    * written across the streams in the connection.
    */

    public interface StreamByteDistributor
    {
        /**
           * Called when the streamable bytes for a stream has changed. Until this
           * method is called for the first time for a give stream, the stream is assumed to have no
           * streamable bytes.
           */
        void updateStreamableBytes(StreamByteDistributorContext state);

        /**
           * Explicitly update the dependency tree. This method is called independently of stream state changes.
           * @param childStreamId The stream identifier associated with the child stream.
           * @param parentStreamId The stream identifier associated with the parent stream. May be {@code 0},
           *                       to make {@code childStreamId} and immediate child of the connection.
           * @param weight The weight which is used relative to other child streams for {@code parentStreamId}. This value
           *               must be between 1 and 256 (inclusive).
           * @param exclusive If {@code childStreamId} should be the exclusive dependency of {@code parentStreamId}.
           */
        void updateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive);

        /**
           * Distributes up to {@code maxBytes} to those streams containing streamable bytes and
           * iterates across those streams to write the appropriate bytes. Criteria for
           * traversing streams is undefined and it is up to the implementation to determine when to stop
           * at a given stream.
           *
           * <p>The streamable bytes are not automatically updated by calling this method. It is up to the
           * caller to indicate the number of bytes streamable after the write by calling
           * {@link #updateStreamableBytes(StreamState)}.
           *
           * @param maxBytes the maximum number of bytes to write.
           * @return {@code true} if there are still streamable bytes that have not yet been written,
           * otherwise {@code false}.
           * @throws Http2Exception If an internal exception occurs and internal connection state would otherwise be
           * corrupted.
           */
        bool distribute(int maxBytes, Action<Http2Stream, int> writer);
    }
}