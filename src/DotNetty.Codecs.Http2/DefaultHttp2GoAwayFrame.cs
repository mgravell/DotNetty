// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /**
 * The default {@link Http2GoAwayFrame} implementation.
 */
    public sealed class DefaultHttp2GoAwayFrame : DefaultByteBufferHolder, Http2GoAwayFrame
    {
        readonly ulong _errorCode;
        readonly int _lastStreamId;
        int _extraStreamIds;

        /**
         * Equivalent to {@code new DefaultHttp2GoAwayFrame(error.code())}.
         *
         * @param error non-{@code null} reason for the go away
         */
        public DefaultHttp2GoAwayFrame(Http2Error error)
            : this((ulong)error)
        {
            ;
        }

        /**
         * Equivalent to {@code new DefaultHttp2GoAwayFrame(content, Unpooled.EMPTY_BUFFER)}.
         *
         * @param errorCode reason for the go away
         */
        public DefaultHttp2GoAwayFrame(ulong errorCode)
            : this(errorCode, Unpooled.Empty)
        {
        }

        /**
         *
         *
         * @param error non-{@code null} reason for the go away
         * @param content non-{@code null} debug data
         */
        public DefaultHttp2GoAwayFrame(Http2Error error, IByteBuffer content)
            : this((ulong)error, content)
        {
        }

        /**
         * Construct a new GOAWAY message.
         *
         * @param errorCode reason for the go away
         * @param content non-{@code null} debug data
         */
        public DefaultHttp2GoAwayFrame(ulong errorCode, IByteBuffer content)
            : this(-1, errorCode, content)
        {
        }

        /**
         * Construct a new GOAWAY message.
         *
         * This constructor is for internal use only. A user should not have to specify a specific last stream identifier,
         * but use {@link #setExtraStreamIds(int)} instead.
         */
        DefaultHttp2GoAwayFrame(int lastStreamId, ulong errorCode, IByteBuffer content)
            : base(content)
        {
            this._errorCode = errorCode;
            this._lastStreamId = lastStreamId;
        }

        public string name()
        {
            return "GOAWAY";
        }

        public ulong errorCode()
        {
            return this._errorCode;
        }

        public int extraStreamIds()
        {
            return this._extraStreamIds;
        }

        public Http2GoAwayFrame setExtraStreamIds(int extraStreamIds)
        {
            if (extraStreamIds < 0)
            {
                throw new ArgumentException("extraStreamIds must be non-negative");
            }

            this._extraStreamIds = extraStreamIds;
            return this;
        }

        public int lastStreamId()
        {
            return this._lastStreamId;
        }

        public override IByteBufferHolder Copy()
        {
            return new DefaultHttp2GoAwayFrame(this._lastStreamId, this._errorCode, this.Content.Copy());
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2GoAwayFrame(this._errorCode, content).setExtraStreamIds(this._extraStreamIds);
        }

        public override bool Equals(object o)
        {
            if (!(o is DefaultHttp2GoAwayFrame))
            {
                return false;
            }

            DefaultHttp2GoAwayFrame other = (DefaultHttp2GoAwayFrame)o;
            return this._errorCode == other._errorCode && this._extraStreamIds == other._extraStreamIds && base.Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (int)(this._errorCode ^ (this._errorCode >> 32));
            hash = hash * 31 + this._extraStreamIds;
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(errorCode=" + this._errorCode + ", content=" + this.Content
                + ", extraStreamIds=" + this._extraStreamIds + ", lastStreamId=" + this._lastStreamId + ')';
        }
    }
}