// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Proxy
{
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Text;

    public sealed class ProxyConnectionEvent
    {
        string strVal;

        /**
         * Creates a new event that indicates a successful connection attempt to the destination address.
         */
        public ProxyConnectionEvent(string protocol, string authScheme, EndPoint proxyAddress, EndPoint destinationAddress)
        {
            Contract.Requires(protocol != null);
            Contract.Requires(authScheme != null);
            Contract.Requires(proxyAddress != null);
            Contract.Requires(destinationAddress != null);

            this.Protocol = protocol;
            this.AuthScheme = authScheme;
            this.ProxyAddress = proxyAddress;
            this.DestinationAddress = destinationAddress;
        }

        /**
         * Returns the name of the proxy protocol in use.
         */
        public string Protocol { get; }

        /**
         * Returns the name of the authentication scheme in use.
         */
        public string AuthScheme { get; }

        /**
         * Returns the address of the proxy server.
         */
        public EndPoint ProxyAddress { get; }

        /**
         * Returns the address of the destination.
         */
        public EndPoint DestinationAddress { get; }

        public override string ToString()
        {
            if (this.strVal != null)
            {
                return this.strVal;
            }

            StringBuilder buf = new StringBuilder(128)
                .Append(typeof(ProxyConnectionEvent).Name)
                .Append('(')
                .Append(this.Protocol)
                .Append(", ")
                .Append(this.AuthScheme)
                .Append(", ")
                .Append(this.ProxyAddress)
                .Append(" => ")
                .Append(this.DestinationAddress)
                .Append(')');

            return this.strVal = buf.ToString();
        }
    }
}