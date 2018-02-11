// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    static class HpackStaticTable
    {
        // Appendix A: Static Table
        // http://tools.ietf.org/html/rfc7541#appendix-A
        static readonly HpackHeaderField[] STATIC_TABLE = 
        {
            /*  1 */ newEmptyHeaderField(":authority"),
            /*  2 */ newHeaderField(":method", "GET"),
            /*  3 */ newHeaderField(":method", "POST"),
            /*  4 */ newHeaderField(":path", "/"),
            /*  5 */ newHeaderField(":path", "/index.html"),
            /*  6 */ newHeaderField(":scheme", "http"),
            /*  7 */ newHeaderField(":scheme", "https"),
            /*  8 */ newHeaderField(":status", "200"),
            /*  9 */ newHeaderField(":status", "204"),
            /* 10 */ newHeaderField(":status", "206"),
            /* 11 */ newHeaderField(":status", "304"),
            /* 12 */ newHeaderField(":status", "400"),
            /* 13 */ newHeaderField(":status", "404"),
            /* 14 */ newHeaderField(":status", "500"),
            /* 15 */ newEmptyHeaderField("accept-charset"),
            /* 16 */ newHeaderField("accept-encoding", "gzip, deflate"),
            /* 17 */ newEmptyHeaderField("accept-language"),
            /* 18 */ newEmptyHeaderField("accept-ranges"),
            /* 19 */ newEmptyHeaderField("accept"),
            /* 20 */ newEmptyHeaderField("access-control-allow-origin"),
            /* 21 */ newEmptyHeaderField("age"),
            /* 22 */ newEmptyHeaderField("allow"),
            /* 23 */ newEmptyHeaderField("authorization"),
            /* 24 */ newEmptyHeaderField("cache-control"),
            /* 25 */ newEmptyHeaderField("content-disposition"),
            /* 26 */ newEmptyHeaderField("content-encoding"),
            /* 27 */ newEmptyHeaderField("content-language"),
            /* 28 */ newEmptyHeaderField("content-length"),
            /* 29 */ newEmptyHeaderField("content-location"),
            /* 30 */ newEmptyHeaderField("content-range"),
            /* 31 */ newEmptyHeaderField("content-type"),
            /* 32 */ newEmptyHeaderField("cookie"),
            /* 33 */ newEmptyHeaderField("date"),
            /* 34 */ newEmptyHeaderField("etag"),
            /* 35 */ newEmptyHeaderField("expect"),
            /* 36 */ newEmptyHeaderField("expires"),
            /* 37 */ newEmptyHeaderField("from"),
            /* 38 */ newEmptyHeaderField("host"),
            /* 39 */ newEmptyHeaderField("if-match"),
            /* 40 */ newEmptyHeaderField("if-modified-since"),
            /* 41 */ newEmptyHeaderField("if-none-match"),
            /* 42 */ newEmptyHeaderField("if-range"),
            /* 43 */ newEmptyHeaderField("if-unmodified-since"),
            /* 44 */ newEmptyHeaderField("last-modified"),
            /* 45 */ newEmptyHeaderField("link"),
            /* 46 */ newEmptyHeaderField("location"),
            /* 47 */ newEmptyHeaderField("max-forwards"),
            /* 48 */ newEmptyHeaderField("proxy-authenticate"),
            /* 49 */ newEmptyHeaderField("proxy-authorization"),
            /* 50 */ newEmptyHeaderField("range"),
            /* 51 */ newEmptyHeaderField("referer"),
            /* 52 */ newEmptyHeaderField("refresh"),
            /* 53 */ newEmptyHeaderField("retry-after"),
            /* 54 */ newEmptyHeaderField("server"),
            /* 55 */ newEmptyHeaderField("set-cookie"),
            /* 56 */ newEmptyHeaderField("strict-transport-security"),
            /* 57 */ newEmptyHeaderField("transfer-encoding"),
            /* 58 */ newEmptyHeaderField("user-agent"),
            /* 59 */ newEmptyHeaderField("vary"),
            /* 60 */ newEmptyHeaderField("via"),
            /* 61 */ newEmptyHeaderField("www-authenticate")
        };

        static HpackHeaderField newEmptyHeaderField(string name)
        {
            return new HpackHeaderField(AsciiString.Cached(name), AsciiString.Empty);
        }

        static HpackHeaderField newHeaderField(string name, string value)
        {
            return new HpackHeaderField(AsciiString.Cached(name), AsciiString.Cached(value));
        }

        static readonly CharSequenceMap<int> STATIC_INDEX_BY_NAME = createMap();

        /**
     * The number of header fields in the static table.
     */
        internal static readonly int length = STATIC_TABLE.Length;

        /**
     * Return the header field at the given index value.
     */
        internal static HpackHeaderField getEntry(int index)
        {
            return STATIC_TABLE[index - 1];
        }

        /**
     * Returns the lowest index value for the given header field name in the static table. Returns
     * -1 if the header field name is not in the static table.
     */
        internal static int getIndex(ICharSequence name)
        {
            int index = STATIC_INDEX_BY_NAME.Get(name);
            if (index == null)
            {
                return -1;
            }

            return index;
        }

        /**
     * Returns the index value for the given header field in the static table. Returns -1 if the
     * header field is not in the static table.
     */
        internal static int getIndex(ICharSequence name, ICharSequence value)
        {
            int index = getIndex(name);
            if (index == -1)
            {
                return -1;
            }

            // Note this assumes all entries for a given header field are sequential.
            while (index <= length)
            {
                HpackHeaderField entry = getEntry(index);
                if (HpackUtil.equalsConstantTime(name, entry.name) == 0)
                {
                    break;
                }

                if (HpackUtil.equalsConstantTime(value, entry.value) != 0)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        // create a map CharSequenceMap header name to index value to allow quick lookup
        static CharSequenceMap<int> createMap()
        {
            int length = STATIC_TABLE.Length;
            CharSequenceMap<int> ret = new CharSequenceMap<int>(true, UnsupportedValueConverter<int>.Instance, length);
            // Iterate through the static table in reverse order to
            // save the smallest index for a given name in the map.
            for (int index = length; index > 0; index--)
            {
                HpackHeaderField entry = getEntry(index);
                ICharSequence name = entry.name;
                ret.Set(name, index);
            }

            return ret;
        }
    }
}