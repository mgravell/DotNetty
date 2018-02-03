// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /**
        * Listener for life-cycle events for streams in this connection.
        */
    public interface Http2ConnectionListener
    {
        /**
         * Notifies the listener that the given stream was added to the connection. This stream may
         * not yet be active (i.e. {@code OPEN} or {@code HALF CLOSED}).
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         */
        void onStreamAdded(Http2Stream stream);

        /**
         * Notifies the listener that the given stream was made active (i.e. {@code OPEN} or {@code HALF CLOSED}).
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         */
        void onStreamActive(Http2Stream stream);

        /**
         * Notifies the listener that the given stream has transitioned from {@code OPEN} to {@code HALF CLOSED}.
         * This method will <strong>not</strong> be called until a state transition occurs from when
         * {@link #onStreamActive(Http2Stream)} was called.
         * The stream can be inspected to determine which side is {@code HALF CLOSED}.
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         */
        void onStreamHalfClosed(Http2Stream stream);

        /**
         * Notifies the listener that the given stream is now {@code CLOSED} in both directions and will no longer
         * be accessible via {@link #forEachActiveStream(Http2StreamVisitor)}.
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         */
        void onStreamClosed(Http2Stream stream);

        /**
         * Notifies the listener that the given stream has now been removed from the connection and
         * will no longer be returned via {@link Http2Connection#stream(int)}. The connection may
         * maintain inactive streams for some time before removing them.
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         */
        void onStreamRemoved(Http2Stream stream);

        /**
         * Called when a {@code GOAWAY} frame was sent for the connection.
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         * @param lastStreamId the last known stream of the remote endpoint.
         * @param errorCode    the error code, if abnormal closure.
         * @param debugData    application-defined debug data.
         */
        void onGoAwaySent(int lastStreamId, long errorCode, IByteBuffer debugData);

        /**
         * Called when a {@code GOAWAY} was received from the remote endpoint. This event handler duplicates {@link
         * Http2FrameListener#onGoAwayRead(io.netty.channel.ChannelHandlerContext, int, long, io.netty.buffer.IByteBuffer)}
         * but is added here in order to simplify application logic for handling {@code GOAWAY} in a uniform way. An
         * application should generally not handle both events, but if it does this method is called second, after
         * notifying the {@link Http2FrameListener}.
         * <p>
         * If a {@link RuntimeException} is thrown it will be logged and <strong>not propagated</strong>.
         * Throwing from this method is not supported and is considered a programming error.
         * @param lastStreamId the last known stream of the remote endpoint.
         * @param errorCode    the error code, if abnormal closure.
         * @param debugData    application-defined debug data.
         */
        void onGoAwayReceived(int lastStreamId, long errorCode, IByteBuffer debugData);
    }
}