// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Text;

    /**
     * Provides utility methods for accessing specific flags as defined by the HTTP/2 spec.
     */

    public sealed class Http2Flags
    {
        public const short END_STREAM = 0x1;

        public const short END_HEADERS = 0x4;

        public const short ACK = 0x1;

        public const short PADDED = 0x8;

        public const short PRIORITY = 0x20;

        short _value;

        public Http2Flags()
        {
        }

        public Http2Flags(short value)
        {
            this._value = value;
        }

        /**
         * Gets the underlying flags value.
         */
        public short value()
        {
            return this._value;
        }

        /**
         * Determines whether the {@link #END_STREAM} flag is set. Only applies to DATA and HEADERS
         * frames.
         */
        public bool endOfStream()
        {
            return this.isFlagSet(END_STREAM);
        }

        /**
         * Determines whether the {@link #END_HEADERS} flag is set. Only applies for HEADERS,
         * PUSH_PROMISE, and CONTINUATION frames.
         */
        public bool endOfHeaders()
        {
            return this.isFlagSet(END_HEADERS);
        }

        /**
         * Determines whether the flag is set indicating the presence of the exclusive, stream
         * dependency, and weight fields in a HEADERS frame.
         */
        public bool priorityPresent()
        {
            return this.isFlagSet(PRIORITY);
        }

        /**
         * Determines whether the flag is set indicating that this frame is an ACK. Only applies for
         * SETTINGS and PING frames.
         */
        public bool ack()
        {
            return this.isFlagSet(ACK);
        }

        /**
         * For frames that include padding, indicates if the {@link #PADDED} field is present. Only
         * applies to DATA, HEADERS, PUSH_PROMISE and CONTINUATION frames.
         */
        public bool paddingPresent()
        {
            return this.isFlagSet(PADDED);
        }

        /**
         * Gets the number of bytes expected for the priority fields of the payload. This is determined
         * by the {@link #priorityPresent()} flag.
         */
        public int getNumPriorityBytes()
        {
            return this.priorityPresent() ? 5 : 0;
        }

        /**
         * Gets the length in bytes of the padding presence field expected in the payload. This is
         * determined by the {@link #paddingPresent()} flag.
         */
        public int getPaddingPresenceFieldLength()
        {
            return this.paddingPresent() ? 1 : 0;
        }

        /**
         * Sets the {@link #END_STREAM} flag.
         */
        public Http2Flags endOfStream(bool endOfStream)
        {
            return this.setFlag(endOfStream, END_STREAM);
        }

        /**
         * Sets the {@link #END_HEADERS} flag.
         */
        public Http2Flags endOfHeaders(bool endOfHeaders)
        {
            return this.setFlag(endOfHeaders, END_HEADERS);
        }

        /**
         * Sets the {@link #PRIORITY} flag.
         */
        public Http2Flags priorityPresent(bool priorityPresent)
        {
            return this.setFlag(priorityPresent, PRIORITY);
        }

        /**
         * Sets the {@link #PADDED} flag.
         */
        public Http2Flags paddingPresent(bool paddingPresent)
        {
            return this.setFlag(paddingPresent, PADDED);
        }

        /**
         * Sets the {@link #ACK} flag.
         */
        public Http2Flags ack(bool ack)
        {
            return this.setFlag(ack, ACK);
        }

        /**
         * Generic method to set any flag.
         * @param on if the flag should be enabled or disabled.
         * @param mask the mask that identifies the bit for the flag.
         * @return this instance.
         */
        public Http2Flags setFlag(bool on, short mask)
        {
            if (on)
            {
                this._value |= mask;
            }
            else
            {
                this._value = (short)(this._value & ~mask);
            }

            return this;
        }

        /**
         * Indicates whether or not a particular flag is set.
         * @param mask the mask identifying the bit for the particular flag being tested
         * @return {@code true} if the flag is set
         */
        public bool isFlagSet(short mask)
        {
            return (this._value & mask) != 0;
        }

        public override int GetHashCode()
        {
            int prime = 31;
            int result = 1;
            result = prime * result + this._value;
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null)
            {
                return false;
            }

            if (this.GetType() != obj.GetType())
            {
                return false;
            }

            return this._value == ((Http2Flags)obj)._value;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("value = ").Append(this._value).Append(" (");
            if (this.ack())
            {
                builder.Append("ACK,");
            }

            if (this.endOfHeaders())
            {
                builder.Append("END_OF_HEADERS,");
            }

            if (this.endOfStream())
            {
                builder.Append("END_OF_STREAM,");
            }

            if (this.priorityPresent())
            {
                builder.Append("PRIORITY_PRESENT,");
            }

            if (this.paddingPresent())
            {
                builder.Append("PADDING_PRESENT,");
            }

            builder.Append(')');
            return builder.ToString();
        }
    }
}