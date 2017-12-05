// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv
{
    using System.Runtime.InteropServices;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class TcpServerChannelConfig : DefaultChannelConfiguration
    {
        const int DefaultBacklog = 128;

        bool reuseAddress;
        bool reusePort;
        int receiveBufferSize;
        int backlog;

        public TcpServerChannelConfig(TcpServerChannel channel) : base(channel)
        {
            this.reuseAddress = true; // SO_REUSEADDR by default
            this.reusePort = false;
            this.receiveBufferSize = 0;
            this.backlog = DefaultBacklog;
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.receiveBufferSize;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.reuseAddress;
            }
            if (ChannelOption.SoReuseport.Equals(option))
            {
                return (T)(object)this.reusePort;
            }
            if (ChannelOption.SoBacklog.Equals(option))
            {
                return (T)(object)this.backlog;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.receiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.reuseAddress = (bool)(object)value;
            }
            else if (ChannelOption.SoReuseport.Equals(option))
            {
                this.reusePort = (bool)(object)value;
            }
            else if (ChannelOption.SoBacklog.Equals(option))
            {
                this.backlog = (int)(object)value;
            }
            else
            {
                return base.SetOption(option, value);
            }

            return true;
        }

        public int Backlog => this.backlog;

        internal void SetOptions(TcpListener tcpListener)
        {
            if (this.receiveBufferSize > 0)
            {
                tcpListener.ReceiveBufferSize(this.receiveBufferSize);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows setting SO_REUSEADDR is basically equivalent to 
                // setting SO_REUSEADDR + SO_REUSEPORT on unix. 
                if (this.reuseAddress || this.reusePort)
                {
                    WindowsApi.ReuseAddress(tcpListener, true);
                }
            }
            else
            {
                // https://github.com/libuv/libuv/blob/fd049399aa4ed8495928e375466970d98cb42e17/src/unix/tcp.c#L166
                // Libuv set the SO_REUSEADDR to true automatically in tcp bind
                // only explicitly turn it off when not required
                if (!this.reuseAddress)
                {
                    UnixApi.ReuseAddress(tcpListener, false);
                }
                if (this.reusePort)
                {
                    UnixApi.ReusePort(tcpListener, this.reusePort);
                }
            }
        }
    }
}
