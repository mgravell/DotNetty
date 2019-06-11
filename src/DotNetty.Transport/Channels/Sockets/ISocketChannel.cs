// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Threading.Tasks;
    
    public interface ISocketChannel : IChannel
    {
        bool InputShutdown { get; }

        Task ShutdownInputAsync();
        
        bool OutputShutdown { get; }

        Task ShutdownOutputAsync();
    }
}