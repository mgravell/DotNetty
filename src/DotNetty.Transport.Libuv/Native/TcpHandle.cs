// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Runtime.InteropServices;

    abstract unsafe class TcpHandle : NativeHandle
    {
        protected TcpHandle(Loop loop) : base(uv_handle_type.UV_TCP)
        {
            Debug.Assert(loop != null);

            IntPtr handle = NativeMethods.Allocate(uv_handle_type.UV_TCP);

            try
            {
                int result = NativeMethods.uv_tcp_init(loop.Handle, handle);
                NativeMethods.ThrowIfError(result);
            }
            catch
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_handle_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);
            this.Handle = handle;
        }

        internal void Bind(IPEndPoint endPoint, bool dualStack = false)
        {
            Debug.Assert(endPoint != null);

            this.Validate();
            NativeMethods.GetSocketAddress(endPoint, out sockaddr addr);
            int result = NativeMethods.uv_tcp_bind(this.Handle, ref addr, (uint)(dualStack ? 1 : 0));
            NativeMethods.ThrowIfError(result);
        }

        public IPEndPoint GetLocalEndPoint()
        {
            this.Validate();
            return NativeMethods.TcpGetSocketName(this.Handle);
        }

        public void NoDelay(bool value)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_nodelay(this.Handle, value ? 1 : 0);
            NativeMethods.ThrowIfError(result);
        }

        public int SendBufferSize(int value)
        {
            Contract.Requires(value >= 0);

            this.Validate();
            var size = (IntPtr)value;
            return NativeMethods.uv_send_buffer_size(this.Handle, ref size);
        }

        public int ReceiveBufferSize(int value)
        {
            Contract.Requires(value >= 0);

            this.Validate();
            var size = (IntPtr)value;
            return NativeMethods.uv_recv_buffer_size(this.Handle, ref size);
        }

        public void KeepAlive(bool value, int delay)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_keepalive(this.Handle, value ? 1 : 0, delay);
            NativeMethods.ThrowIfError(result);
        }

        public void SimultaneousAccepts(bool value)
        {
            this.Validate();
            int result = NativeMethods.uv_tcp_simultaneous_accepts(this.Handle, value ? 1 : 0);
            NativeMethods.ThrowIfError(result);
        }
    }
}
