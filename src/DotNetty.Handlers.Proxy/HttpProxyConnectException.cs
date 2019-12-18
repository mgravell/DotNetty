// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Proxy
{
    using DotNetty.Codecs.Http;

    /**
     * Specific case of a connection failure, which may include headers from the proxy.
     */
    public sealed class HttpProxyConnectException : ProxyConnectException 
    {
        /**
         * @param message The failure message.
         * @param headers Header associated with the connection failure.  May be {@code null}.
         */
        public HttpProxyConnectException(string message, HttpHeaders headers)
            : base(message)
        {
            this.Headers = headers;
        }

        /**
         * Returns headers, if any.  May be {@code null}.
         */
        public HttpHeaders Headers { get; }
    }
}