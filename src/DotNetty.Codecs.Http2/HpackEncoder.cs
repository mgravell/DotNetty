// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    sealed class HpackEncoder
    {
        static readonly Encoding Enc = Encoding.GetEncoding("ISO-8859-1");

        // a linked hash map of header fields
        readonly HeaderEntry[] headerFields;
        readonly HeaderEntry head = new HeaderEntry(-1, AsciiString.Empty, AsciiString.Empty, int.MaxValue, null);
        readonly HpackHuffmanEncoder hpackHuffmanEncoder = new HpackHuffmanEncoder();
        readonly byte hashMask;
        readonly bool ignoreMaxHeaderListSize;
        long _size;
        long _maxHeaderTableSize;
        long maxHeaderListSize;

        /**
     * Creates a new encoder.
     */
        internal HpackEncoder()
            : this(false)
        {
        }

        /**
     * Creates a new encoder.
     */
        public HpackEncoder(bool ignoreMaxHeaderListSize)
            : this(ignoreMaxHeaderListSize, 16)
        {
        }

        /**
     * Creates a new encoder.
     */
        public HpackEncoder(bool ignoreMaxHeaderListSize, int arraySizeHint)
        {
            this.ignoreMaxHeaderListSize = ignoreMaxHeaderListSize;
            this._maxHeaderTableSize = Http2CodecUtil.DEFAULT_HEADER_TABLE_SIZE;
            this.maxHeaderListSize = Http2CodecUtil.DEFAULT_HEADER_LIST_SIZE;
            // Enforce a bound of [2, 128] because hashMask is a byte. The max possible value of hashMask is one less
            // than the length of this array, and we want the mask to be > 0.
            this.headerFields = new HeaderEntry[MathUtil.FindNextPositivePowerOfTwo(Math.Max(2, Math.Min(arraySizeHint, 128)))];
            this.hashMask = (byte)(this.headerFields.Length - 1);
            this.head.before = this.head.after = this.head;
        }

        /**
     * Encode the header field into the header block.
     *
     * <strong>The given {@link ICharSequence}s must be immutable!</strong>
     */
        public void encodeHeaders(int streamId, IByteBuffer output, Http2Headers headers, SensitivityDetector sensitivityDetector)
        {
            if (this.ignoreMaxHeaderListSize)
            {
                this.encodeHeadersIgnoreMaxHeaderListSize(output, headers, sensitivityDetector);
            }
            else
            {
                this.encodeHeadersEnforceMaxHeaderListSize(streamId, output, headers, sensitivityDetector);
            }
        }

        void encodeHeadersEnforceMaxHeaderListSize(int streamId, IByteBuffer output, Http2Headers headers, SensitivityDetector sensitivityDetector)
        {
            long headerSize = 0;
            // To ensure we stay consistent with our peer check the _size is valid before we potentially modify HPACK state.
            foreach (HeaderEntry<ICharSequence, ICharSequence> header in headers)
            {
                ICharSequence name = header.Key;
                ICharSequence value = header.Value;
                // OK to increment now and check for bounds after because this value is limited to unsigned int and will not
                // overflow.
                headerSize += HpackHeaderField.sizeOf(name, value);
                if (headerSize > this.maxHeaderListSize)
                {
                    Http2CodecUtil.headerListSizeExceeded(streamId, this.maxHeaderListSize, false);
                }
            }

            this.encodeHeadersIgnoreMaxHeaderListSize(@output, headers, sensitivityDetector);
        }

        void encodeHeadersIgnoreMaxHeaderListSize(IByteBuffer output, Http2Headers headers, SensitivityDetector sensitivityDetector)
        {
            foreach (HeaderEntry<ICharSequence, ICharSequence> header in headers)
            {
                ICharSequence name = header.Key;
                ICharSequence value = header.Value;
                this.encodeHeader(output, name, value, sensitivityDetector.isSensitive(name, value), HpackHeaderField.sizeOf(name, value));
            }
        }

        /**
         * Encode the header field into the header block.
         *
         * <strong>The given {@link ICharSequence}s must be immutable!</strong>
         */
        void encodeHeader(IByteBuffer @output, ICharSequence name, ICharSequence value, bool sensitive, long headerSize)
        {
            // If the header value is sensitive then it must never be indexed
            if (sensitive)
            {
                int nameIndex = this.getNameIndex(name);
                this.encodeLiteral(@output, name, value, HpackUtil.IndexType.NEVER, nameIndex);
                return;
            }

            // If the peer will only use the static table
            if (this._maxHeaderTableSize == 0)
            {
                int staticTableIndex = HpackStaticTable.getIndex(name, value);
                if (staticTableIndex == -1)
                {
                    int nameIndex = HpackStaticTable.getIndex(name);
                    this.encodeLiteral(@output, name, value, HpackUtil.IndexType.NONE, nameIndex);
                }
                else
                {
                    encodeInteger(@output, 0x80, 7, staticTableIndex);
                }

                return;
            }

            // If the headerSize is greater than the max table _size then it must be encoded literally
            if (headerSize > this._maxHeaderTableSize)
            {
                int nameIndex = this.getNameIndex(name);
                this.encodeLiteral(@output, name, value, HpackUtil.IndexType.NONE, nameIndex);
                return;
            }

            HeaderEntry headerField = this.getEntry(name, value);
            if (headerField != null)
            {
                int index = this.getIndex(headerField.index) + HpackStaticTable.length;
                // Section 6.1. Indexed Header Field Representation
                encodeInteger(@output, 0x80, 7, index);
            }
            else
            {
                int staticTableIndex = HpackStaticTable.getIndex(name, value);
                if (staticTableIndex != -1)
                {
                    // Section 6.1. Indexed Header Field Representation
                    encodeInteger(@output, 0x80, 7, staticTableIndex);
                }
                else
                {
                    this.ensureCapacity(headerSize);
                    this.encodeLiteral(@output, name, value, HpackUtil.IndexType.INCREMENTAL, this.getNameIndex(name));
                    this.add(name, value, headerSize);
                }
            }
        }

        /**
         * Set the maximum table _size.
         */
        public void setMaxHeaderTableSize(IByteBuffer output, long _maxHeaderTableSize)
        {
            if (_maxHeaderTableSize < Http2CodecUtil.MIN_HEADER_TABLE_SIZE || _maxHeaderTableSize > Http2CodecUtil.MAX_HEADER_TABLE_SIZE)
            {
                throw Http2Exception.connectionError(
                    Http2Error.PROTOCOL_ERROR,
                    "Header Table Size must be >= {0} and <= {1} but was {2}",
                    Http2CodecUtil.MIN_HEADER_TABLE_SIZE,
                    Http2CodecUtil.MAX_HEADER_TABLE_SIZE,
                    _maxHeaderTableSize);
            }

            if (this._maxHeaderTableSize == _maxHeaderTableSize)
            {
                return;
            }

            this._maxHeaderTableSize = _maxHeaderTableSize;
            this.ensureCapacity(0);
            // Casting to integer is safe as we verified the _maxHeaderTableSize is a valid unsigned int.
            encodeInteger(output, 0x20, 5, _maxHeaderTableSize);
        }

        /**
         * Return the maximum table _size.
         */
        public long getMaxHeaderTableSize()
        {
            return this._maxHeaderTableSize;
        }

        public void setMaxHeaderListSize(long maxHeaderListSize)
        {
            if (maxHeaderListSize < Http2CodecUtil.MIN_HEADER_LIST_SIZE || maxHeaderListSize > Http2CodecUtil.MAX_HEADER_LIST_SIZE)
            {
                throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Header List Size must be >= {0} and <= {1} but was {2}", Http2CodecUtil.MIN_HEADER_LIST_SIZE, Http2CodecUtil.MAX_HEADER_LIST_SIZE, maxHeaderListSize);
            }

            this.maxHeaderListSize = maxHeaderListSize;
        }

        public long getMaxHeaderListSize()
        {
            return this.maxHeaderListSize;
        }

        /**
         * Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
         */
        static void encodeInteger(IByteBuffer output, int mask, int n, int i)
        {
            encodeInteger(output, mask, n, (long)i);
        }

        /**
         * Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
         */
        static void encodeInteger(IByteBuffer output, int mask, int n, long i)
        {
            Contract.Assert(n >= 0 && n <= 8, "N: " + n);
            int nbits = 0xFF >> (8 - n);
            if (i < nbits)
            {
                output.WriteByte((int)(mask | i));
            }
            else
            {
                output.WriteByte(mask | nbits);
                long length = i - nbits;
                for (; (length & ~0x7F) != 0; length >>= 7)
                {
                    output.WriteByte((int)((length & 0x7F) | 0x80));
                }

                output.WriteByte((int)length);
            }
        }

        /**
         * Encode string literal according to Section 5.2.
         */
        void encodeStringLiteral(IByteBuffer output, ICharSequence str)
        {
            int huffmanLength = this.hpackHuffmanEncoder.getEncodedLength(str);
            if (huffmanLength < str.Count)
            {
                encodeInteger(output, 0x80, 7, huffmanLength);
                this.hpackHuffmanEncoder.encode(output, str);
            }
            else
            {
                encodeInteger(output, 0x00, 7, str.Count);
                if (str is AsciiString)
                {
                    // Fast-path
                    AsciiString asciiString = (AsciiString)str;
                    output.WriteBytes(asciiString.Array, asciiString.Offset, asciiString.Count);
                }
                else
                {
                    // Only ASCII is allowed in http2 headers, so its fine to use this.
                    // https://tools.ietf.org/html/rfc7540#section-8.1.2
                    output.WriteCharSequence(str, Enc);
                }
            }
        }

        /**
         * Encode literal header field according to Section 6.2.
         */
        void encodeLiteral(IByteBuffer output, ICharSequence name, ICharSequence value, HpackUtil.IndexType indexType, int nameIndex)
        {
            bool nameIndexValid = nameIndex != -1;
            switch (indexType)
            {
                case HpackUtil.IndexType.INCREMENTAL:
                    encodeInteger(output, 0x40, 6, nameIndexValid ? nameIndex : 0);
                    break;
                case HpackUtil.IndexType.NONE:
                    encodeInteger(output, 0x00, 4, nameIndexValid ? nameIndex : 0);
                    break;
                case HpackUtil.IndexType.NEVER:
                    encodeInteger(output, 0x10, 4, nameIndexValid ? nameIndex : 0);
                    break;
                default:
                    throw new Exception("should not reach here");
            }

            if (!nameIndexValid)
            {
                this.encodeStringLiteral(output, name);
            }

            this.encodeStringLiteral(output, value);
        }

        int getNameIndex(ICharSequence name)
        {
            int index = HpackStaticTable.getIndex(name);
            if (index == -1)
            {
                index = this.getIndex(name);
                if (index >= 0)
                {
                    index += HpackStaticTable.length;
                }
            }

            return index;
        }

        /**
         * Ensure that the dynamic table has enough room to hold 'headerSize' more bytes. Removes the
         * oldest entry from the dynamic table until sufficient space is available.
         */
        void ensureCapacity(long headerSize)
        {
            while (this._maxHeaderTableSize - this._size < headerSize)
            {
                int index = this.length();
                if (index == 0)
                {
                    break;
                }

                this.remove();
            }
        }

        /**
         * Return the number of header fields in the dynamic table. Exposed for testing.
         */
        int length()
        {
            return this._size == 0 ? 0 : this.head.after.index - this.head.before.index + 1;
        }

        /**
         * Return the _size of the dynamic table. Exposed for testing.
         */
        long size()
        {
            return this._size;
        }

        /**
         * Return the header field at the given index. Exposed for testing.
         */
        HpackHeaderField getHeaderField(int index)
        {
            HeaderEntry entry = this.head;
            while (index-- >= 0)
            {
                entry = entry.before;
            }

            return entry;
        }

        /**
         * Returns the header entry with the lowest index value for the header field. Returns null if
         * header field is not in the dynamic table.
         */
        HeaderEntry getEntry(ICharSequence name, ICharSequence value)
        {
            if (this.length() == 0 || name == null || value == null)
            {
                return null;
            }

            int h = AsciiString.GetHashCode(name);
            int i = this.index(h);
            for (HeaderEntry e = this.headerFields[i]; e != null; e = e.next)
            {
                // To avoid short circuit behavior a bitwise operator is used instead of a bool operator.
                if (e.hash == h && (HpackUtil.equalsConstantTime(name, e.name) & HpackUtil.equalsConstantTime(value, e.value)) != 0)
                {
                    return e;
                }
            }

            return null;
        }

        /**
         * Returns the lowest index value for the header field name in the dynamic table. Returns -1 if
         * the header field name is not in the dynamic table.
         */
        int getIndex(ICharSequence name)
        {
            if (this.length() == 0 || name == null)
            {
                return -1;
            }

            int h = AsciiString.GetHashCode(name);
            int i = this.index(h);
            for (HeaderEntry e = this.headerFields[i]; e != null; e = e.next)
            {
                if (e.hash == h && HpackUtil.equalsConstantTime(name, e.name) != 0)
                {
                    return this.getIndex(e.index);
                }
            }

            return -1;
        }

        /**
         * Compute the index into the dynamic table given the index in the header entry.
         */
        int getIndex(int index)
        {
            return index == -1 ? -1 : index - this.head.before.index + 1;
        }

        /**
         * Add the header field to the dynamic table. Entries are evicted from the dynamic table until
         * the _size of the table and the new header field is less than the table's _maxHeaderTableSize. If the _size
         * of the new entry is larger than the table's _maxHeaderTableSize, the dynamic table will be cleared.
         */
        void add(ICharSequence name, ICharSequence value, long headerSize)
        {
            // Clear the table if the header field _size is larger than the _maxHeaderTableSize.
            if (headerSize > this._maxHeaderTableSize)
            {
                this.clear();
                return;
            }

            // Evict oldest entries until we have enough _maxHeaderTableSize.
            while (this._maxHeaderTableSize - this._size < headerSize)
            {
                this.remove();
            }

            int h = AsciiString.GetHashCode(name);
            int i = this.index(h);
            HeaderEntry old = this.headerFields[i];
            HeaderEntry e = new HeaderEntry(h, name, value, this.head.before.index - 1, old);
            this.headerFields[i] = e;
            e.addBefore(this.head);
            this._size += headerSize;
        }

        /**
         * Remove and return the oldest header field from the dynamic table.
         */
        HpackHeaderField remove()
        {
            if (this._size == 0)
            {
                return null;
            }

            HeaderEntry eldest = this.head.after;
            int h = eldest.hash;
            int i = this.index(h);
            HeaderEntry prev = this.headerFields[i];
            HeaderEntry e = prev;
            while (e != null)
            {
                HeaderEntry next = e.next;
                if (e == eldest)
                {
                    if (prev == eldest)
                    {
                        this.headerFields[i] = next;
                    }
                    else
                    {
                        prev.next = next;
                    }

                    eldest.remove();
                    this._size -= eldest.size();
                    return eldest;
                }

                prev = e;
                e = next;
            }

            return null;
        }

        /**
         * Remove all entries from the dynamic table.
         */
        void clear()
        {
            for (var i = 0; i < this.headerFields.Length; i++)
            {
                this.headerFields[i] = null;
            }

            //Arrays.fill(headerFields, null);
            this.head.before = this.head.after = this.head;
            this._size = 0;
        }

        /**
         * Returns the index into the hash table for the hash code h.
         */
        int index(int h)
        {
            return h & this.hashMask;
        }

        /**
         * A linked hash map HpackHeaderField entry.
         */
        sealed class HeaderEntry : HpackHeaderField
        {
            // These fields comprise the doubly linked list used for iteration.
            internal HeaderEntry before;
            internal HeaderEntry after;

            // These fields comprise the chained list for header fields with the same hash.
            internal HeaderEntry next;
            internal readonly int hash;

            // This is used to compute the index in the dynamic table.
            internal readonly int index;

            /**
             * Creates new entry.
             */
            internal HeaderEntry(int hash, ICharSequence name, ICharSequence value, int index, HeaderEntry next)
                : base(name, value)
            {
                this.index = index;
                this.hash = hash;
                this.next = next;
            }

            /**
             * Removes this entry from the linked list.
             */
            internal void remove()
            {
                this.before.after = this.after;
                this.after.before = this.before;
                this.before = null; // null references to prevent nepotism in generational GC.
                this.after = null;
                this.next = null;
            }

            /**
             * Inserts this entry before the specified existing entry in the list.
             */
            internal void addBefore(HeaderEntry existingEntry)
            {
                this.after = existingEntry;
                this.before = existingEntry.before;
                this.before.after = this;
                this.after.before = this;
            }
        }
    }
}