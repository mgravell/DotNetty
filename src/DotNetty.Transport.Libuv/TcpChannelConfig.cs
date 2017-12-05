// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System.Runtime.InteropServices;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class TcpChannelConfig : DefaultChannelConfiguration
    {
        bool reuseAddress;
        int receiveBufferSize;
        int sendBufferSize;
        bool noDelay;
        bool keepAlive;

        public TcpChannelConfig(TcpChannel channel) : base(channel)
        {
            this.reuseAddress = true;  // SO_REUSEADDR by default
            this.receiveBufferSize = 0;
            this.sendBufferSize = 0;
            this.noDelay = true; // TCP_NODELAY by default
            this.keepAlive = false;
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.receiveBufferSize;
            }
            if (ChannelOption.SoSndbuf.Equals(option))
            {
                return (T)(object)this.sendBufferSize;
            }
            if (ChannelOption.TcpNodelay.Equals(option))
            {
                return (T)(object)this.noDelay;
            }
            if (ChannelOption.SoKeepalive.Equals(option))
            {
                return (T)(object)this.keepAlive;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.reuseAddress;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            if (base.SetOption(option, value))
            {
                return true;
            }

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.receiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoSndbuf.Equals(option))
            {
                this.sendBufferSize = (int)(object)value;
            }
            else if (ChannelOption.TcpNodelay.Equals(option))
            {
                this.noDelay = (bool)(object)value;
            }
            else if (ChannelOption.SoKeepalive.Equals(option))
            {
                this.keepAlive = (bool)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.reuseAddress = (bool)(object)value;
            }
            else
            {
                return false;
            }

            return true;
        }

        internal void SetOptions(Tcp tcp)
        {
            if (this.receiveBufferSize > 0)
            {
                tcp.ReceiveBufferSize(this.receiveBufferSize);
            }
            if (this.sendBufferSize > 0)
            {
                tcp.SendBufferSize(this.sendBufferSize);
            }
            if (this.noDelay)
            {
                tcp.NoDelay(this.noDelay);
            }
            if (this.keepAlive)
            {
                tcp.KeepAlive(true, 1 /* Delay in seconds */);
            }
            if (this.reuseAddress)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WindowsApi.ReuseAddress(tcp, this.reuseAddress);
                }
                else
                {
                    UnixApi.ReuseAddress(tcp, this.reuseAddress);
                }
            }
        }
    }
}
