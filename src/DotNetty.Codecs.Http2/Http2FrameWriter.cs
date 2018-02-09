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
     * A writer responsible for marshaling HTTP/2 frames to the channel. All of the write methods in
     * this interface write to the context, but DO NOT FLUSH. To perform a flush, you must separately
     * call {@link IChannelHandlerContext#flush()}.
     */
    public interface Http2FrameWriter : Http2DataWriter, IDisposable
    {
        /**
           * Writes a HEADERS frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param headers the headers to be sent.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param endStream indicates if this is the last frame to be sent for the stream.
           * @param promise the promise for the write.
           * @return the future for the write.
           * <a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
           * <pre>
           * The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.
           * </pre>
           * If this call has modified the HPACK header state you <strong>MUST</strong> throw a connection error.
           * <p>
           * If this call has <strong>NOT</strong> modified the HPACK header state you are free to throw a stream error.
           */
        Task writeHeaders(IChannelHandlerContext ctx, int streamId, Http2Headers headers, int padding, bool endStream, IPromise promise);

        /**
           * Writes a HEADERS frame with priority specified to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param headers the headers to be sent.
           * @param streamDependency the stream on which this stream should depend, or 0 if it should
           *            depend on the connection.
           * @param weight the weight for this stream.
           * @param exclusive whether this stream should be the exclusive dependant of its parent.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param endStream indicates if this is the last frame to be sent for the stream.
           * @param promise the promise for the write.
           * @return the future for the write.
           * <a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
           * <pre>
           * The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.
           * </pre>
           * If this call has modified the HPACK header state you <strong>MUST</strong> throw a connection error.
           * <p>
           * If this call has <strong>NOT</strong> modified the HPACK header state you are free to throw a stream error.
           */
        Task writeHeaders(IChannelHandlerContext ctx, int streamId, Http2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endStream, IPromise promise);

        /**
           * Writes a PRIORITY frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param streamDependency the stream on which this stream should depend, or 0 if it should
           *            depend on the connection.
           * @param weight the weight for this stream.
           * @param exclusive whether this stream should be the exclusive dependant of its parent.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writePriority(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise);

        /**
           * Writes a RST_STREAM frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param errorCode the error code indicating the nature of the failure.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeRstStream(IChannelHandlerContext ctx, int streamId, long errorCode, IPromise promise);

        /**
           * Writes a SETTINGS frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param settings the settings to be sent.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeSettings(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise);

        /**
           * Writes a SETTINGS acknowledgment to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeSettingsAck(IChannelHandlerContext ctx, IPromise promise);

        /**
           * Writes a PING frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param ack indicates whether this is an ack of a PING frame previously received from the
           *            remote endpoint.
           * @param data the payload of the frame. This will be released by this method.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writePing(IChannelHandlerContext ctx, bool ack, IByteBuffer data, IPromise promise);

        /**
           * Writes a PUSH_PROMISE frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param promisedStreamId the ID of the promised stream.
           * @param headers the headers to be sent.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive).
           * @param promise the promise for the write.
           * @return the future for the write.
           * <a href="https://tools.ietf.org/html/rfc7540#section-10.5.1">Section 10.5.1</a> states the following:
           * <pre>
           * The header block MUST be processed to ensure a consistent connection state, unless the connection is closed.
           * </pre>
           * If this call has modified the HPACK header state you <strong>MUST</strong> throw a connection error.
           * <p>
           * If this call has <strong>NOT</strong> modified the HPACK header state you are free to throw a stream error.
           */
        Task writePushPromise(IChannelHandlerContext ctx, int streamId, int promisedStreamId, Http2Headers headers, int padding, IPromise promise);

        /**
           * Writes a GO_AWAY frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param lastStreamId the last known stream of this endpoint.
           * @param errorCode the error code, if the connection was abnormally terminated.
           * @param debugData application-defined debug data. This will be released by this method.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeGoAway(IChannelHandlerContext ctx, int lastStreamId, long errorCode, IByteBuffer debugData, IPromise promise);

        /**
           * Writes a WINDOW_UPDATE frame to the remote endpoint.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param windowSizeIncrement the number of bytes by which the local inbound flow control window
           *            is increasing.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeWindowUpdate(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise);

        /**
           * Generic write method for any HTTP/2 frame. This allows writing of non-standard frames.
           *
           * @param ctx the context to use for writing.
           * @param frameType the frame type identifier.
           * @param streamId the stream for which to send the frame.
           * @param flags the flags to write for this frame.
           * @param payload the payload to write for this frame. This will be released by this method.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise);

        /**
           * Get the configuration related elements for this {@link Http2FrameWriter}
           */
        Http2FrameWriterConfiguration configuration();
    }
}