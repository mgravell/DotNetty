// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackDecoder
    {
        static readonly Http2Exception DECODE_ULE_128_DECOMPRESSION_EXCEPTION =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - decompression failure");

        static readonly Http2Exception DECODE_ULE_128_TO_LONG_DECOMPRESSION_EXCEPTION =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - long overflow");

        static readonly Http2Exception DECODE_ULE_128_TO_INT_DECOMPRESSION_EXCEPTION =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - int overflow");

        static readonly Http2Exception DECODE_ILLEGAL_INDEX_VALUE =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - illegal index value");

        static readonly Http2Exception INDEX_HEADER_ILLEGAL_INDEX_VALUE =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - illegal index value");

        static readonly Http2Exception READ_NAME_ILLEGAL_INDEX_VALUE =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - illegal index value");

        static readonly Http2Exception INVALID_MAX_DYNAMIC_TABLE_SIZE =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - invalid max dynamic table size");

        static readonly Http2Exception MAX_DYNAMIC_TABLE_SIZE_CHANGE_REQUIRED =
            Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - max dynamic table size change required");

        const byte READ_HEADER_REPRESENTATION = 0;

        const byte READ_MAX_DYNAMIC_TABLE_SIZE = 1;

        const byte READ_INDEXED_HEADER = 2;

        const byte READ_INDEXED_HEADER_NAME = 3;

        const byte READ_LITERAL_HEADER_NAME_LENGTH_PREFIX = 4;

        const byte READ_LITERAL_HEADER_NAME_LENGTH = 5;

        const byte READ_LITERAL_HEADER_NAME = 6;

        const byte READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX = 7;

        const byte READ_LITERAL_HEADER_VALUE_LENGTH = 8;

        const byte READ_LITERAL_HEADER_VALUE = 9;

        readonly HpackDynamicTable hpackDynamicTable;

        readonly HpackHuffmanDecoder hpackHuffmanDecoder;

        long maxHeaderListSizeGoAway;
        long maxHeaderListSize;
        long maxDynamicTableSize;
        long encoderMaxDynamicTableSize;
        bool maxDynamicTableSizeChangeRequired;

        /**
         * Create a new instance.
         * @param maxHeaderListSize This is the only setting that can be configured before notifying the peer.
         *  This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_Http2CodecUtil.MAX_HEADER_LIST_SIZE</a>
         *  allows a lower than advertised limit from being enforced, and the default limit is unlimited
         *  (which is dangerous).
         * @param initialHuffmanDecodeCapacity Size of an intermediate buffer used during huffman decode.
         */
        internal HpackDecoder(long maxHeaderListSize, int initialHuffmanDecodeCapacity)
            : this(maxHeaderListSize, initialHuffmanDecodeCapacity, Http2CodecUtil.DEFAULT_HEADER_TABLE_SIZE)
        {

        }

        /**
         * Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
         * for testing but violate the RFC if used outside the scope of testing.
         */
        HpackDecoder(long maxHeaderListSize, int initialHuffmanDecodeCapacity, int maxHeaderTableSize)
        {
            Contract.Requires(maxHeaderListSize > 0);
            this.maxHeaderListSize = maxHeaderListSize;
            this.maxHeaderListSizeGoAway = Http2CodecUtil.calculateMaxHeaderListSizeGoAway(maxHeaderListSize);

            this.maxDynamicTableSize = this.encoderMaxDynamicTableSize = maxHeaderTableSize;
            this.maxDynamicTableSizeChangeRequired = false;
            hpackDynamicTable = new HpackDynamicTable(maxHeaderTableSize);
            hpackHuffmanDecoder = new HpackHuffmanDecoder(initialHuffmanDecodeCapacity);
        }

        /**
         * Decode the header block into header fields.
         * <p>
         * This method assumes the entire header block is contained in {@code in}.
         */
        public void decode(int streamId, IByteBuffer input, Http2Headers headers)
        {

            int index = 0;
            long headersLength = 0;
            int nameLength = 0;
            int valueLength = 0;
            byte state = READ_HEADER_REPRESENTATION;
            bool huffmanEncoded = false;
            ICharSequence name = null;

            HpackUtil.IndexType indexType = HpackUtil.IndexType.NONE;
            while (input.IsReadable())
            {
                switch (state)
                {
                    case READ_HEADER_REPRESENTATION:
                        byte b = input.ReadByte();
                        if (maxDynamicTableSizeChangeRequired && (b & 0xE0) != 0x20)
                        {
                            // HpackEncoder MUST signal maximum dynamic table size change
                            throw MAX_DYNAMIC_TABLE_SIZE_CHANGE_REQUIRED;
                        }

                        if (b < 0)
                        {
                            // Indexed Header Field
                            index = b & 0x7F;
                            switch (index)
                            {
                                case 0:
                                    throw DECODE_ILLEGAL_INDEX_VALUE;
                                case 0x7F:
                                    state = READ_INDEXED_HEADER;
                                    break;
                                default:
                                    headersLength = indexHeader(index, headers, headersLength);
                                    break;
                            }
                        }
                        else if ((b & 0x40) == 0x40)
                        {
                            // Literal Header Field with Incremental Indexing
                            indexType = HpackUtil.IndexType.INCREMENTAL;
                            index = b & 0x3F;
                            switch (index)
                            {
                                case 0:
                                    state = READ_LITERAL_HEADER_NAME_LENGTH_PREFIX;
                                    break;
                                case 0x3F:
                                    state = READ_INDEXED_HEADER_NAME;
                                    break;
                                default:
                                    // Index was stored as the prefix
                                    name = readName(index);
                                    nameLength = name.Count;
                                    state = READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
                                    break;
                            }
                        }
                        else if ((b & 0x20) == 0x20)
                        {
                            // Dynamic Table Size Update
                            index = b & 0x1F;
                            if (index == 0x1F)
                            {
                                state = READ_MAX_DYNAMIC_TABLE_SIZE;
                            }
                            else
                            {
                                setDynamicTableSize(index);
                                state = READ_HEADER_REPRESENTATION;
                            }
                        }
                        else
                        {
                            // Literal Header Field without Indexing / never Indexed
                            indexType = ((b & 0x10) == 0x10) ? HpackUtil.IndexType.NEVER : HpackUtil.IndexType.NONE;
                            index = b & 0x0F;
                            switch (index)
                            {
                                case 0:
                                    state = READ_LITERAL_HEADER_NAME_LENGTH_PREFIX;
                                    break;
                                case 0x0F:
                                    state = READ_INDEXED_HEADER_NAME;
                                    break;
                                default:
                                    // Index was stored as the prefix
                                    name = readName(index);
                                    nameLength = name.Count;
                                    state = READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
                                    break;
                            }
                        }

                        break;

                    case READ_MAX_DYNAMIC_TABLE_SIZE:
                        setDynamicTableSize(decodeULE128(input, (long)index));
                        state = READ_HEADER_REPRESENTATION;
                        break;

                    case READ_INDEXED_HEADER:
                        headersLength = indexHeader(decodeULE128(input, index), headers, headersLength);
                        state = READ_HEADER_REPRESENTATION;
                        break;

                    case READ_INDEXED_HEADER_NAME:
                        // Header Name matches an entry in the Header Table
                        name = readName(decodeULE128(input, index));
                        nameLength = name.Count;
                        state = READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
                        break;

                    case READ_LITERAL_HEADER_NAME_LENGTH_PREFIX:
                        b = input.ReadByte();
                        huffmanEncoded = (b & 0x80) == 0x80;
                        index = b & 0x7F;
                        if (index == 0x7f)
                        {
                            state = READ_LITERAL_HEADER_NAME_LENGTH;
                        }
                        else
                        {
                            if (index > maxHeaderListSizeGoAway - headersLength)
                            {
                                Http2CodecUtil.headerListSizeExceeded(maxHeaderListSizeGoAway);
                            }

                            nameLength = index;
                            state = READ_LITERAL_HEADER_NAME;
                        }

                        break;

                    case READ_LITERAL_HEADER_NAME_LENGTH:
                        // Header Name is a Literal String
                        nameLength = decodeULE128(input, index);

                        if (nameLength > maxHeaderListSizeGoAway - headersLength)
                        {
                            Http2CodecUtil.headerListSizeExceeded(maxHeaderListSizeGoAway);
                        }

                        state = READ_LITERAL_HEADER_NAME;
                        break;

                    case READ_LITERAL_HEADER_NAME:
                        // Wait until entire name is readable
                        if (input.ReadableBytes < nameLength)
                        {
                            throw notEnoughDataException(input);
                        }

                        name = readStringLiteral(input, nameLength, huffmanEncoded);

                        state = READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX;
                        break;

                    case READ_LITERAL_HEADER_VALUE_LENGTH_PREFIX:
                        b = input.ReadByte();
                        huffmanEncoded = (b & 0x80) == 0x80;
                        index = b & 0x7F;
                        switch (index)
                        {
                            case 0x7f:
                                state = READ_LITERAL_HEADER_VALUE_LENGTH;
                                break;
                            case 0:
                                headersLength = insertHeader(headers, name, AsciiString.Empty, indexType, headersLength);
                                state = READ_HEADER_REPRESENTATION;
                                break;
                            default:
                                // Check new header size against max header size
                                if ((long)index + nameLength > maxHeaderListSizeGoAway - headersLength)
                                {
                                    Http2CodecUtil.headerListSizeExceeded(maxHeaderListSizeGoAway);
                                }

                                valueLength = index;
                                state = READ_LITERAL_HEADER_VALUE;
                                break;
                        }

                        break;

                    case READ_LITERAL_HEADER_VALUE_LENGTH:
                        // Header Value is a Literal String
                        valueLength = decodeULE128(input, index);

                        // Check new header size against max header size
                        if ((long)valueLength + nameLength > maxHeaderListSizeGoAway - headersLength)
                        {
                            Http2CodecUtil.headerListSizeExceeded(maxHeaderListSizeGoAway);
                        }

                        state = READ_LITERAL_HEADER_VALUE;
                        break;

                    case READ_LITERAL_HEADER_VALUE:
                        // Wait until entire value is readable
                        if (input.ReadableBytes < valueLength)
                        {
                            throw notEnoughDataException(input);
                        }

                        ICharSequence value = readStringLiteral(input, valueLength, huffmanEncoded);
                        headersLength = insertHeader(headers, name, value, indexType, headersLength);
                        state = READ_HEADER_REPRESENTATION;
                        break;

                    default:
                        throw new Exception("should not reach here state: " + state);
                }
            }

            // we have read all of our headers, and not exceeded maxHeaderListSizeGoAway see if we have
            // exceeded our actual maxHeaderListSize. This must be done here to prevent dynamic table
            // corruption
            if (headersLength > maxHeaderListSize)
            {
                Http2CodecUtil.headerListSizeExceeded(streamId, maxHeaderListSize, true);
            }

            if (state != READ_HEADER_REPRESENTATION)
            {
                throw Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "Incomplete header block fragment.");
            }
        }

        /**
         * Set the maximum table size. If this is below the maximum size of the dynamic table used by
         * the encoder, the beginning of the next header block MUST signal this change.
         */
        public void setMaxHeaderTableSize(long maxHeaderTableSize)
        {
            if (maxHeaderTableSize < Http2CodecUtil.MIN_HEADER_TABLE_SIZE || maxHeaderTableSize > Http2CodecUtil.MAX_HEADER_TABLE_SIZE)
            {
                throw Http2Exception.connectionError(
                    Http2Error.PROTOCOL_ERROR,
                    "Header Table Size must be >= %d and <= %d but was %d",
                    Http2CodecUtil.MIN_HEADER_TABLE_SIZE,
                    Http2CodecUtil.MAX_HEADER_TABLE_SIZE,
                    maxHeaderTableSize);
            }

            maxDynamicTableSize = maxHeaderTableSize;
            if (maxDynamicTableSize < encoderMaxDynamicTableSize)
            {
                // decoder requires less space than encoder
                // encoder MUST signal this change
                maxDynamicTableSizeChangeRequired = true;
                hpackDynamicTable.setCapacity(maxDynamicTableSize);
            }
        }

        public void setMaxHeaderListSize(long maxHeaderListSize, long maxHeaderListSizeGoAway)
        {
            if (maxHeaderListSizeGoAway < maxHeaderListSize || maxHeaderListSizeGoAway < 0)
            {
                throw Http2Exception.connectionError(
                    Http2Error.INTERNAL_ERROR,
                    "Header List Size GO_AWAY {0} must be positive and >= {1}",
                    maxHeaderListSizeGoAway,
                    maxHeaderListSize);
            }

            if (maxHeaderListSize < Http2CodecUtil.MIN_HEADER_LIST_SIZE || maxHeaderListSize > Http2CodecUtil.MAX_HEADER_LIST_SIZE)
            {
                throw Http2Exception.connectionError(
                    Http2Error.PROTOCOL_ERROR,
                    "Header List Size must be >= {0} and <= {1} but was {2}",
                    Http2CodecUtil.MIN_HEADER_TABLE_SIZE,
                    Http2CodecUtil.MAX_HEADER_TABLE_SIZE,
                    maxHeaderListSize);
            }

            this.maxHeaderListSize = maxHeaderListSize;
            this.maxHeaderListSizeGoAway = maxHeaderListSizeGoAway;
        }

        public long getMaxHeaderListSize()
        {
            return maxHeaderListSize;
        }

        public long getMaxHeaderListSizeGoAway()
        {
            return maxHeaderListSizeGoAway;
        }

        /**
         * Return the maximum table size. This is the maximum size allowed by both the encoder and the
         * decoder.
         */
        public long getMaxHeaderTableSize()
        {
            return hpackDynamicTable.capacity();
        }

        /**
         * Return the number of header fields input the dynamic table. Exposed for testing.
         */
        int length()
        {
            return hpackDynamicTable.length();
        }

        /**
         * Return the size of the dynamic table. Exposed for testing.
         */
        long size()
        {
            return hpackDynamicTable.size();
        }

        /**
         * Return the header field at the given index. Exposed for testing.
         */
        HpackHeaderField getHeaderField(int index)
        {
            return hpackDynamicTable.getEntry(index + 1);
        }

        private void setDynamicTableSize(long dynamicTableSize)
        {
            if (dynamicTableSize > maxDynamicTableSize)
            {
                throw INVALID_MAX_DYNAMIC_TABLE_SIZE;
            }

            encoderMaxDynamicTableSize = dynamicTableSize;
            maxDynamicTableSizeChangeRequired = false;
            hpackDynamicTable.setCapacity(dynamicTableSize);
        }

        private ICharSequence readName(int index)
        {
            if (index <= HpackStaticTable.length)
            {
                HpackHeaderField hpackHeaderField = HpackStaticTable.getEntry(index);
                return hpackHeaderField.name;
            }

            if (index - HpackStaticTable.length <= hpackDynamicTable.length())
            {
                HpackHeaderField hpackHeaderField = hpackDynamicTable.getEntry(index - HpackStaticTable.length);
                return hpackHeaderField.name;
            }

            throw READ_NAME_ILLEGAL_INDEX_VALUE;
        }

        private long indexHeader(int index, Http2Headers headers, long headersLength)
        {
            if (index <= HpackStaticTable.length)
            {
                HpackHeaderField hpackHeaderField = HpackStaticTable.getEntry(index);
                return addHeader(headers, hpackHeaderField.name, hpackHeaderField.value, headersLength);
            }

            if (index - HpackStaticTable.length <= hpackDynamicTable.length())
            {
                HpackHeaderField hpackHeaderField = hpackDynamicTable.getEntry(index - HpackStaticTable.length);
                return addHeader(headers, hpackHeaderField.name, hpackHeaderField.value, headersLength);
            }

            throw INDEX_HEADER_ILLEGAL_INDEX_VALUE;
        }

        private long insertHeader(
            Http2Headers headers,
            ICharSequence name,
            ICharSequence value,
            HpackUtil.IndexType indexType,
            long headerSize)
        {
            headerSize = addHeader(headers, name, value, headerSize);
            switch (indexType)
            {
                case HpackUtil.IndexType.NONE:
                case HpackUtil.IndexType.NEVER:
                    break;
                case HpackUtil.IndexType.INCREMENTAL:
                    hpackDynamicTable.add(new HpackHeaderField(name, value));
                    break;
                default:
                    throw new Exception("should not reach here");
            }

            return headerSize;
        }

        private long addHeader(Http2Headers headers, ICharSequence name, ICharSequence value, long headersLength)
        {
            headersLength += HpackHeaderField.sizeOf(name, value);
            if (headersLength > maxHeaderListSizeGoAway)
            {
                Http2CodecUtil.headerListSizeExceeded(maxHeaderListSizeGoAway);
            }

            headers.Add(name, value);
            return headersLength;
        }

        private ICharSequence readStringLiteral(IByteBuffer input, int length, bool huffmanEncoded)
        {
            if (huffmanEncoded)
            {
                return hpackHuffmanDecoder.decode(input, length);
            }

            byte[] buf = new byte[length];
            input.ReadBytes(buf);
            return new AsciiString(buf, false);
        }

        private static ArgumentException notEnoughDataException(IByteBuffer input)
        {
            return new ArgumentException("decode only works with an entire header block! " + input);
        }

        /**
         * Unsigned Little Endian Base 128 Variable-Length Integer Encoding
         * <p>
         * Visible for testing only!
         */
        static int decodeULE128(IByteBuffer input, int result)
        {
            int readerIndex = input.ReaderIndex;
            long v = decodeULE128(input, (long)result);
            if (v > int.MaxValue)
            {
                // the maximum value that can be represented by a signed 32 bit number is:
                // [0x1,0x7f] + 0x7f + (0x7f << 7) + (0x7f << 14) + (0x7f << 21) + (0x6 << 28)
                // OR
                // 0x0 + 0x7f + (0x7f << 7) + (0x7f << 14) + (0x7f << 21) + (0x7 << 28)
                // we should reset the readerIndex if we overflowed the int type.
                input.SetReaderIndex(readerIndex);
                throw DECODE_ULE_128_TO_INT_DECOMPRESSION_EXCEPTION;
            }

            return (int)v;
        }

        /**
         * Unsigned Little Endian Base 128 Variable-Length Integer Encoding
         * <p>
         * Visible for testing only!
         */
        static long decodeULE128(IByteBuffer input, long result)
        {
            Contract.Assert(result <= 0x7f && result >= 0);
            bool resultStartedAtZero = result == 0;
            int writerIndex = input.WriterIndex;
            for (int readerIndex = input.ReaderIndex, shift = 0; readerIndex < writerIndex; ++readerIndex, shift += 7)
            {
                byte b = input.GetByte(readerIndex);
                if (shift == 56 && ((b & 0x80) != 0 || b == 0x7F && !resultStartedAtZero))
                {
                    // the maximum value that can be represented by a signed 64 bit number is:
                    // [0x01L, 0x7fL] + 0x7fL + (0x7fL << 7) + (0x7fL << 14) + (0x7fL << 21) + (0x7fL << 28) + (0x7fL << 35)
                    // + (0x7fL << 42) + (0x7fL << 49) + (0x7eL << 56)
                    // OR
                    // 0x0L + 0x7fL + (0x7fL << 7) + (0x7fL << 14) + (0x7fL << 21) + (0x7fL << 28) + (0x7fL << 35) +
                    // (0x7fL << 42) + (0x7fL << 49) + (0x7fL << 56)
                    // this means any more shifts will result in overflow so we should break out and throw an error.
                    throw DECODE_ULE_128_TO_LONG_DECOMPRESSION_EXCEPTION;
                }

                if ((b & 0x80) == 0)
                {
                    input.SetReaderIndex(readerIndex + 1);
                    return result + ((b & 0x7FL) << shift);
                }

                result += (b & 0x7FL) << shift;
            }

            throw DECODE_ULE_128_DECOMPRESSION_EXCEPTION;
        }
    }
}