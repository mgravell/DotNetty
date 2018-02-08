using DotNetty.Common.Utilities;

namespace DotNetty.Codecs.Http2
{
    sealed class HpackEncoder 
    {
        // a linked hash map of header fields
        readonly HeaderEntry[] headerFields;
        readonly HeaderEntry head = new HeaderEntry(-1, AsciiString.Empty, AsciiString.Empty, int.MaxValue, null);
        readonly HpackHuffmanEncoder hpackHuffmanEncoder = new HpackHuffmanEncoder();
        readonly byte hashMask;
        readonly bool ignoreMaxHeaderListSize;
        private long size;
        private long maxHeaderTableSize;
        private long maxHeaderListSize;

        /**
     * Creates a new encoder.
     */
        HpackEncoder() : this(false) {
            
        }

        /**
     * Creates a new encoder.
     */
        public HpackEncoder(bool ignoreMaxHeaderListSize) : this(ignoreMaxHeaderListSize, 16)
        {
            
        }

        /**
     * Creates a new encoder.
     */
        public HpackEncoder(bool ignoreMaxHeaderListSize, int arraySizeHint) {
            this.ignoreMaxHeaderListSize = ignoreMaxHeaderListSize;
            this.maxHeaderTableSize = DEFAULT_HEADER_TABLE_SIZE;
            this.maxHeaderListSize = DEFAULT_HEADER_LIST_SIZE;
            // Enforce a bound of [2, 128] because hashMask is a byte. The max possible value of hashMask is one less
            // than the length of this array, and we want the mask to be > 0.
            headerFields = new HeaderEntry<,>[findNextPositivePowerOfTwo(max(2, min(arraySizeHint, 128)))];
            this.hashMask = (byte) (headerFields.length - 1);
            head.before = head.after = head;
        }

        /**
     * Encode the header field into the header block.
     *
     * <strong>The given {@link CharSequence}s must be immutable!</strong>
     */
        public void encodeHeaders(int streamId, ByteBuf out, Http2Headers headers, SensitivityDetector sensitivityDetector)
         {
        if (ignoreMaxHeaderListSize) {
            encodeHeadersIgnoreMaxHeaderListSize(out, headers, sensitivityDetector);
        } else {
            encodeHeadersEnforceMaxHeaderListSize(streamId, out, headers, sensitivityDetector);
        }
    }


    private void encodeHeadersEnforceMaxHeaderListSize(int streamId, ByteBuf out, Http2Headers headers, SensitivityDetector sensitivityDetector)
     {
        long headerSize = 0;
        // To ensure we stay consistent with our peer check the size is valid before we potentially modify HPACK state.
        foreach (Map.Entry<CharSequence, CharSequence> header in headers) {
            CharSequence name = header.getKey();
            CharSequence value = header.getValue();
            // OK to increment now and check for bounds after because this value is limited to unsigned int and will not
            // overflow.
            headerSize += HpackHeaderField.sizeOf(name, value);
            if (headerSize > maxHeaderListSize) {
                headerListSizeExceeded(streamId, maxHeaderListSize, false);
            }
        }
        encodeHeadersIgnoreMaxHeaderListSize(@out, headers, sensitivityDetector);
    }

    private void encodeHeadersIgnoreMaxHeaderListSize(ByteBuf out, Http2Headers headers, SensitivityDetector sensitivityDetector)  {
        foreach (Map.Entry<CharSequence, CharSequence> header in headers) 
        {
            CharSequence name = header.getKey();
            CharSequence value = header.getValue();
            encodeHeader(out, name, value, sensitivityDetector.isSensitive(name, value),
                         HpackHeaderField.sizeOf(name, value));
        }
    }

    /**
     * Encode the header field into the header block.
     *
     * <strong>The given {@link CharSequence}s must be immutable!</strong>
     */
    private void encodeHeader(ByteBuf @out, CharSequence name, CharSequence value, bool sensitive, long headerSize) {
        // If the header value is sensitive then it must never be indexed
        if (sensitive) {
            int nameIndex = getNameIndex(name);
            encodeLiteral(@out, name, value, IndexType.NEVER, nameIndex);
            return;
        }

        // If the peer will only use the static table
        if (maxHeaderTableSize == 0) {
            int staticTableIndex = HpackStaticTable.getIndex(name, value);
            if (staticTableIndex == -1) {
                int nameIndex = HpackStaticTable.getIndex(name);
                encodeLiteral(@out, name, value, IndexType.NONE, nameIndex);
            } else {
                encodeInteger(@out, 0x80, 7, staticTableIndex);
            }
            return;
        }

        // If the headerSize is greater than the max table size then it must be encoded literally
        if (headerSize > maxHeaderTableSize) {
            int nameIndex = getNameIndex(name);
            encodeLiteral(@out, name, value, IndexType.NONE, nameIndex);
            return;
        }

        HeaderEntry headerField = getEntry(name, value);
        if (headerField != null) {
            int index = getIndex(headerField.index) + HpackStaticTable.length;
            // Section 6.1. Indexed Header Field Representation
            encodeInteger(@out, 0x80, 7, index);
        } else {
            int staticTableIndex = HpackStaticTable.getIndex(name, value);
            if (staticTableIndex != -1) {
                // Section 6.1. Indexed Header Field Representation
                encodeInteger(@out, 0x80, 7, staticTableIndex);
            } else {
                ensureCapacity(headerSize);
                encodeLiteral(@out, name, value, IndexType.INCREMENTAL, getNameIndex(name));
                add(name, value, headerSize);
            }
        }
    }

    /**
     * Set the maximum table size.
     */
    public void setMaxHeaderTableSize(ByteBuf out, long maxHeaderTableSize)  {
        if (maxHeaderTableSize < MIN_HEADER_TABLE_SIZE || maxHeaderTableSize > MAX_HEADER_TABLE_SIZE) {
            throw connectionError(PROTOCOL_ERROR, "Header Table Size must be >= %d and <= %d but was %d",
                    MIN_HEADER_TABLE_SIZE, MAX_HEADER_TABLE_SIZE, maxHeaderTableSize);
        }
        if (this.maxHeaderTableSize == maxHeaderTableSize) {
            return;
        }
        this.maxHeaderTableSize = maxHeaderTableSize;
        ensureCapacity(0);
        // Casting to integer is safe as we verified the maxHeaderTableSize is a valid unsigned int.
        encodeInteger(out, 0x20, 5, maxHeaderTableSize);
    }

    /**
     * Return the maximum table size.
     */
    public long getMaxHeaderTableSize() {
        return maxHeaderTableSize;
    }

    public void setMaxHeaderListSize(long maxHeaderListSize)  {
        if (maxHeaderListSize < MIN_HEADER_LIST_SIZE || maxHeaderListSize > MAX_HEADER_LIST_SIZE) {
            throw connectionError(PROTOCOL_ERROR, "Header List Size must be >= %d and <= %d but was %d",
                    MIN_HEADER_LIST_SIZE, MAX_HEADER_LIST_SIZE, maxHeaderListSize);
        }
        this.maxHeaderListSize = maxHeaderListSize;
    }

    public long getMaxHeaderListSize() {
        return maxHeaderListSize;
    }

    /**
     * Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
     */
    private static void encodeInteger(ByteBuf out, int mask, int n, int i) {
        encodeInteger(out, mask, n, (long) i);
    }

    /**
     * Encode integer according to <a href="https://tools.ietf.org/html/rfc7541#section-5.1">Section 5.1</a>.
     */
    private static void encodeInteger(ByteBuf out, int mask, int n, long i) {
        assert n >= 0 && n <= 8 : "N: " + n;
        int nbits = 0xFF >>> (8 - n);
        if (i < nbits) {
            out.writeByte((int) (mask | i));
        } else {
            out.writeByte(mask | nbits);
            long length = i - nbits;
            for (; (length & ~0x7F) != 0; length >>>= 7) {
                out.writeByte((int) ((length & 0x7F) | 0x80));
            }
            out.writeByte((int) length);
        }
    }

    /**
     * Encode string literal according to Section 5.2.
     */
    private void encodeStringLiteral(ByteBuf out, CharSequence string) {
        int huffmanLength = hpackHuffmanEncoder.getEncodedLength(string);
        if (huffmanLength < string.length()) {
            encodeInteger(out, 0x80, 7, huffmanLength);
            hpackHuffmanEncoder.encode(out, string);
        } else {
            encodeInteger(out, 0x00, 7, string.length());
            if (string instanceof AsciiString) {
                // Fast-path
                AsciiString asciiString = (AsciiString) string;
                out.writeBytes(asciiString.array(), asciiString.arrayOffset(), asciiString.length());
            } else {
                // Only ASCII is allowed in http2 headers, so its fine to use this.
                // https://tools.ietf.org/html/rfc7540#section-8.1.2
                out.writeCharSequence(string, CharsetUtil.ISO_8859_1);
            }
        }
    }

    /**
     * Encode literal header field according to Section 6.2.
     */
    private void encodeLiteral(ByteBuf out, CharSequence name, CharSequence value, IndexType indexType,
                               int nameIndex) {
        bool nameIndexValid = nameIndex != -1;
        switch (indexType) {
            case INCREMENTAL:
                encodeInteger(out, 0x40, 6, nameIndexValid ? nameIndex : 0);
                break;
            case NONE:
                encodeInteger(out, 0x00, 4, nameIndexValid ? nameIndex : 0);
                break;
            case NEVER:
                encodeInteger(out, 0x10, 4, nameIndexValid ? nameIndex : 0);
                break;
            default:
                throw new Error("should not reach here");
        }
        if (!nameIndexValid) {
            encodeStringLiteral(out, name);
        }
        encodeStringLiteral(out, value);
    }

    private int getNameIndex(CharSequence name) {
        int index = HpackStaticTable.getIndex(name);
        if (index == -1) {
            index = getIndex(name);
            if (index >= 0) {
                index += HpackStaticTable.length;
            }
        }
        return index;
    }

    /**
     * Ensure that the dynamic table has enough room to hold 'headerSize' more bytes. Removes the
     * oldest entry from the dynamic table until sufficient space is available.
     */
    private void ensureCapacity(long headerSize) {
        while (maxHeaderTableSize - size < headerSize) {
            int index = length();
            if (index == 0) {
                break;
            }
            remove();
        }
    }

    /**
     * Return the number of header fields in the dynamic table. Exposed for testing.
     */
    int length() {
        return size == 0 ? 0 : head.after.index - head.before.index + 1;
    }

    /**
     * Return the size of the dynamic table. Exposed for testing.
     */
    long size() {
        return size;
    }

    /**
     * Return the header field at the given index. Exposed for testing.
     */
    HpackHeaderField getHeaderField(int index) {
        HeaderEntry entry = head;
        while (index-- >= 0) {
            entry = entry.before;
        }
        return entry;
    }

    /**
     * Returns the header entry with the lowest index value for the header field. Returns null if
     * header field is not in the dynamic table.
     */
    private HeaderEntry getEntry(CharSequence name, CharSequence value) {
        if (length() == 0 || name == null || value == null) {
            return null;
        }
        int h = AsciiString.hashCode(name);
        int i = index(h);
        for (HeaderEntry e = headerFields[i]; e != null; e = e.next) {
            // To avoid short circuit behavior a bitwise operator is used instead of a bool operator.
            if (e.hash == h && (equalsConstantTime(name, e.name) & equalsConstantTime(value, e.value)) != 0) {
                return e;
            }
        }
        return null;
    }

    /**
     * Returns the lowest index value for the header field name in the dynamic table. Returns -1 if
     * the header field name is not in the dynamic table.
     */
    private int getIndex(CharSequence name) {
        if (length() == 0 || name == null) {
            return -1;
        }
        int h = AsciiString.hashCode(name);
        int i = index(h);
        for (HeaderEntry e = headerFields[i]; e != null; e = e.next) {
            if (e.hash == h && equalsConstantTime(name, e.name) != 0) {
                return getIndex(e.index);
            }
        }
        return -1;
    }

    /**
     * Compute the index into the dynamic table given the index in the header entry.
     */
    private int getIndex(int index) {
        return index == -1 ? -1 : index - head.before.index + 1;
    }

    /**
     * Add the header field to the dynamic table. Entries are evicted from the dynamic table until
     * the size of the table and the new header field is less than the table's maxHeaderTableSize. If the size
     * of the new entry is larger than the table's maxHeaderTableSize, the dynamic table will be cleared.
     */
    private void add(CharSequence name, CharSequence value, long headerSize) {
        // Clear the table if the header field size is larger than the maxHeaderTableSize.
        if (headerSize > maxHeaderTableSize) {
            clear();
            return;
        }

        // Evict oldest entries until we have enough maxHeaderTableSize.
        while (maxHeaderTableSize - size < headerSize) {
            remove();
        }

        int h = AsciiString.hashCode(name);
        int i = index(h);
        HeaderEntry old = headerFields[i];
        HeaderEntry e = new HeaderEntry(h, name, value, head.before.index - 1, old);
        headerFields[i] = e;
        e.addBefore(head);
        size += headerSize;
    }

    /**
     * Remove and return the oldest header field from the dynamic table.
     */
    private HpackHeaderField remove() {
        if (size == 0) {
            return null;
        }
        HeaderEntry eldest = head.after;
        int h = eldest.hash;
        int i = index(h);
        HeaderEntry prev = headerFields[i];
        HeaderEntry e = prev;
        while (e != null) {
            HeaderEntry next = e.next;
            if (e == eldest) {
                if (prev == eldest) {
                    headerFields[i] = next;
                } else {
                    prev.next = next;
                }
                eldest.remove();
                size -= eldest.size();
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
    private void clear() {
        Arrays.fill(headerFields, null);
        head.before = head.after = head;
        size = 0;
    }

    /**
     * Returns the index into the hash table for the hash code h.
     */
    private int index(int h) {
        return h & hashMask;
    }

    /**
     * A linked hash map HpackHeaderField entry.
     */
    private static final class HeaderEntry extends HpackHeaderField {
        // These fields comprise the doubly linked list used for iteration.
        HeaderEntry before, after;

        // These fields comprise the chained list for header fields with the same hash.
        HeaderEntry next;
        int hash;

        // This is used to compute the index in the dynamic table.
        int index;

        /**
         * Creates new entry.
         */
        HeaderEntry(int hash, CharSequence name, CharSequence value, int index, HeaderEntry next) {
            super(name, value);
            this.index = index;
            this.hash = hash;
            this.next = next;
        }

        /**
         * Removes this entry from the linked list.
         */
        private void remove() {
            before.after = after;
            after.before = before;
            before = null; // null references to prevent nepotism in generational GC.
            after = null;
            next = null;
        }

        /**
         * Inserts this entry before the specified existing entry in the list.
         */
        private void addBefore(HeaderEntry existingEntry) {
            after = existingEntry;
            before = existingEntry.before;
            before.after = this;
            after.before = this;
        }
    }
}
