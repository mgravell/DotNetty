// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Proxy
{
    using System;
    using DotNetty.Transport.Channels;

    public class ProxyConnectException : ConnectException
    {
        public ProxyConnectException(string msg) : base(msg, null)
        { }

        public ProxyConnectException(Exception cause) :base(null, cause)
        {
        }
        
        public ProxyConnectException(string message, Exception innerException)  : base(message, innerException)
        {
        }
    }
}