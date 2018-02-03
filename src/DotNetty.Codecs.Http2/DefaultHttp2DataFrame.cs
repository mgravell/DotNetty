// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /**
     * The default {@link Http2DataFrame} implementation.
     */
    public sealed class DefaultHttp2DataFrame : AbstractHttp2StreamFrame, Http2DataFrame
    {
        readonly IByteBuffer content;
        readonly bool endStream;
        readonly int _padding;
        readonly int _initialFlowControlledBytes;

        /**
         * Equivalent to {@code new DefaultHttp2DataFrame(content, false)}.
         *
         * @param content non-{@code null} payload
         */
        public DefaultHttp2DataFrame(IByteBuffer content)
            : this(content, false)
        {
        }

        /**
         * Equivalent to {@code new DefaultHttp2DataFrame(Unpooled.EMPTY_BUFFER, endStream)}.
         *
         * @param endStream whether this data should terminate the stream
         */
        public DefaultHttp2DataFrame(bool endStream)
            : this(Unpooled.Empty, endStream)
        {
        }

        /**
         * Equivalent to {@code new DefaultHttp2DataFrame(content, endStream, 0)}.
         *
         * @param content non-{@code null} payload
         * @param endStream whether this data should terminate the stream
         */
        public DefaultHttp2DataFrame(IByteBuffer content, bool endStream)
            : this(content, endStream, 0)
        {
        }

        /**
         * Construct a new data message.
         *
         * @param content non-{@code null} payload
         * @param endStream whether this data should terminate the stream
         * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
         *                256 (inclusive).
         */
        public DefaultHttp2DataFrame(IByteBuffer content, bool endStream, int padding)
        {
            Contract.Requires(content != null);
            this.content = content;
            this.endStream = endStream;
            Http2CodecUtil.verifyPadding(padding);
            this._padding = padding;
            if (this.Content.ReadableBytes + (long)padding > int.MaxValue)
            {
                throw new ArgumentException("content + padding must be <= int.MaxValue");
            }

            this._initialFlowControlledBytes = this.Content.ReadableBytes + padding;
        }

        public override string name()
        {
            return "DATA";
        }

        public bool isEndStream()
        {
            return this.endStream;
        }

        public int padding()
        {
            return this._padding;
        }

        public IByteBuffer Content
        {
            get
            {
                var refCount = this.content.ReferenceCount;
                if (refCount <= 0)
                {
                    throw new IllegalReferenceCountException(refCount);
                }

                return this.content;
            }
        }

        public int initialFlowControlledBytes()
        {
            return this._initialFlowControlledBytes;
        }

        public IByteBufferHolder Copy()
        {
            return this.Replace(this.Content.Copy());
        }

        public IByteBufferHolder Duplicate()
        {
            return this.Replace(this.Content.Duplicate());
        }

        public IByteBufferHolder RetainedDuplicate()
        {
            return this.Replace(this.Content.RetainedDuplicate());
        }

        public IByteBufferHolder Replace(IByteBuffer content)
        {
            return new DefaultHttp2DataFrame(content, this.endStream, this._padding);
        }

        public int ReferenceCount => this.content.ReferenceCount;

        public bool Release()
        {
            return this.content.Release();
        }

        public bool Release(int decrement)
        {
            return this.content.Release(decrement);
        }

        public IReferenceCounted Retain()
        {
            this.content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            this.content.Retain(increment);
            return this;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + this.stream() + ", content=" + this.content
                + ", endStream=" + this.endStream + ", padding=" + this._padding + ')';
        }

        public IReferenceCounted Touch()
        {
            this.content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            this.content.Touch(hint);
            return this;
        }

        public override bool Equals(object o)
        {
            if (!(o is DefaultHttp2DataFrame))
            {
                return false;
            }

            DefaultHttp2DataFrame other = (DefaultHttp2DataFrame)o;
            return base.Equals(other) && this.content.Equals(other.Content)
                && this.endStream == other.endStream && this._padding == other._padding;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + this.content.GetHashCode();
            hash = hash * 31 + (this.endStream ? 0 : 1);
            hash = hash * 31 + this._padding;
            return hash;
        }
    }
}