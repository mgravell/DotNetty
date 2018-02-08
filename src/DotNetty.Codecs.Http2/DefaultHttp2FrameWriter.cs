using DotNetty.Codecs.Http2;

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Transport.Channels;

    /**
 * A {@link Http2FrameWriter} that supports all frame types defined by the HTTP/2 specification.
 */
    public class DefaultHttp2FrameWriter : Http2FrameWriter, Http2FrameSizePolicy, Http2FrameWriterConfiguration 
    {
    private static readonly string STREAM_ID = "Stream ID";
    private static readonly string STREAM_DEPENDENCY = "Stream Dependency";
    /**
     * This buffer is allocated to the maximum size of the padding field, and filled with zeros.
     * When padding is needed it can be taken as a slice of this buffer. Users should call {@link IByteBuffer#retain()}
     * before using their slice.
     */
    private static readonly IByteBuffer ZERO_BUFFER =
            Unpooled.UnreleasableBuffer(Unpooled.DirectBuffer(Http2CodecUtil.MAX_UNSIGNED_BYTE).WriteZero(Http2CodecUtil.MAX_UNSIGNED_BYTE)).AsReadOnly();

    readonly Http2HeadersEncoder headersEncoder;
    private int _maxFrameSize;

    public DefaultHttp2FrameWriter() : this(new DefaultHttp2HeadersEncoder())
    {
        
    }

    public DefaultHttp2FrameWriter(SensitivityDetector headersSensitivityDetector) : this(new DefaultHttp2HeadersEncoder(headersSensitivityDetector))
    {
        
    }

    public DefaultHttp2FrameWriter(SensitivityDetector headersSensitivityDetector, bool ignoreMaxHeaderListSize) : this(new DefaultHttp2HeadersEncoder(headersSensitivityDetector, ignoreMaxHeaderListSize))
    {
        
    }

    public DefaultHttp2FrameWriter(Http2HeadersEncoder headersEncoder) {
        this.headersEncoder = headersEncoder;
        _maxFrameSize = Http2CodecUtil.DEFAULT_MAX_FRAME_SIZE;
    }

    public Http2FrameWriterConfiguration configuration() {
        return this;
    }

    public Http2HeadersEncoderConfiguration headersConfiguration() {
        return headersEncoder.configuration();
    }

    public Http2FrameSizePolicy frameSizePolicy() {
        return this;
    }

    public void maxFrameSize(int max)  {
        if (!Http2CodecUtil.isMaxFrameSizeValid(max)) {
            throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Invalid MAX_FRAME_SIZE specified in sent settings: {0}", max);
        }
        _maxFrameSize = max;
    }

    public int maxFrameSize() {
        return _maxFrameSize;
    }

    public void Dispose()
    {
        
    }

    public Task writeData(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endStream, TaskCompletionSource promise) 
    {
        SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        IByteBuffer frameHeader = null;
        try {
            verifyStreamId(streamId, STREAM_ID);
            Http2CodecUtil.verifyPadding(padding);

            int remainingData = data.ReadableBytes;
            Http2Flags flags = new Http2Flags();
            flags.endOfStream(false);
            flags.paddingPresent(false);
            // Fast path to write frames of payload size maxFrameSize first.
            if (remainingData > _maxFrameSize) {
                frameHeader = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
                Http2CodecUtil.writeFrameHeaderInternal(frameHeader, _maxFrameSize, Http2FrameTypes.DATA, flags, streamId);
                do {
                    // Write the header.
                    ctx.WriteAsync(frameHeader.RetainedSlice(), promiseAggregator.newPromise());

                    // Write the payload.
                    ctx.WriteAsync(data.ReadRetainedSlice( _maxFrameSize), promiseAggregator.newPromise());

                    remainingData -=  _maxFrameSize;
                    // Stop iterating if remainingData ==  _maxFrameSize so we can take care of reference counts below.
                } while (remainingData >  _maxFrameSize);
            }

            if (padding == 0) {
                // Write the header.
                if (frameHeader != null) {
                    frameHeader.Release();
                    frameHeader = null;
                }
                IByteBuffer frameHeader2 = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
                flags.endOfStream(endStream);
                Http2CodecUtil.writeFrameHeaderInternal(frameHeader2, remainingData, Http2FrameTypes.DATA, flags, streamId);
                ctx.WriteAsync(frameHeader2, promiseAggregator.newPromise());

                // Write the payload.
                IByteBuffer lastFrame = data.ReadSlice(remainingData);
                data = null;
                ctx.WriteAsync(lastFrame, promiseAggregator.newPromise());
            } else {
                if (remainingData != _maxFrameSize) {
                    if (frameHeader != null) {
                        frameHeader.Release();
                        frameHeader = null;
                    }
                } else {
                    remainingData -=  _maxFrameSize;
                    // Write the header.
                    IByteBuffer lastFrame;
                    if (frameHeader == null) {
                        lastFrame = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
                        Http2CodecUtil.writeFrameHeaderInternal(lastFrame, _maxFrameSize, Http2FrameTypes.DATA, flags, streamId);
                    } else {
                        lastFrame = frameHeader.Slice();
                        frameHeader = null;
                    }
                    ctx.WriteAsync(lastFrame, promiseAggregator.newPromise());

                    // Write the payload.
                    lastFrame = data.ReadSlice(_maxFrameSize);
                    data = null;
                    ctx.WriteAsync(lastFrame, promiseAggregator.newPromise());
                }

                do {
                    int frameDataBytes = Math.Min(remainingData, _maxFrameSize);
                    int framePaddingBytes = Math.Min(padding, Math.Max(0, (_maxFrameSize - 1) - frameDataBytes));

                    // Decrement the remaining counters.
                    padding -= framePaddingBytes;
                    remainingData -= frameDataBytes;

                    // Write the header.
                    IByteBuffer frameHeader2 = ctx.Allocator.Buffer(Http2CodecUtil.DATA_FRAME_HEADER_LENGTH);
                    flags.endOfStream(endStream && remainingData == 0 && padding == 0);
                    flags.paddingPresent(framePaddingBytes > 0);
                    Http2CodecUtil.writeFrameHeaderInternal(frameHeader2, framePaddingBytes + frameDataBytes, Http2FrameTypes.DATA, flags, streamId);
                    writePaddingLength(frameHeader2, framePaddingBytes);
                    ctx.WriteAsync(frameHeader2, promiseAggregator.newPromise());

                    // Write the payload.
                    if (frameDataBytes != 0) {
                        if (remainingData == 0) {
                            IByteBuffer lastFrame = data.ReadSlice(frameDataBytes);
                            data = null;
                            ctx.WriteAsync(lastFrame, promiseAggregator.newPromise());
                        } else {
                            ctx.WriteAsync(data.ReadRetainedSlice(frameDataBytes), promiseAggregator.newPromise());
                        }
                    }
                    // Write the frame padding.
                    if (paddingBytes(framePaddingBytes) > 0) {
                        ctx.WriteAsync(ZERO_BUFFER.Slice(0, paddingBytes(framePaddingBytes)),
                                  promiseAggregator.newPromise());
                    }
                } while (remainingData != 0 || padding != 0);
            }
        } catch (Exception cause) {
            if (frameHeader != null) {
                frameHeader.Release();
            }
            // Use a try/finally here in case the data has been released before calling this method. This is not
            // necessary above because we internally allocate frameHeader.
            try {
                if (data != null) {
                    data.Release();
                }
            } finally {
                promiseAggregator.SetException(cause);
                promiseAggregator.doneAllocatingPromises();
            }
            return promiseAggregator;
        }
        return promiseAggregator.doneAllocatingPromises();
    }

    
    public Task writeHeaders(IChannelHandlerContext ctx, int streamId,
            Http2Headers headers, int padding, bool endStream, TaskCompletionSource promise) {
        return writeHeadersInternal(ctx, streamId, headers, padding, endStream, false, 0, 0, false, promise);
    }

    public Task writeHeaders(IChannelHandlerContext ctx, int streamId,
            Http2Headers headers, int streamDependency, short weight, bool exclusive,
            int padding, bool endStream, TaskCompletionSource promise) {
        return writeHeadersInternal(ctx, streamId, headers, padding, endStream,
                true, streamDependency, weight, exclusive, promise);
    }

    public Task writePriority(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, TaskCompletionSource promise) {
        try {
            verifyStreamId(streamId, STREAM_ID);
            verifyStreamId(streamDependency, STREAM_DEPENDENCY);
            verifyWeight(weight);

            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.PRIORITY_FRAME_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, Http2CodecUtil.PRIORITY_ENTRY_LENGTH, Http2FrameTypes.PRIORITY, new Http2Flags(), streamId);
            buf.WriteInt(exclusive ? (int) (0x80000000L | streamDependency) : streamDependency);
            // Adjust the weight so that it fits into a single byte on the wire.
            buf.WriteByte(weight - 1);
            return ctx.WriteAsync(buf, promise);
        } catch (Exception t) {
            return promise.SetException(t);
        }
    }

    public Task writeRstStream(IChannelHandlerContext ctx, int streamId, long errorCode, TaskCompletionSource promise) {
        try {
            verifyStreamId(streamId, STREAM_ID);
            verifyErrorCode(errorCode);

            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.RST_STREAM_FRAME_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, Http2CodecUtil.INT_FIELD_LENGTH, Http2FrameTypes.RST_STREAM, new Http2Flags(), streamId);
            buf.WriteInt((int) errorCode);
            return ctx.WriteAsync(buf, promise);
        } catch (Exception t) {
            return promise.SetException(t);
        }
    }

    public Task writeSettings(IChannelHandlerContext ctx, Http2Settings settings,
            TaskCompletionSource promise) {
        try {
            checkNotNull(settings, "settings");
            int payloadLength = Http2CodecUtil.SETTING_ENTRY_LENGTH * settings.Count;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH + settings.Count * Http2CodecUtil.SETTING_ENTRY_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.SETTINGS, new Http2Flags(), 0);
            foreach (KeyValuePair<char, long> entry in settings) {
                buf.WriteChar(entry.Key);
                buf.WriteInt((int)entry.Value);
            }
            return ctx.WriteAsync(buf, promise);
        } catch (Exception t) {
            return promise.SetException(t);
        }
    }

    public Task writeSettingsAck(IChannelHandlerContext ctx, TaskCompletionSource promise) {
        try {
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, 0, Http2FrameTypes.SETTINGS, new Http2Flags().ack(true), 0);
            return ctx.WriteAsync(buf, promise);
        } catch (Exception t) {
            return promise.SetException(t);
        }
    }

    public Task writePing(IChannelHandlerContext ctx, bool ack, IByteBuffer data, TaskCompletionSource promise) {
        final SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        try {
            verifyPingPayload(data);
            Http2Flags flags = ack ? new Http2Flags().ack(true) : new Http2Flags();
            int payloadLength = data.ReadableBytes;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
            // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
            // in the catch block.
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.PING, flags, 0);
            ctx.WriteAsync(buf, promiseAggregator.newPromise());
        } catch (Exception t) {
            try {
                data.Release();
            } finally {
                promiseAggregator.SetException(t);
                promiseAggregator.doneAllocatingPromises();
            }
            return promiseAggregator;
        }

        try {
            // Write the debug data.
            ctx.WriteAsync(data, promiseAggregator.newPromise());
        } catch (Exception t) {
            promiseAggregator.SetException(t);
        }

        return promiseAggregator.doneAllocatingPromises();
    }

    public Task writePushPromise(IChannelHandlerContext ctx, int streamId,
            int promisedStreamId, Http2Headers headers, int padding, TaskCompletionSource promise) {
        IByteBuffer headerBlock = null;
        SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        try {
            verifyStreamId(streamId, STREAM_ID);
            verifyStreamId(promisedStreamId, "Promised Stream ID");
            Http2CodecUtil.verifyPadding(padding);

            // Encode the entire header block into an intermediate buffer.
            headerBlock = ctx.Allocator.Buffer();
            headersEncoder.encodeHeaders(streamId, headers, headerBlock);

            // Read the first fragment (possibly everything).
            Http2Flags flags = new Http2Flags().paddingPresent(padding > 0);
            // INT_FIELD_LENGTH is for the length of the promisedStreamId
            int nonFragmentLength = Http2CodecUtil.INT_FIELD_LENGTH + padding;
            int maxFragmentLength =  _maxFrameSize - nonFragmentLength;
            IByteBuffer fragment = headerBlock.ReadRetainedSlice(Math.Min(headerBlock.ReadableBytes, maxFragmentLength));

            flags.endOfHeaders(!headerBlock.IsReadable());

            int payloadLength = fragment.ReadableBytes + nonFragmentLength;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.PUSH_PROMISE_FRAME_HEADER_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.PUSH_PROMISE, flags, streamId);
            writePaddingLength(buf, padding);

            // Write out the promised stream ID.
            buf.WriteInt(promisedStreamId);
            ctx.WriteAsync(buf, promiseAggregator.newPromise());

            // Write the first fragment.
            ctx.WriteAsync(fragment, promiseAggregator.newPromise());

            // Write out the padding, if any.
            if (paddingBytes(padding) > 0) {
                ctx.WriteAsync(ZERO_BUFFER.Slice(0, paddingBytes(padding)), promiseAggregator.newPromise());
            }

            if (!flags.endOfHeaders()) {
                writeContinuationFrames(ctx, streamId, headerBlock, padding, promiseAggregator);
            }
        } catch (Http2Exception e) {
            promiseAggregator.SetException(e);
        } catch (Exception t) {
            promiseAggregator.SetException(t);
            promiseAggregator.doneAllocatingPromises();
            throw;
        } finally {
            if (headerBlock != null) {
                headerBlock.Release();
            }
        }
        return promiseAggregator.doneAllocatingPromises();
    }

    public Task writeGoAway(IChannelHandlerContext ctx, int lastStreamId, long errorCode,
            IByteBuffer debugData, TaskCompletionSource promise) {
        SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        try {
            verifyStreamOrConnectionId(lastStreamId, "Last Stream ID");
            verifyErrorCode(errorCode);

            int payloadLength = 8 + debugData.ReadableBytes;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.GO_AWAY_FRAME_HEADER_LENGTH);
            // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
            // in the catch block.
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.GO_AWAY, new Http2Flags(), 0);
            buf.WriteInt(lastStreamId);
            buf.WriteInt((int) errorCode);
            ctx.WriteAsync(buf, promiseAggregator.newPromise());
        } catch (Exception t) {
            try {
                debugData.Release();
            } finally {
                promiseAggregator.SetException(t);
                promiseAggregator.doneAllocatingPromises();
            }
            return promiseAggregator;
        }

        try {
            ctx.WriteAsync(debugData, promiseAggregator.newPromise());
        } catch (Exception t) {
            promiseAggregator.SetException(t);
        }
        return promiseAggregator.doneAllocatingPromises();
    }

    public Task writeWindowUpdate(IChannelHandlerContext ctx, int streamId,
            int windowSizeIncrement, TaskCompletionSource promise) {
        try {
            verifyStreamOrConnectionId(streamId, STREAM_ID);
            verifyWindowSizeIncrement(windowSizeIncrement);

            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.WINDOW_UPDATE_FRAME_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, Http2CodecUtil.INT_FIELD_LENGTH, Http2FrameTypes.WINDOW_UPDATE, new Http2Flags(), streamId);
            buf.WriteInt(windowSizeIncrement);
            return ctx.WriteAsync(buf, promise);
        } catch (Exception t) {
            return promise.SetException(t);
        }
    }

    public Task writeFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId,
            Http2Flags flags, IByteBuffer payload, TaskCompletionSource promise) {
        SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        try {
            verifyStreamOrConnectionId(streamId, STREAM_ID);
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FRAME_HEADER_LENGTH);
            // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
            // in the catch block.
            Http2CodecUtil.writeFrameHeaderInternal(buf, payload.ReadableBytes, frameType, flags, streamId);
            ctx.WriteAsync(buf, promiseAggregator.newPromise());
        } catch (Exception t) {
            try {
                payload.Release();
            } finally {
                promiseAggregator.SetException(t);
                promiseAggregator.doneAllocatingPromises();
            }
            return promiseAggregator;
        }
        try {
            ctx.WriteAsync(payload, promiseAggregator.newPromise());
        } catch (Exception t) {
            promiseAggregator.SetException(t);
        }
        return promiseAggregator.doneAllocatingPromises();
    }

    private Task writeHeadersInternal(IChannelHandlerContext ctx,
            int streamId, Http2Headers headers, int padding, bool endStream,
            bool hasPriority, int streamDependency, short weight, bool exclusive, TaskCompletionSource promise) {
        IByteBuffer headerBlock = null;
        SimpleChannelPromiseAggregator promiseAggregator =
                new SimpleChannelPromiseAggregator(promise, ctx.Channel, ctx.Executor);
        try {
            verifyStreamId(streamId, STREAM_ID);
            if (hasPriority) {
                verifyStreamOrConnectionId(streamDependency, STREAM_DEPENDENCY);
                Http2CodecUtil.verifyPadding(padding);
                verifyWeight(weight);
            }

            // Encode the entire header block.
            headerBlock = ctx.Allocator.Buffer();
            headersEncoder.encodeHeaders(streamId, headers, headerBlock);

            Http2Flags flags =
                    new Http2Flags().endOfStream(endStream).priorityPresent(hasPriority).paddingPresent(padding > 0);

            // Read the first fragment (possibly everything).
            int nonFragmentBytes = padding + flags.getNumPriorityBytes();
            int maxFragmentLength =  _maxFrameSize - nonFragmentBytes;
            IByteBuffer fragment = headerBlock.ReadRetainedSlice(Math.Min(headerBlock.ReadableBytes, maxFragmentLength));

            // Set the end of headers flag for the first frame.
            flags.endOfHeaders(!headerBlock.IsReadable());

            int payloadLength = fragment.ReadableBytes + nonFragmentBytes;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.HEADERS_FRAME_HEADER_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.HEADERS, flags, streamId);
            writePaddingLength(buf, padding);

            if (hasPriority) {
                buf.WriteInt(exclusive ? (int) (0x80000000L | streamDependency) : streamDependency);

                // Adjust the weight so that it fits into a single byte on the wire.
                buf.WriteByte(weight - 1);
            }
            ctx.WriteAsync(buf, promiseAggregator.newPromise());

            // Write the first fragment.
            ctx.WriteAsync(fragment, promiseAggregator.newPromise());

            // Write out the padding, if any.
            if (paddingBytes(padding) > 0) {
                ctx.WriteAsync(ZERO_BUFFER.Slice(0, paddingBytes(padding)), promiseAggregator.newPromise());
            }

            if (!flags.endOfHeaders()) {
                writeContinuationFrames(ctx, streamId, headerBlock, padding, promiseAggregator);
            }
        } catch (Http2Exception e) {
            promiseAggregator.SetException(e);
        } catch (Exception t) {
            promiseAggregator.SetException(t);
            promiseAggregator.doneAllocatingPromises();
            throw;
        } finally {
            if (headerBlock != null) {
                headerBlock.Release();
            }
        }
        return promiseAggregator.doneAllocatingPromises();
    }

    /**
     * Writes as many continuation frames as needed until {@code padding} and {@code headerBlock} are consumed.
     */
    private Task writeContinuationFrames(IChannelHandlerContext ctx, int streamId,
            IByteBuffer headerBlock, int padding, SimpleChannelPromiseAggregator promiseAggregator) {
        Http2Flags flags = new Http2Flags().paddingPresent(padding > 0);
        int maxFragmentLength =  _maxFrameSize - padding;
        // TODO: same padding is applied to all frames, is this desired?
        if (maxFragmentLength <= 0) {
            return promiseAggregator.SetException(new ArgumentException(
                    "Padding [" + padding + "] is too large for max frame size [" +  _maxFrameSize + "]"));
        }

        if (headerBlock.IsReadable()) {
            // The frame header (and padding) only changes on the last frame, so allocate it once and re-use
            int fragmentReadableBytes = Math.Min(headerBlock.ReadableBytes, maxFragmentLength);
            int payloadLength = fragmentReadableBytes + padding;
            IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.CONTINUATION_FRAME_HEADER_LENGTH);
            Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.CONTINUATION, flags, streamId);
            writePaddingLength(buf, padding);

            do {
                fragmentReadableBytes = Math.Min(headerBlock.ReadableBytes, maxFragmentLength);
                IByteBuffer fragment = headerBlock.ReadRetainedSlice(fragmentReadableBytes);

                payloadLength = fragmentReadableBytes + padding;
                if (headerBlock.IsReadable()) {
                    ctx.WriteAsync(buf.Retain(), promiseAggregator.newPromise());
                } else {
                    // The frame header is different for the last frame, so re-allocate and release the old buffer
                    flags = flags.endOfHeaders(true);
                    buf.Release();
                    buf = ctx.Allocator.Buffer(Http2CodecUtil.CONTINUATION_FRAME_HEADER_LENGTH);
                    Http2CodecUtil.writeFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.CONTINUATION, flags, streamId);
                    writePaddingLength(buf, padding);
                    ctx.WriteAsync(buf, promiseAggregator.newPromise());
                }

                ctx.WriteAsync(fragment, promiseAggregator.newPromise());

                // Write out the padding, if any.
                if (paddingBytes(padding) > 0) {
                    ctx.WriteAsync(ZERO_BUFFER.Slice(0, paddingBytes(padding)), promiseAggregator.newPromise());
                }
            } while(headerBlock.IsReadable());
        }
        return promiseAggregator;
    }

    /**
     * Returns the number of padding bytes that should be appended to the end of a frame.
     */
    private static int paddingBytes(int padding) {
        // The padding parameter contains the 1 byte pad length field as well as the trailing padding bytes.
        // Subtract 1, so to only get the number of padding bytes that need to be appended to the end of a frame.
        return padding - 1;
    }

    private static void writePaddingLength(IByteBuffer buf, int padding) {
        if (padding > 0) {
            // It is assumed that the padding length has been bounds checked before this
            // Minus 1, as the pad length field is included in the padding parameter and is 1 byte wide.
            buf.WriteByte(padding - 1);
        }
    }

    private static void verifyStreamId(int streamId, string argumentName) {
        if (streamId <= 0) {
            throw new ArgumentException(argumentName + " must be > 0");
        }
    }

    private static void verifyStreamOrConnectionId(int streamId, string argumentName) {
        if (streamId < 0) {
            throw new ArgumentException(argumentName + " must be >= 0");
        }
    }

    private static void verifyWeight(short weight) {
        if (weight < Http2CodecUtil.MIN_WEIGHT || weight > Http2CodecUtil.MAX_WEIGHT) {
            throw new ArgumentException("Invalid weight: " + weight);
        }
    }

    private static void verifyErrorCode(long errorCode) {
        if (errorCode < 0 || errorCode > Http2CodecUtil.MAX_UNSIGNED_INT) {
            throw new ArgumentException("Invalid errorCode: " + errorCode);
        }
    }

    private static void verifyWindowSizeIncrement(int windowSizeIncrement) {
        if (windowSizeIncrement < 0) {
            throw new ArgumentException("WindowSizeIncrement must be >= 0");
        }
    }

    private static void verifyPingPayload(IByteBuffer data) {
        if (data == null || data.ReadableBytes != Http2CodecUtil.PING_FRAME_PAYLOAD_LENGTH) {
            throw new ArgumentException("Opaque data must be " + Http2CodecUtil.PING_FRAME_PAYLOAD_LENGTH + " bytes");
        }
    }
}
