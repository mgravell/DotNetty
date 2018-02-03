// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /**
     * The default {@link Http2PingFrame} implementation.
     */
    public class DefaultHttp2PingFrame : DefaultByteBufferHolder, Http2PingFrame
    {
        readonly bool _ack;

        public DefaultHttp2PingFrame(IByteBuffer content)
            : this(content, false)
        {
            ;
        }

        /**
     * A user cannot send a ping ack, as this is done automatically when a ping is received.
     */
        DefaultHttp2PingFrame(IByteBuffer content, bool ack)
            : base(mustBeEightBytes(content))
        {
            this._ack = ack;
        }

        public bool ack()
        {
            return this._ack;
        }

        public string name()
        {
            return "PING";
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2PingFrame(content, this._ack);
        }

        public override bool Equals(object o)
        {
            if (!(o is Http2PingFrame))
            {
                return false;
            }

            Http2PingFrame other = (Http2PingFrame)o;
            return base.Equals(o) && this._ack == other.ack();
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (this._ack ? 1 : 0);
            return hash;
        }

        static IByteBuffer mustBeEightBytes(IByteBuffer content)
        {
            if (content.ReadableBytes != 8)
            {
                throw new ArgumentException(
                    "PING frames require 8 bytes of content. Was " +
                    content.ReadableBytes + " bytes.");
            }

            return content;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(content=" + this.ContentToString() + ", ack=" + this._ack + ')';
        }
    }
}