// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class DefaultHttp2FrameReader : Http2FrameReader, Http2FrameSizePolicy, Http2FrameReaderConfiguration
    {
        delegate void FragmentProcessor(bool endOfHeaders, IByteBuffer fragment, HeadersBlockBuilder headerBlockBuilder, Http2FrameListener listener);

        readonly Http2HeadersDecoder headersDecoder;

        /**
     * {@code true} = reading headers, {@code false} = reading payload.
     */
        bool readingHeaders = true;

        /**
     * Once set to {@code true} the value will never change. This is set to {@code true} if an unrecoverable error which
     * renders the connection unusable.
     */
        bool readError;
        Http2FrameTypes frameType;
        int streamId;
        Http2Flags flags;
        int payloadLength;
        HeadersContinuation headersContinuation;
        int _maxFrameSize;

        /**
     * Create a new instance.
     * <p>
     * Header names will be validated.
     */
        public DefaultHttp2FrameReader()
            : this(true)
        {
        }

        /**
         * Create a new instance.
         * @param validateHeaders {@code true} to validate headers. {@code false} to not validate headers.
         * @see DefaultHttp2HeadersDecoder(bool)
         */
        public DefaultHttp2FrameReader(bool validateHeaders)
            : this(new DefaultHttp2HeadersDecoder(validateHeaders))
        {
        }

        public DefaultHttp2FrameReader(Http2HeadersDecoder headersDecoder)
        {
            this.headersDecoder = headersDecoder;
            this._maxFrameSize = Http2CodecUtil.DEFAULT_MAX_FRAME_SIZE;
        }

        public Http2HeadersDecoderConfiguration headersConfiguration()
        {
            return this.headersDecoder.configuration();
        }

        public Http2FrameReaderConfiguration configuration()
        {
            return this;
        }

        public Http2FrameSizePolicy frameSizePolicy()
        {
            return this;
        }

        public void maxFrameSize(int max)
        {
            if (!Http2CodecUtil.isMaxFrameSizeValid(max))
            {
                throw Http2Exception.streamError(
                    this.streamId,
                    Http2Error.FRAME_SIZE_ERROR,
                    "Invalid MAX_FRAME_SIZE specified in sent settings: {0}",
                    max);
            }

            this._maxFrameSize = max;
        }

        public int maxFrameSize()
        {
            return this._maxFrameSize;
        }

        public void Dispose()
        {
            this.closeHeadersContinuation();
        }

        void closeHeadersContinuation()
        {
            if (this.headersContinuation != null)
            {
                this.headersContinuation.close();
                this.headersContinuation = null;
            }
        }

        public void readFrame(IChannelHandlerContext ctx, IByteBuffer input, Http2FrameListener listener)
        {
            if (this.readError)
            {
                input.SkipBytes(input.ReadableBytes);
                return;
            }

            try
            {
                do
                {
                    if (this.readingHeaders)
                    {
                        this.processHeaderState(input);
                        if (this.readingHeaders)
                        {
                            // Wait until the entire header has arrived.
                            return;
                        }
                    }

                    // The header is complete, fall into the next case to process the payload.
                    // This is to ensure the proper handling of zero-length payloads. In this
                    // case, we don't want to loop around because there may be no more data
                    // available, causing us to exit the loop. Instead, we just want to perform
                    // the first pass at payload processing now.
                    this.processPayloadState(ctx, input, listener);
                    if (!this.readingHeaders)
                    {
                        // Wait until the entire payload has arrived.
                        return;
                    }
                }
                while (input.IsReadable());
            }
            catch (Http2Exception e)
            {
                this.readError = !Http2Exception.isStreamError(e);
                throw;
            }
            catch
            {
                this.readError = true;
                throw;
            }
        }

        void processHeaderState(IByteBuffer input)
        {
            if (input.ReadableBytes < Http2CodecUtil.FRAME_HEADER_LENGTH)
            {
                // Wait until the entire frame header has been read.
                return;
            }

            // Read the header and prepare the unmarshaller to read the frame.
            this.payloadLength = input.ReadUnsignedMedium();
            if (this.payloadLength > this._maxFrameSize)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Frame length: {0} exceeds maximum: {0}", this.payloadLength, this._maxFrameSize);
            }

            this.frameType = (Http2FrameTypes)input.ReadByte();
            this.flags = new Http2Flags(input.ReadByte());
            this.streamId = Http2CodecUtil.readUnsignedInt(input);

            // We have consumed the data, next time we read we will be expecting to read the frame payload.
            this.readingHeaders = false;

            switch (this.frameType)
            {
                case Http2FrameTypes.DATA:
                    this.verifyDataFrame();
                    break;
                case Http2FrameTypes.HEADERS:
                    this.verifyHeadersFrame();
                    break;
                case Http2FrameTypes.PRIORITY:
                    this.verifyPriorityFrame();
                    break;
                case Http2FrameTypes.RST_STREAM:
                    this.verifyRstStreamFrame();
                    break;
                case Http2FrameTypes.SETTINGS:
                    this.verifySettingsFrame();
                    break;
                case Http2FrameTypes.PUSH_PROMISE:
                    this.verifyPushPromiseFrame();
                    break;
                case Http2FrameTypes.PING:
                    this.verifyPingFrame();
                    break;
                case Http2FrameTypes.GO_AWAY:
                    this.verifyGoAwayFrame();
                    break;
                case Http2FrameTypes.WINDOW_UPDATE:
                    this.verifyWindowUpdateFrame();
                    break;
                case Http2FrameTypes.CONTINUATION:
                    this.verifyContinuationFrame();
                    break;
                default:
                    // Unknown frame type, could be an extension.
                    this.verifyUnknownFrame();
                    break;
            }
        }

        void processPayloadState(IChannelHandlerContext ctx, IByteBuffer input, Http2FrameListener listener)
        {
            if (input.ReadableBytes < this.payloadLength)
            {
                // Wait until the entire payload has been read.
                return;
            }

            // Get a view of the buffer for the size of the payload.
            IByteBuffer payload = input.ReadSlice(this.payloadLength);

            // We have consumed the data, next time we read we will be expecting to read a frame header.
            this.readingHeaders = true;

            // Read the payload and fire the frame event to the listener.
            switch (this.frameType)
            {
                case Http2FrameTypes.DATA:
                    this.readDataFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.HEADERS:
                    this.readHeadersFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.PRIORITY:
                    this.readPriorityFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.RST_STREAM:
                    this.readRstStreamFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.SETTINGS:
                    this.readSettingsFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.PUSH_PROMISE:
                    this.readPushPromiseFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.PING:
                    this.readPingFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.GO_AWAY:
                    readGoAwayFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.WINDOW_UPDATE:
                    this.readWindowUpdateFrame(ctx, payload, listener);
                    break;
                case Http2FrameTypes.CONTINUATION:
                    this.readContinuationFrame(payload, listener);
                    break;
                default:
                    this.readUnknownFrame(ctx, payload, listener);
                    break;
            }
        }

        void verifyDataFrame()
        {
            this.verifyAssociatedWithAStream();
            this.verifyNotProcessingHeaders();
            this.verifyPayloadLength(this.payloadLength);

            if (this.payloadLength < this.flags.getPaddingPresenceFieldLength())
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.FRAME_SIZE_ERROR, "Frame length {0} too small.", this.payloadLength);
            }
        }

        void verifyHeadersFrame()
        {
            this.verifyAssociatedWithAStream();
            this.verifyNotProcessingHeaders();
            this.verifyPayloadLength(this.payloadLength);

            int requiredLength = this.flags.getPaddingPresenceFieldLength() + this.flags.getNumPriorityBytes();
            if (this.payloadLength < requiredLength)
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.FRAME_SIZE_ERROR, "Frame length too small." + this.payloadLength);
            }
        }

        void verifyPriorityFrame()
        {
            this.verifyAssociatedWithAStream();
            this.verifyNotProcessingHeaders();

            if (this.payloadLength != Http2CodecUtil.PRIORITY_ENTRY_LENGTH)
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.FRAME_SIZE_ERROR, "Invalid frame length {0}.", this.payloadLength);
            }
        }

        void verifyRstStreamFrame()
        {
            this.verifyAssociatedWithAStream();
            this.verifyNotProcessingHeaders();

            if (this.payloadLength != Http2CodecUtil.INT_FIELD_LENGTH)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Invalid frame length {0}.", this.payloadLength);
            }
        }

        void verifySettingsFrame()
        {
            this.verifyNotProcessingHeaders();
            this.verifyPayloadLength(this.payloadLength);
            if (this.streamId != 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "A stream ID must be zero.");
            }

            if (this.flags.ack() && this.payloadLength > 0)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Ack settings frame must have an empty payload.");
            }

            if (this.payloadLength % Http2CodecUtil.SETTING_ENTRY_LENGTH > 0)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Frame length {0} invalid.", this.payloadLength);
            }
        }

        void verifyPushPromiseFrame()
        {
            this.verifyNotProcessingHeaders();
            this.verifyPayloadLength(this.payloadLength);

            // Subtract the length of the promised stream ID field, to determine the length of the
            // rest of the payload (header block fragment + payload).
            int minLength = this.flags.getPaddingPresenceFieldLength() + Http2CodecUtil.INT_FIELD_LENGTH;
            if (this.payloadLength < minLength)
            {
                throw Http2Exception.streamError(
                    this.streamId,
                    Http2Error.FRAME_SIZE_ERROR,
                    "Frame length {0} too small.",
                    this.payloadLength);
            }
        }

        void verifyPingFrame()
        {
            this.verifyNotProcessingHeaders();
            if (this.streamId != 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "A stream ID must be zero.");
            }

            if (this.payloadLength != Http2CodecUtil.PING_FRAME_PAYLOAD_LENGTH)
            {
                throw Http2Exception.connectionError(
                    Http2Error.FRAME_SIZE_ERROR,
                    "Frame length {0} incorrect size for ping.",
                    this.payloadLength);
            }
        }

        void verifyGoAwayFrame()
        {
            this.verifyNotProcessingHeaders();
            this.verifyPayloadLength(this.payloadLength);

            if (this.streamId != 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "A stream ID must be zero.");
            }

            if (this.payloadLength < 8)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Frame length {0} too small.", this.payloadLength);
            }
        }

        void verifyWindowUpdateFrame()
        {
            this.verifyNotProcessingHeaders();
            verifyStreamOrConnectionId(this.streamId, "Stream ID");

            if (this.payloadLength != Http2CodecUtil.INT_FIELD_LENGTH)
            {
                throw Http2Exception.connectionError(Http2Error.FRAME_SIZE_ERROR, "Invalid frame length {0}.", this.payloadLength);
            }
        }

        void verifyContinuationFrame()
        {
            this.verifyAssociatedWithAStream();
            this.verifyPayloadLength(this.payloadLength);

            if (this.headersContinuation == null)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Received {0} frame but not currently processing headers.", this.frameType);
            }

            if (this.streamId != this.headersContinuation.getStreamId())
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Continuation stream ID does not match pending headers. "+ "Expected {0}, but received {1}.", this.headersContinuation.getStreamId(), this.streamId);
            }

            if (this.payloadLength < this.flags.getPaddingPresenceFieldLength())
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.FRAME_SIZE_ERROR, "Frame length {0} too small for padding.", this.payloadLength);
            }
        }

        void verifyUnknownFrame()
        {
            this.verifyNotProcessingHeaders();
        }

        void readDataFrame(
            IChannelHandlerContext ctx,
            IByteBuffer payload,
            Http2FrameListener listener)
        {
            int padding = this.readPadding(payload);
            this.verifyPadding(padding);

            // Determine how much data there is to read by removing the trailing
            // padding.
            int dataLength = lengthWithoutTrailingPadding(payload.ReadableBytes, padding);

            IByteBuffer data = payload.ReadSlice(dataLength);
            listener.onDataRead(ctx, this.streamId, data, padding, this.flags.endOfStream());
            payload.SkipBytes(payload.ReadableBytes);
        }

        void readHeadersFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            int headersStreamId = this.streamId;
            Http2Flags headersFlags = this.flags;
            int padding = this.readPadding(payload);
            this.verifyPadding(padding);

            IByteBuffer fragment;

            // The callback that is invoked is different depending on whether priority information
            // is present in the headers frame.
            if (this.flags.priorityPresent())
            {
                long word1 = payload.ReadUnsignedInt();
                bool exclusive = (word1 & 0x80000000L) != 0;
                int streamDependency = (int)(word1 & 0x7FFFFFFFL);
                if (streamDependency == this.streamId)
                {
                    throw Http2Exception.streamError(this.streamId, Http2Error.PROTOCOL_ERROR, "A stream cannot depend on itself.");
                }

                short weight = (short)(payload.ReadByte() + 1);
                fragment = payload.ReadSlice(lengthWithoutTrailingPadding(payload.ReadableBytes, padding));

                // Create a handler that invokes the listener when the header block is complete.
                this.headersContinuation = new HeadersContinuation(headersStreamId, this, ProcessWithPriority);

                // Process the initial fragment, invoking the listener's callback if end of headers.
                this.headersContinuation.processFragment(this.flags.endOfHeaders(), fragment, listener);
                this.resetHeadersContinuationIfEnd(this.flags.endOfHeaders());
                return;

                void ProcessWithPriority(bool endOfHeaders, IByteBuffer buffer, HeadersBlockBuilder headerBlockBuilder, Http2FrameListener lsnr)
                {
                    headerBlockBuilder.addFragment(buffer, ctx.Allocator, endOfHeaders);
                    if (endOfHeaders)
                    {
                        lsnr.onHeadersRead(ctx, this.streamId, headerBlockBuilder.headers(), streamDependency, weight, exclusive, padding, headersFlags.endOfStream());
                    }
                }
            }

            // The priority fields are not present in the frame. Prepare a continuation that invokes
            // the listener callback without priority information.
            this.headersContinuation = new HeadersContinuation(headersStreamId, this, ProcessWithoutPriority);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            fragment = payload.ReadSlice(lengthWithoutTrailingPadding(payload.ReadableBytes, padding));
            this.headersContinuation.processFragment(this.flags.endOfHeaders(), fragment, listener);
            this.resetHeadersContinuationIfEnd(this.flags.endOfHeaders());

            void ProcessWithoutPriority(bool endOfHeaders, IByteBuffer buffer, HeadersBlockBuilder headerBlockBuilder, Http2FrameListener lsnr)
            {
                headerBlockBuilder.addFragment(buffer, ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    lsnr.onHeadersRead(ctx, headersStreamId, headerBlockBuilder.headers(), padding, headersFlags.endOfStream());
                }
            }
        }

        void resetHeadersContinuationIfEnd(bool endOfHeaders)
        {
            if (endOfHeaders)
            {
                this.closeHeadersContinuation();
            }
        }

        void readPriorityFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            long word1 = payload.ReadUnsignedInt();
            bool exclusive = (word1 & 0x80000000L) != 0;
            int streamDependency = (int)(word1 & 0x7FFFFFFFL);
            if (streamDependency == this.streamId)
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.PROTOCOL_ERROR, "A stream cannot depend on itself.");
            }

            short weight = (short)(payload.ReadByte() + 1);
            listener.onPriorityRead(ctx, this.streamId, streamDependency, weight, exclusive);
        }

        void readRstStreamFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            long errorCode = payload.ReadUnsignedInt();
            listener.onRstStreamRead(ctx, this.streamId, errorCode);
        }

        void readSettingsFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            if (this.flags.ack())
            {
                listener.onSettingsAckRead(ctx);
            }
            else
            {
                int numSettings = this.payloadLength / Http2CodecUtil.SETTING_ENTRY_LENGTH;
                Http2Settings settings = new Http2Settings();
                for (int index = 0; index < numSettings; ++index)
                {
                    char id = (char)payload.ReadUnsignedShort();
                    long value = payload.ReadUnsignedInt();
                    try
                    {
                        settings.put(id, value);
                    }
                    catch (ArgumentException e)
                    {
                        switch (id)
                        {
                            case Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE:
                                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, e, e.Message);
                            case Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE:
                                throw Http2Exception.connectionError(Http2Error.FLOW_CONTROL_ERROR, e, e.Message);
                            default:
                                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, e, e.Message);
                        }
                    }
                }

                listener.onSettingsRead(ctx, settings);
            }
        }

        void readPushPromiseFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            int pushPromiseStreamId = this.streamId;
            int padding = this.readPadding(payload);
            this.verifyPadding(padding);
            int promisedStreamId = Http2CodecUtil.readUnsignedInt(payload);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            IByteBuffer fragment = payload.ReadSlice(lengthWithoutTrailingPadding(payload.ReadableBytes, padding));

            // Create a handler that invokes the listener when the header block is complete.
            this.headersContinuation = new HeadersContinuation(pushPromiseStreamId, this, ProcessPushPromise);
            this.headersContinuation.processFragment(this.flags.endOfHeaders(), fragment, listener);
            this.resetHeadersContinuationIfEnd(this.flags.endOfHeaders());

            void ProcessPushPromise(bool endOfHeaders, IByteBuffer buffer, HeadersBlockBuilder headerBlockBuilder, Http2FrameListener lsnr)
            {
                headerBlockBuilder.addFragment(fragment, ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    lsnr.onPushPromiseRead(ctx, pushPromiseStreamId, promisedStreamId, headerBlockBuilder.headers(), padding);
                }
            }
        }

        void readPingFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            IByteBuffer data = payload.ReadSlice(payload.ReadableBytes);
            if (this.flags.ack())
            {
                listener.onPingAckRead(ctx, data);
            }
            else
            {
                listener.onPingRead(ctx, data);
            }
        }

        static void readGoAwayFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            int lastStreamId = Http2CodecUtil.readUnsignedInt(payload);
            long errorCode = payload.ReadUnsignedInt();
            IByteBuffer debugData = payload.ReadSlice(payload.ReadableBytes);
            listener.onGoAwayRead(ctx, lastStreamId, errorCode, debugData);
        }

        void readWindowUpdateFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            int windowSizeIncrement = Http2CodecUtil.readUnsignedInt(payload);
            if (windowSizeIncrement == 0)
            {
                throw Http2Exception.streamError(this.streamId, Http2Error.PROTOCOL_ERROR, "Received WINDOW_UPDATE with delta 0 for stream: {0}", this.streamId);
            }

            listener.onWindowUpdateRead(ctx, this.streamId, windowSizeIncrement);
        }

        void readContinuationFrame(IByteBuffer payload, Http2FrameListener listener)
        {
            // Process the initial fragment, invoking the listener's callback if end of headers.
            IByteBuffer continuationFragment = payload.ReadSlice(payload.ReadableBytes);
            this.headersContinuation.processFragment(this.flags.endOfHeaders(), continuationFragment, listener);
            this.resetHeadersContinuationIfEnd(this.flags.endOfHeaders());
        }

        void readUnknownFrame(IChannelHandlerContext ctx, IByteBuffer payload, Http2FrameListener listener)
        {
            payload = payload.ReadSlice(payload.ReadableBytes);
            listener.onUnknownFrame(ctx, this.frameType, this.streamId, this.flags, payload);
        }

        /**
         * If padding is present in the payload, reads the next byte as padding. The padding also includes the one byte
         * width of the pad length field. Otherwise, returns zero.
         */
        int readPadding(IByteBuffer payload)
        {
            if (!this.flags.paddingPresent())
            {
                return 0;
            }

            return payload.ReadByte() + 1;
        }

        void verifyPadding(int padding)
        {
            int len = lengthWithoutTrailingPadding(this.payloadLength, padding);
            if (len < 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Frame payload too small for padding.");
            }
        }

        /**
         * The padding parameter consists of the 1 byte pad length field and the trailing padding bytes. This method
         * returns the number of readable bytes without the trailing padding.
         */
        static int lengthWithoutTrailingPadding(int readableBytes, int padding)
        {
            return padding == 0 ? readableBytes : readableBytes - (padding - 1);
        }

        /**
         * Base class for processing of HEADERS and PUSH_PROMISE header blocks that potentially span
         * multiple frames. The implementation of this interface will perform the final callback to the
         * {@link Http2FrameListener} once the end of headers is reached.
         */
        class HeadersContinuation
        {
            readonly int streamId;
            readonly HeadersBlockBuilder builder;
            readonly FragmentProcessor processor;

            public HeadersContinuation(int streamId, DefaultHttp2FrameReader reader, FragmentProcessor processor)
            {
                this.streamId = streamId;
                this.processor = processor;
                this.builder = new HeadersBlockBuilder(reader);
            }

            /**
             * Returns the stream for which headers are currently being processed.
             */
            internal int getStreamId() => this.streamId;

            /**
             * Processes the next fragment for the current header block.
             *
             * @param endOfHeaders whether the fragment is the last in the header block.
             * @param fragment the fragment of the header block to be added.
             * @param listener the listener to be notified if the header block is completed.
             */
            internal void processFragment(bool endOfHeaders, IByteBuffer fragment, Http2FrameListener listener) => this.processor(endOfHeaders, fragment, this.builder, listener);

            /**
             * Free any allocated resources.
             */
            internal void close()
            {
                this.builder.close();
            }
        }

        /**
         * Utility class to help with construction of the headers block that may potentially span
         * multiple frames.
         */
        class HeadersBlockBuilder
        {
            readonly DefaultHttp2FrameReader reader;
            IByteBuffer headerBlock;

            public HeadersBlockBuilder(DefaultHttp2FrameReader reader)
            {
                this.reader = reader;
            }

            /**
             * The local header size maximum has been exceeded while accumulating bytes.
             * @throws Http2Exception A connection error indicating too much data has been received.
             */
            void headerSizeExceeded()
            {
                this.close();
                Http2CodecUtil.headerListSizeExceeded(this.reader.headersDecoder.configuration().maxHeaderListSizeGoAway());
            }

            /**
             * Adds a fragment to the block.
             *
             * @param fragment the fragment of the headers block to be added.
             * @param alloc allocator for new blocks if needed.
             * @param endOfHeaders flag indicating whether the current frame is the end of the headers.
             *            This is used for an optimization for when the first fragment is the full
             *            block. In that case, the buffer is used directly without copying.
             */
            internal void addFragment(IByteBuffer fragment, IByteBufferAllocator alloc, bool endOfHeaders)
            {
                if (this.headerBlock == null)
                {
                    if (fragment.ReadableBytes > this.reader.headersDecoder.configuration().maxHeaderListSizeGoAway())
                    {
                        this.headerSizeExceeded();
                    }

                    if (endOfHeaders)
                    {
                        // Optimization - don't bother copying, just use the buffer as-is. Need
                        // to retain since we release when the header block is built.
                        this.headerBlock = (IByteBuffer)fragment.Retain();
                    }
                    else
                    {
                        this.headerBlock = alloc.Buffer(fragment.ReadableBytes);
                        this.headerBlock.WriteBytes(fragment);
                    }

                    return;
                }

                if (this.reader.headersDecoder.configuration().maxHeaderListSizeGoAway() - fragment.ReadableBytes < this.headerBlock.ReadableBytes)
                {
                    this.headerSizeExceeded();
                }

                if (this.headerBlock.IsWritable(fragment.ReadableBytes))
                {
                    // The buffer can hold the requested bytes, just write it directly.
                    this.headerBlock.WriteBytes(fragment);
                }
                else
                {
                    // Allocate a new buffer that is big enough to hold the entire header block so far.
                    IByteBuffer buf = alloc.Buffer(this.headerBlock.ReadableBytes + fragment.ReadableBytes);
                    buf.WriteBytes(this.headerBlock);
                    buf.WriteBytes(fragment);
                    this.headerBlock.Release();
                    this.headerBlock = buf;
                }
            }

            /**
             * Builds the headers from the completed headers block. After this is called, this builder
             * should not be called again.
             */
            internal Http2Headers headers()
            {
                try
                {
                    return this.reader.headersDecoder.decodeHeaders(this.reader.streamId, this.headerBlock);
                }
                finally
                {
                    this.close();
                }
            }

            /**
             * Closes this builder and frees any resources.
             */
            internal void close()
            {
                if (this.headerBlock != null)
                {
                    this.headerBlock.Release();
                    this.headerBlock = null;
                }

                // Clear the member variable pointing at this instance.
                this.reader.headersContinuation = null;
            }
        }

        /**
         * Verify that current state is not processing on header block
         * @throws Http2Exception thrown if {@link #headersContinuation} is not null
         */
        void verifyNotProcessingHeaders()
        {
            if (this.headersContinuation != null)
            {
                throw Http2Exception.connectionError(
                    Http2Error.PROTOCOL_ERROR,
                    "Received frame of type %s while processing headers on stream {0}.",
                    this.frameType,
                    this.headersContinuation.getStreamId());
            }
        }

        void verifyPayloadLength(int payloadLength)
        {
            if (payloadLength > this._maxFrameSize)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Total payload length {0} exceeds max frame length.", payloadLength);
            }
        }

        void verifyAssociatedWithAStream()
        {
            if (this.streamId == 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Frame of type %s must be associated with a stream.", this.frameType);
            }
        }

        static void verifyStreamOrConnectionId(int streamId, string argumentName)
        {
            if (streamId < 0)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "%s must be >= 0", argumentName);
            }
        }
    }
}