// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
     * Abstract implementation of {@link Http2StreamFrame}.
     */
    public abstract class AbstractHttp2StreamFrame : Http2StreamFrame
    {
        Http2FrameStream _stream;
        
        public abstract string name();

        public Http2StreamFrame stream(Http2FrameStream stream)
        {
            this._stream = stream;
            return this;
        }

        public Http2FrameStream stream()
        {
            return this._stream;
        }

        /**
         * Returns {@code true} if {@code o} has equal {@code stream} to this object.
         */
        public override bool Equals(object o)
        {
            if (!(o is Http2StreamFrame))
            {
                return false;
            }

            Http2StreamFrame other = (Http2StreamFrame)o;
            return this._stream == other.stream() || (this._stream != null && this._stream.Equals(other.stream()));
        }

        public override int GetHashCode()
        {
            Http2FrameStream stream = this._stream;
            if (stream == null)
            {
                return base.GetHashCode();
            }

            return stream.GetHashCode();
        }
    }
}