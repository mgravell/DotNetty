// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    public interface Http2RemoteFlowController : Http2FlowController
    {
        /**
           * Get the {@link IChannelHandlerContext} for which to apply flow control on.
           * <p>
           * This is intended for us by {@link FlowControlled} implementations only. Use with caution.
           * @return The {@link IChannelHandlerContext} for which to apply flow control on.
           */
        IChannelHandlerContext channelHandlerContext();

        /**
           * Queues a payload for transmission to the remote endpoint. There is no guarantee as to when the data
           * will be written or how it will be assigned to frames.
           * before sending.
           * <p>
           * Writes do not actually occur until {@link #writePendingBytes()} is called.
           *
           * @param stream the subject stream. Must not be the connection stream object.
           * @param payload payload to write subject to flow-control accounting and ordering rules.
           */
        void addFlowControlled(Http2Stream stream, FlowControlled payload);

        /**
           * Determine if {@code stream} has any {@link FlowControlled} frames currently queued.
           * @param stream the stream to check if it has flow controlled frames.
           * @return {@code true} if {@code stream} has any {@link FlowControlled} frames currently queued.
           */
        bool hasFlowControlled(Http2Stream stream);

        /**
           * Write all data pending in the flow controller up to the flow-control limits.
           *
           * @throws Http2Exception throws if a protocol-related error occurred.
           */
        void writePendingBytes();

        /**
           * Set the active listener on the flow-controller.
           *
           * @param listener to notify when the a write occurs, can be {@code null}.
           */
        void listener(Http2FlowControlListener listener);

        /**
           * Determine if the {@code stream} has bytes remaining for use in the flow control window.
           * <p>
           * Note that this method respects channel writability. The channel must be writable for this method to
           * return {@code true}.
           *
           * @param stream The stream to test.
           * @return {@code true} if the {@code stream} has bytes remaining for use in the flow control window and the
           * channel is writable, {@code false} otherwise.
           */
        bool isWritable(Http2Stream stream);

        /**
           * Notification that the writability of {@link #IChannelHandlerContext()} has changed.
           * @throws Http2Exception If any writes occur as a result of this call and encounter errors.
           */
        void channelWritabilityChanged();

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
    }
}