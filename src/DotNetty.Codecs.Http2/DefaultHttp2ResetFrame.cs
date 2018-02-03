// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    public sealed class DefaultHttp2ResetFrame : AbstractHttp2StreamFrame, Http2ResetFrame
    {
        readonly ulong _errorCode;

        /**
         * Construct a reset message.
         *
         * @param error the non-{@code null} reason for reset
         */
        public DefaultHttp2ResetFrame(Http2Error error)
        {
            this._errorCode = (ulong)error;
        }

        /**
         * Construct a reset message.
         *
         * @param errorCode the reason for reset
         */
        public DefaultHttp2ResetFrame(ulong errorCode)
        {
            this._errorCode = errorCode;
        }

        public override string name()
        {
            return "RST_STREAM";
        }

        public ulong errorCode()
        {
            return this._errorCode;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + this.stream() + ", errorCode=" + this._errorCode + ')';
        }

        public override bool Equals(object o)
        {
            if (!(o is DefaultHttp2ResetFrame))
            {
                return false;
            }

            DefaultHttp2ResetFrame other = (DefaultHttp2ResetFrame)o;
            return base.Equals(o) && this._errorCode == other._errorCode;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = hash * 31 + (int)(this._errorCode ^ (this._errorCode >> 32));
            return hash;
        }
    }
}