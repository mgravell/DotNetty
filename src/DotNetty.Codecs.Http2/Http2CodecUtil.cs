// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;

    /**
     * Constants and utility method used for encoding/decoding HTTP2 frames.
     */
    public sealed class Http2CodecUtil
    {
        public const int CONNECTION_STREAM_ID = 0;

        public const int HTTP_UPGRADE_STREAM_ID = 1;

        public static readonly ICharSequence HTTP_UPGRADE_SETTINGS_HEADER = AsciiString.Cached("HTTP2-Settings");

        public static readonly ICharSequence HTTP_UPGRADE_PROTOCOL_NAME = AsciiString.Cached("h2c");

        public static readonly ICharSequence TLS_UPGRADE_PROTOCOL_NAME = AsciiString.Cached(ApplicationProtocolNames.HTTP_2);

        public const int PING_FRAME_PAYLOAD_LENGTH = 8;

        public const short MAX_UNSIGNED_BYTE = 0xff;

        /**
         * The maximum number of padding bytes. That is the 255 padding bytes appended to the end of a frame and the 1 byte
         * pad length field.
         */
        public const int MAX_PADDING = 256;

        public static readonly long MAX_UNSIGNED_INT = 0xffffffffL;

        public const int FRAME_HEADER_LENGTH = 9;

        public const int SETTING_ENTRY_LENGTH = 6;

        public const int PRIORITY_ENTRY_LENGTH = 5;

        public const int INT_FIELD_LENGTH = 4;

        public const short MAX_WEIGHT = 256;

        public const short MIN_WEIGHT = 1;

        static readonly IByteBuffer CONNECTION_PREFACE =
            unreleasableBuffer(directBuffer(24).WriteBytes(Encoding.UTF8.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")));

        static readonly IByteBuffer EMPTY_PING =
            unreleasableBuffer(directBuffer(PING_FRAME_PAYLOAD_LENGTH).WriteZero(PING_FRAME_PAYLOAD_LENGTH));

        static readonly int MAX_PADDING_LENGTH_LENGTH = 1;

        public static readonly int DATA_FRAME_HEADER_LENGTH = FRAME_HEADER_LENGTH + MAX_PADDING_LENGTH_LENGTH;

        public static readonly int HEADERS_FRAME_HEADER_LENGTH =
            FRAME_HEADER_LENGTH + MAX_PADDING_LENGTH_LENGTH + INT_FIELD_LENGTH + 1;

        public const int PRIORITY_FRAME_LENGTH = FRAME_HEADER_LENGTH + PRIORITY_ENTRY_LENGTH;

        public const int RST_STREAM_FRAME_LENGTH = FRAME_HEADER_LENGTH + INT_FIELD_LENGTH;

        public static readonly int PUSH_PROMISE_FRAME_HEADER_LENGTH =
            FRAME_HEADER_LENGTH + MAX_PADDING_LENGTH_LENGTH + INT_FIELD_LENGTH;

        public const int GO_AWAY_FRAME_HEADER_LENGTH = FRAME_HEADER_LENGTH + 2 * INT_FIELD_LENGTH;

        public const int WINDOW_UPDATE_FRAME_LENGTH = FRAME_HEADER_LENGTH + INT_FIELD_LENGTH;

        public static readonly int CONTINUATION_FRAME_HEADER_LENGTH = FRAME_HEADER_LENGTH + MAX_PADDING_LENGTH_LENGTH;

        public const char SETTINGS_HEADER_TABLE_SIZE = (char)1;

        public const char SETTINGS_ENABLE_PUSH = (char)2;

        public const char SETTINGS_MAX_CONCURRENT_STREAMS = (char)3;

        public const char SETTINGS_INITIAL_WINDOW_SIZE = (char)4;

        public const char SETTINGS_MAX_FRAME_SIZE = (char)5;

        public const char SETTINGS_MAX_HEADER_LIST_SIZE = (char)6;

        public const int NUM_STANDARD_SETTINGS = 6;

        public static readonly long MAX_HEADER_TABLE_SIZE = MAX_UNSIGNED_INT;

        public static readonly long MAX_CONCURRENT_STREAMS = MAX_UNSIGNED_INT;

        public const int MAX_INITIAL_WINDOW_SIZE = int.MaxValue;

        public const int MAX_FRAME_SIZE_LOWER_BOUND = 0x4000;

        public const int MAX_FRAME_SIZE_UPPER_BOUND = 0xffffff;

        public static readonly long MAX_HEADER_LIST_SIZE = MAX_UNSIGNED_INT;

        public static readonly long MIN_HEADER_TABLE_SIZE = 0;

        public static readonly long MIN_CONCURRENT_STREAMS = 0;

        public const int MIN_INITIAL_WINDOW_SIZE = 0;

        public static readonly long MIN_HEADER_LIST_SIZE = 0;

        public const int DEFAULT_WINDOW_SIZE = 65535;

        public const short DEFAULT_PRIORITY_WEIGHT = 16;

        public const int DEFAULT_HEADER_TABLE_SIZE = 4096;

        /**
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">The initial value of this setting is unlimited</a>.
         * However in practice we don't want to allow our peers to use unlimited memory by default. So we take advantage
         * of the <q>For any given request, a lower limit than what is advertised MAY be enforced.</q> loophole.
         */
        public static readonly long DEFAULT_HEADER_LIST_SIZE = 8192;

        public const int DEFAULT_MAX_FRAME_SIZE = MAX_FRAME_SIZE_LOWER_BOUND;

        /**
         * The assumed minimum value for {@code SETTINGS_MAX_CONCURRENT_STREAMS} as
         * recommended by the <a herf="https://tools.ietf.org/html/rfc7540#section-6.5.2">HTTP/2 spec</a>.
         */
        public const int SMALLEST_MAX_CONCURRENT_STREAMS = 100;

        public static readonly int DEFAULT_MAX_RESERVED_STREAMS = SMALLEST_MAX_CONCURRENT_STREAMS;

        public static readonly int DEFAULT_MIN_ALLOCATION_CHUNK = 1024;

        public static readonly int DEFAULT_INITIAL_HUFFMAN_DECODE_CAPACITY = 32;
        
        /**
         * Calculate the threshold in bytes which should trigger a {@code GO_AWAY} if a set of headers exceeds this amount.
         * @param maxHeaderListSize
         *      <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a> for the local
         *      endpoint.
         * @return the threshold in bytes which should trigger a {@code GO_AWAY} if a set of headers exceeds this amount.
         */
        public static long calculateMaxHeaderListSizeGoAway(long maxHeaderListSize) {
            // This is equivalent to `maxHeaderListSize * 1.25` but we avoid floating point multiplication.
            return maxHeaderListSize + (maxHeaderListSize >> 2);
        }
        

        /**
     * Return a unreleasable view on the given {@link ByteBuf} which will just ignore release and retain calls.
     */
        public static IByteBuffer unreleasableBuffer(IByteBuffer buf)
        {
            return new UnreleasableByteBuffer(buf);
        }

        /**
         * Creates a new big-endian direct buffer with the specified {@code capacity}, which
         * expands its capacity boundlessly on demand.  The new buffer's {@code readerIndex} and
         * {@code writerIndex} are {@code 0}.
         */
        public static IByteBuffer directBuffer(int initialCapacity)
        {
            return Unpooled.DirectBuffer(initialCapacity);
        }

        /**
         * Indicates whether or not the given value for max frame size falls within the valid range.
         */
        public static bool isMaxFrameSizeValid(long maxFrameSize)
        {
            return maxFrameSize >= MAX_FRAME_SIZE_LOWER_BOUND && maxFrameSize <= MAX_FRAME_SIZE_UPPER_BOUND;
        }

        public static void verifyPadding(int padding)
        {
            if (padding < 0 || padding > MAX_PADDING)
            {
                throw new ArgumentException($"Invalid padding '{padding}'. Padding must be between 0 and {MAX_PADDING} (inclusive).");
            }
        }

        /**
         * Results in a RST_STREAM being sent for {@code streamId} due to violating
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
         * @param streamId The stream ID that was being processed when the exceptional condition occurred.
         * @param maxHeaderListSize The max allowed size for a list of headers in bytes which was exceeded.
         * @param onDecode {@code true} if the exception was encountered during decoder. {@code false} for encode.
         * @a stream error.
         */
        public static void headerListSizeExceeded(int streamId, long maxHeaderListSize, bool onDecode)
        {
            throw Http2Exception.headerListSizeError(streamId, Http2Error.PROTOCOL_ERROR, onDecode, "Header size exceeded max " + "allowed size ({0})", maxHeaderListSize);
        }

        /**
         * Results in a GO_AWAY being sent due to violating
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a> in an unrecoverable
         * manner.
         * @param maxHeaderListSize The max allowed size for a list of headers in bytes which was exceeded.
         * @a connection error.
         */
        public static void headerListSizeExceeded(long maxHeaderListSize)
        {
            throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Header size exceeded max " + "allowed size ({0})", maxHeaderListSize);
        }

        /**
         * Calculate the amount of bytes that can be sent by {@code state}. The lower bound is {@code 0}.
         */
        public static int streamableBytes(StreamByteDistributorContext state)
        {
            return Math.Max(0, (int)Math.Min(state.pendingBytes(), state.windowSize()));
        }

        /**
         * Reads a big-endian (31-bit) integer from the buffer.
         */
        public static int readUnsignedInt(IByteBuffer buf)
        {
            return buf.ReadInt() & 0x7fffffff;
        }

        internal static void writeFrameHeaderInternal(IByteBuffer output, int payloadLength, Http2FrameTypes type, Http2Flags flags, int streamId)
        {
            output.WriteMedium(payloadLength);
            output.WriteByte((byte)type);
            output.WriteByte(flags.value());
            output.WriteInt(streamId);
        }

        Http2CodecUtil()
        {
        }
    }
}