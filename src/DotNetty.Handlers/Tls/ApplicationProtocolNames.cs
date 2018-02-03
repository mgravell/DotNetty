// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{

    /**
     * Provides a set of protocol names used in ALPN and NPN.
     *
     * @see <a href="https://tools.ietf.org/html/rfc7540#section-11.1">RFC7540 (HTTP/2)</a>
     * @see <a href="https://tools.ietf.org/html/rfc7301#section-6">RFC7301 (TLS ALPN Extension)</a>
     * @see <a href="https://tools.ietf.org/html/draft-agl-tls-nextprotoneg-04#section-7">TLS NPN Extension Draft</a>
     */
    public static class ApplicationProtocolNames 
    {

        /**
         * {@code "h2"}: HTTP version 2
         */
        public static readonly string HTTP_2 = "h2";

        /**
         * {@code "http/1.1"}: HTTP version 1.1
         */
        public static readonly string HTTP_1_1 = "http/1.1";

        /**
         * {@code "spdy/3.1"}: SPDY version 3.1
         */
        public static readonly string SPDY_3_1 = "spdy/3.1";

        /**
         * {@code "spdy/3"}: SPDY version 3
         */
        public static readonly string SPDY_3 = "spdy/3";

        /**
         * {@code "spdy/2"}: SPDY version 2
         */
        public static readonly string SPDY_2 = "spdy/2";

        /**
         * {@code "spdy/1"}: SPDY version 1
         */
        public static readonly string SPDY_1 = "spdy/1";
    }
}