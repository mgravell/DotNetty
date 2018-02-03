// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;

    /**
     * HTTP/2 pseudo-headers names.
     */
    public class PseudoHeaderName
    {
        /**
         * {@code :method}.
         */
        public static readonly PseudoHeaderName METHOD = new PseudoHeaderName(":method");

        /**
         * {@code :scheme}.
         */
        public static readonly PseudoHeaderName SCHEME = new PseudoHeaderName(":scheme");

        /**
         * {@code :authority}.
         */
        public static readonly PseudoHeaderName AUTHORITY = new PseudoHeaderName(":authority");

        /**
         * {@code :path}.
         */
        public static readonly PseudoHeaderName PATH = new PseudoHeaderName(":path");

        /**
         * {@code :status}.
         */
        public static readonly PseudoHeaderName STATUS = new PseudoHeaderName(":status");
        
        static readonly PseudoHeaderName[] All  = { METHOD, SCHEME, AUTHORITY, PATH, STATUS};

        readonly AsciiString _value;

        static readonly ISet<ICharSequence> PSEUDO_HEADERS = new HashSet<ICharSequence>();

        static PseudoHeaderName()
        {
            foreach (PseudoHeaderName pseudoHeader in All)
            {
                PSEUDO_HEADERS.Add(pseudoHeader.value());
            }
        }

        PseudoHeaderName(string value)
        {
            this._value = AsciiString.Cached(value);
        }

        public AsciiString value()
        {
            // Return a slice so that the buffer gets its own reader index.
            return _value;
        }

        /**
         * Indicates whether the given header name is a valid HTTP/2 pseudo header.
         */
        public static bool isPseudoHeader(ICharSequence header)
        {
            return PSEUDO_HEADERS.Contains(header);
        }
    }
}