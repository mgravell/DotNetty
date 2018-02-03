// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /**
    * Manager for the life cycle of the HTTP/2 connection. Handles graceful shutdown of the channel,
    * closing only after all of the streams have closed.
    */

    public interface Http2LifecycleManager
    {
        /**
           * Closes the local side of the {@code stream}. Depending on the {@code stream} state this may result in
           * {@code stream} being closed. See {@link #closeStream(Http2Stream, Task)}.
           * @param stream the stream to be half closed.
           * @param future See {@link #closeStream(Http2Stream, Task)}.
           */
        void closeStreamLocal(Http2Stream stream, Task future);

        /**
           * Closes the remote side of the {@code stream}. Depending on the {@code stream} state this may result in
           * {@code stream} being closed. See {@link #closeStream(Http2Stream, Task)}.
           * @param stream the stream to be half closed.
           * @param future See {@link #closeStream(Http2Stream, Task)}.
           */
        void closeStreamRemote(Http2Stream stream, Task future);

        /**
           * Closes and deactivates the given {@code stream}. A listener is also attached to {@code future} and upon
           * completion the underlying channel will be closed if {@link Http2Connection#numActiveStreams()} is 0.
           * @param stream the stream to be closed and deactivated.
           * @param future when completed if {@link Http2Connection#numActiveStreams()} is 0 then the underlying channel
           * will be closed.
           */
        void closeStream(Http2Stream stream, Task future);

        /**
           * Ensure the stream identified by {@code streamId} is reset. If our local state does not indicate the stream has
           * been reset yet then a {@code RST_STREAM} will be sent to the peer. If our local state indicates the stream
           * has already been reset then the return status will indicate success without sending anything to the peer.
           * @param ctx The context used for communication and buffer allocation if necessary.
           * @param streamId The identifier of the stream to reset.
           * @param errorCode Justification as to why this stream is being reset. See {@link Http2Error}.
           * @param promise Used to indicate the return status of this operation.
           * @return Will be considered successful when the connection and stream state has been updated, and a
           * {@code RST_STREAM} frame has been sent to the peer. If the stream state has already been updated and a
           * {@code RST_STREAM} frame has been sent then the return status may indicate success immediately.
           */
        Task resetStream(
            IChannelHandlerContext ctx,
            int streamId,
            long errorCode,
            TaskCompletionSource promise);

        /**
           * Prevents the peer from creating streams and close the connection if {@code errorCode} is not
           * {@link Http2Error#NO_ERROR}. After this call the peer is not allowed to create any new streams and the local
           * endpoint will be limited to creating streams with {@code stream identifier <= lastStreamId}. This may result in
           * sending a {@code GO_AWAY} frame (assuming we have not already sent one with
           * {@code Last-Stream-ID <= lastStreamId}), or may just return success if a {@code GO_AWAY} has previously been
           * sent.
           * @param ctx The context used for communication and buffer allocation if necessary.
           * @param lastStreamId The last stream that the local endpoint is claiming it will accept.
           * @param errorCode The rational as to why the connection is being closed. See {@link Http2Error}.
           * @param debugData For diagnostic purposes (carries no semantic value).
           * @param promise Used to indicate the return status of this operation.
           * @return Will be considered successful when the connection and stream state has been updated, and a
           * {@code GO_AWAY} frame has been sent to the peer. If the stream state has already been updated and a
           * {@code GO_AWAY} frame has been sent then the return status may indicate success immediately.
           */
        Task goAway(IChannelHandlerContext ctx, int lastStreamId, long errorCode, IByteBuffer debugData, TaskCompletionSource promise);

        /**
           * Processes the given error.
           *
           * @param ctx The context used for communication and buffer allocation if necessary.
           * @param outbound {@code true} if the error was caused by an outbound operation and so the corresponding
           * {@link TaskCompletionSource} was failed as well.
           * @param cause the error.
           */
        void onError(IChannelHandlerContext ctx, bool outbound, Exception cause);
    }
}