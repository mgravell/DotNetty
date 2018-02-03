// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    /**
     * The default {@link Http2HeadersFrame} implementation.
     */
    public sealed class DefaultHttp2HeadersFrame : AbstractHttp2StreamFrame, Http2HeadersFrame
    {
        readonly Http2Headers _headers;
        readonly bool endStream;
        readonly int _padding;

        /**
     * Equivalent to {@code new DefaultHttp2HeadersFrame(headers, false)}.
     *
     * @param headers the non-{@code null} headers to send
     */
        public DefaultHttp2HeadersFrame(Http2Headers headers)
            : this(headers, false)
        {
            ;
        }

        /**
     * Equivalent to {@code new DefaultHttp2HeadersFrame(headers, endStream, 0)}.
     *
     * @param headers the non-{@code null} headers to send
     */
        public DefaultHttp2HeadersFrame(Http2Headers headers, bool endStream)
            : this(headers, endStream, 0)
        {
        }

        /**
     * Construct a new headers message.
     *
     * @param headers the non-{@code null} headers to send
     * @param endStream whether these headers should terminate the stream
     * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
     *                256 (inclusive).
     */
        public DefaultHttp2HeadersFrame(Http2Headers headers, bool endStream, int padding)
        {
            Contract.Requires(headers != null);
            this._headers = headers;
            this.endStream = endStream;
            Http2CodecUtil.verifyPadding(padding);
            this._padding = padding;
        }

        public override string name()
        {
            return "HEADERS";
        }

        public Http2Headers headers()
        {
            return this._headers;
        }

        public bool isEndStream()
        {
            return this.endStream;
        }

        public int padding()
        {
            return this._padding;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + this.stream() + ", headers=" + this._headers
                + ", endStream=" + this.endStream + ", padding=" + this._padding + ')';
        }

        public override bool Equals(object o)
        {
            if (!(o is DefaultHttp2HeadersFrame))
            {
                return false;
            }

            DefaultHttp2HeadersFrame other = (DefaultHttp2HeadersFrame)o;
            return base.Equals(other) && this._headers.Equals(other._headers)
                && this.endStream == other.endStream && this._padding == other._padding;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + this._headers.GetHashCode();
            hash = hash * 31 + (this.endStream ? 0 : 1);
            hash = hash * 31 + this._padding;
            return hash;
        }
    }
}