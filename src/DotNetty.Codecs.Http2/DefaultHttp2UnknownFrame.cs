// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public sealed class DefaultHttp2UnknownFrame : DefaultByteBufferHolder, Http2UnknownFrame
    {
        readonly byte _frameType;
        readonly Http2Flags _flags;
        Http2FrameStream _stream;

        public DefaultHttp2UnknownFrame(byte frameType, Http2Flags flags)
            : this(frameType, flags, Unpooled.Empty)
        {
            ;
        }

        public DefaultHttp2UnknownFrame(byte frameType, Http2Flags flags, IByteBuffer data)
            : base(data)
        {
            this._frameType = frameType;
            this._flags = flags;
        }

        public Http2FrameStream stream()
        {
            return this._stream;
        }

        public Http2UnknownFrame stream(Http2FrameStream stream)
        {
            this._stream = stream;
            return this;
        }

        public byte frameType()
        {
            return this._frameType;
        }

        public Http2Flags flags()
        {
            return this._flags;
        }

        public string name()
        {
            return "UNKNOWN";
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2UnknownFrame(this._frameType, this._flags, content).stream(this.stream());
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(frameType=" + this.frameType() + ", stream=" + this.stream() +
                ", flags=" + this.flags() + ", content=" + this.ContentToString() + ')';
        }

        public override bool Equals(object o)
        {
            if (!(o is DefaultHttp2UnknownFrame))
            {
                return false;
            }

            DefaultHttp2UnknownFrame other = (DefaultHttp2UnknownFrame)o;
            return base.Equals(other) && this.flags().Equals(other.flags())
                && this.frameType() == other.frameType() && (this.stream() == null && other.stream() == null) ||
                this.stream().Equals(other.stream());
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + this.frameType();
            hash = hash * 31 + this.flags().GetHashCode();
            if (this.stream() != null)
            {
                hash = hash * 31 + this.stream().GetHashCode();
            }

            return hash;
        }
    }
}