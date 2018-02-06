// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    public sealed class HeaderListSizeException : StreamException
    {
        static readonly long serialVersionUID = -8807603212183882637L;

        readonly bool decode;

        public HeaderListSizeException(int streamId, Http2Error error, string message, bool decode)
            : base(streamId, error, message)
        {
            this.decode = decode;
        }

        public bool duringDecode()
        {
            return this.decode;
        }
    }
}