// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    sealed class HpackDynamicTable
    {
        // a circular queue of header fields
        HpackHeaderField[] hpackHeaderFields;
        int head;
        int tail;
        long _size;
        long _capacity = -1; // ensure setCapacity creates the array

        /**
     * Creates a new dynamic table with the specified initial _capacity.
     */
        HpackDynamicTable(long initialCapacity)
        {
            this.setCapacity(initialCapacity);
        }

        /**
     * Return the number of header fields in the dynamic table.
     */
        public int length()
        {
            int length;
            if (this.head < this.tail)
            {
                length = this.hpackHeaderFields.Length - this.tail + this.head;
            }
            else
            {
                length = this.head - this.tail;
            }

            return length;
        }

        /**
     * Return the current _size of the dynamic table. This is the sum of the _size of the entries.
     */
        public long size()
        {
            return this._size;
        }

        /**
     * Return the maximum allowable _size of the dynamic table.
     */
        public long capacity()
        {
            return this._capacity;
        }

        /**
     * Return the header field at the given index. The first and newest entry is always at index 1,
     * and the oldest entry is at the index length().
     */
        public HpackHeaderField getEntry(int index)
        {
            if (index <= 0 || index > this.length())
            {
                throw new IndexOutOfRangeException();
            }

            int i = this.head - index;
            if (i < 0)
            {
                return this.hpackHeaderFields[i + this.hpackHeaderFields.Length];
            }
            else
            {
                return this.hpackHeaderFields[i];
            }
        }

        /**
     * Add the header field to the dynamic table. Entries are evicted from the dynamic table until
     * the _size of the table and the new header field is less than or equal to the table's _capacity.
     * If the _size of the new entry is larger than the table's _capacity, the dynamic table will be
     * cleared.
     */
        public void add(HpackHeaderField header)
        {
            int headerSize = header.size();
            if (headerSize > this._capacity)
            {
                this.clear();
                return;
            }

            while (this._capacity - this._size < headerSize)
            {
                this.remove();
            }

            this.hpackHeaderFields[this.head++] = header;
            this._size += header.size();
            if (this.head == this.hpackHeaderFields.Length)
            {
                this.head = 0;
            }
        }

        /**
     * Remove and return the oldest header field from the dynamic table.
     */
        public HpackHeaderField remove()
        {
            HpackHeaderField removed = this.hpackHeaderFields[this.tail];
            if (removed == null)
            {
                return null;
            }

            this._size -= removed.size();
            this.hpackHeaderFields[this.tail++] = null;
            if (this.tail == this.hpackHeaderFields.Length)
            {
                this.tail = 0;
            }

            return removed;
        }

        /**
     * Remove all entries from the dynamic table.
     */
        public void clear()
        {
            while (this.tail != this.head)
            {
                this.hpackHeaderFields[this.tail++] = null;
                if (this.tail == this.hpackHeaderFields.Length)
                {
                    this.tail = 0;
                }
            }

            this.head = 0;
            this.tail = 0;
            this._size = 0;
        }

        /**
     * Set the maximum _size of the dynamic table. Entries are evicted from the dynamic table until
     * the _size of the table is less than or equal to the maximum _size.
     */
        public void setCapacity(long _capacity)
        {
            if (_capacity < Http2CodecUtil.MIN_HEADER_TABLE_SIZE || _capacity > Http2CodecUtil.MAX_HEADER_TABLE_SIZE)
            {
                throw new ArgumentException("_capacity is invalid: " + _capacity);
            }

            // initially _capacity will be -1 so init won't return here
            if (this._capacity == _capacity)
            {
                return;
            }

            this._capacity = _capacity;

            if (_capacity == 0)
            {
                this.clear();
            }
            else
            {
                // initially _size will be 0 so remove won't be called
                while (this._size > _capacity)
                {
                    this.remove();
                }
            }

            int maxEntries = (int)(_capacity / HpackHeaderField.HEADER_ENTRY_OVERHEAD);
            if (_capacity % HpackHeaderField.HEADER_ENTRY_OVERHEAD != 0)
            {
                maxEntries++;
            }

            // check if _capacity change requires us to reallocate the array
            if (this.hpackHeaderFields != null && this.hpackHeaderFields.Length == maxEntries)
            {
                return;
            }

            HpackHeaderField[] tmp = new HpackHeaderField[maxEntries];

            // initially length will be 0 so there will be no copy
            int len = this.length();
            int cursor = this.tail;
            for (int i = 0; i < len; i++)
            {
                HpackHeaderField entry = this.hpackHeaderFields[cursor++];
                tmp[i] = entry;
                if (cursor == this.hpackHeaderFields.Length)
                {
                    cursor = 0;
                }
            }

            this.tail = 0;
            this.head = this.tail + len;
            this.hpackHeaderFields = tmp;
        }
    }
}