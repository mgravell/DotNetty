// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public interface Http2FrameListener
    {
        /**
           * Handles an inbound {@code DATA} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the subject stream for the frame.
           * @param data payload buffer for the frame. This buffer will be released by the codec.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param endOfStream Indicates whether this is the last frame to be sent from the remote endpoint for this stream.
           * @return the number of bytes that have been processed by the application. The returned bytes are used by the
           * inbound flow controller to determine the appropriate time to expand the inbound flow control window (i.e. send
           * {@code WINDOW_UPDATE}). Returning a value equal to the length of {@code data} + {@code padding} will effectively
           * opt-out of application-level flow control for this frame. Returning a value less than the length of {@code data}
           * + {@code padding} will defer the returning of the processed bytes, which the application must later return via
           * {@link Http2LocalFlowController#consumeBytes(Http2Stream, int)}. The returned value must
           * be >= {@code 0} and <= {@code data.readableBytes()} + {@code padding}.
           */
        int onDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream);

        /**
           * Handles an inbound {@code HEADERS} frame.
           * <p>
           * Only one of the following methods will be called for each {@code HEADERS} frame sequence.
           * One will be called when the {@code END_HEADERS} flag has been received.
           * <ul>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, bool)}</li>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, short, bool, int, bool)}</li>
           * <li>{@link #onPushPromiseRead(IChannelHandlerContext, int, int, Http2Headers, int)}</li>
           * </ul>
           * <p>
           * To say it another way; the {@link Http2Headers} will contain all of the headers
           * for the current message exchange step (additional queuing is not necessary).
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the subject stream for the frame.
           * @param headers the received headers.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param endOfStream Indicates whether this is the last frame to be sent from the remote endpoint
           *            for this stream.
           */
        void onHeadersRead(IChannelHandlerContext ctx, int streamId, Http2Headers headers, int padding, bool endOfStream);

        /**
           * Handles an inbound {@code HEADERS} frame with priority information specified.
           * Only called if {@code END_HEADERS} encountered.
           * <p>
           * Only one of the following methods will be called for each {@code HEADERS} frame sequence.
           * One will be called when the {@code END_HEADERS} flag has been received.
           * <ul>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, bool)}</li>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, short, bool, int, bool)}</li>
           * <li>{@link #onPushPromiseRead(IChannelHandlerContext, int, int, Http2Headers, int)}</li>
           * </ul>
           * <p>
           * To say it another way; the {@link Http2Headers} will contain all of the headers
           * for the current message exchange step (additional queuing is not necessary).
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the subject stream for the frame.
           * @param headers the received headers.
           * @param streamDependency the stream on which this stream depends, or 0 if dependent on the
           *            connection.
           * @param weight the new weight for the stream.
           * @param exclusive whether or not the stream should be the exclusive dependent of its parent.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param endOfStream Indicates whether this is the last frame to be sent from the remote endpoint
           *            for this stream.
           */
        void onHeadersRead(IChannelHandlerContext ctx, int streamId, Http2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream);

        /**
           * Handles an inbound {@code PRIORITY} frame.
           * <p>
           * Note that is it possible to have this method called and no stream object exist for either
           * {@code streamId}, {@code streamDependency}, or both. This is because the {@code PRIORITY} frame can be
           * sent/received when streams are in the {@code CLOSED} state.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the subject stream for the frame.
           * @param streamDependency the stream on which this stream depends, or 0 if dependent on the
           *            connection.
           * @param weight the new weight for the stream.
           * @param exclusive whether or not the stream should be the exclusive dependent of its parent.
           */
        void onPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive);

        /**
           * Handles an inbound {@code RST_STREAM} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the stream that is terminating.
           * @param errorCode the error code identifying the type of failure.
           */
        void onRstStreamRead(IChannelHandlerContext ctx, int streamId, long errorCode);

        /**
           * Handles an inbound {@code SETTINGS} acknowledgment frame.
           * @param ctx the context from the handler where the frame was read.
           */
        void onSettingsAckRead(IChannelHandlerContext ctx);

        /**
           * Handles an inbound {@code SETTINGS} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param settings the settings received from the remote endpoint.
           */
        void onSettingsRead(IChannelHandlerContext ctx, Http2Settings settings);

        /**
           * Handles an inbound {@code PING} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param data the payload of the frame. If this buffer needs to be retained by the listener
           *            they must make a copy.
           */
        void onPingRead(IChannelHandlerContext ctx, IByteBuffer data);

        /**
           * Handles an inbound {@code PING} acknowledgment.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param data the payload of the frame. If this buffer needs to be retained by the listener
           *            they must make a copy.
           */
        void onPingAckRead(IChannelHandlerContext ctx, IByteBuffer data);

        /**
           * Handles an inbound {@code PUSH_PROMISE} frame. Only called if {@code END_HEADERS} encountered.
           * <p>
           * Promised requests MUST be authoritative, cacheable, and safe.
           * See <a href="https://tools.ietf.org/html/draft-ietf-httpbis-http2-17#section-8.2">[RFC http2], Section 8.2</a>.
           * <p>
           * Only one of the following methods will be called for each {@code HEADERS} frame sequence.
           * One will be called when the {@code END_HEADERS} flag has been received.
           * <ul>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, bool)}</li>
           * <li>{@link #onHeadersRead(IChannelHandlerContext, int, Http2Headers, int, short, bool, int, bool)}</li>
           * <li>{@link #onPushPromiseRead(IChannelHandlerContext, int, int, Http2Headers, int)}</li>
           * </ul>
           * <p>
           * To say it another way; the {@link Http2Headers} will contain all of the headers
           * for the current message exchange step (additional queuing is not necessary).
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the stream the frame was sent on.
           * @param promisedStreamId the ID of the promised stream.
           * @param headers the received headers.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           */
        void onPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, Http2Headers headers, int padding);

        /**
           * Handles an inbound {@code GO_AWAY} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param lastStreamId the last known stream of the remote endpoint.
           * @param errorCode the error code, if abnormal closure.
           * @param debugData application-defined debug data. If this buffer needs to be retained by the
           *            listener they must make a copy.
           */
        void onGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, long errorCode, IByteBuffer debugData);

        /**
           * Handles an inbound {@code WINDOW_UPDATE} frame.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param streamId the stream the frame was sent on.
           * @param windowSizeIncrement the increased number of bytes of the remote endpoint's flow
           *            control window.
           */
        void onWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement);

        /**
           * Handler for a frame not defined by the HTTP/2 spec.
           *
           * @param ctx the context from the handler where the frame was read.
           * @param frameType the frame type from the HTTP/2 header.
           * @param streamId the stream the frame was sent on.
           * @param flags the flags in the frame header.
           * @param payload the payload of the frame.
           */
        void onUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload);
    }
}