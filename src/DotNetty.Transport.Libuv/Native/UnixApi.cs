// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
// ReSharper disable RedundantAssignment
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;

    static class UnixApi
    {
        [DllImport("libc", SetLastError = true)]
        static extern int setsockopt(int socket, int level, int option_name, IntPtr option_value, uint option_len);

        const int SOL_SOCKET_LINUX = 0x0001;
        const int SO_REUSEADDR_LINUX = 0x0002;
        const int SO_REUSEPORT_LINUX = 0x000f;

        const int SOL_SOCKET_OSX = 0xffff;
        const int SO_REUSEADDR_OSX = 0x0004;
        const int SO_REUSEPORT_OSX = 0x0200;

        internal static unsafe void ReuseAddress(NativeHandle handle, bool value)
        {
            IntPtr socket = IntPtr.Zero;
            NativeMethods.uv_fileno(handle.Handle, ref socket);
            int optionValue = value ? 1 : 0;

            int status = 0;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                status = setsockopt(socket.ToInt32(), SOL_SOCKET_LINUX, SO_REUSEADDR_LINUX, (IntPtr)(&optionValue), sizeof(int));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                status = setsockopt(socket.ToInt32(), SOL_SOCKET_OSX, SO_REUSEADDR_OSX, (IntPtr)(&optionValue), sizeof(int));
            }
            if (status != 0)
            {
                throw new SocketException(Marshal.GetLastWin32Error());
            }
        }

        internal static unsafe void ReusePort(NativeHandle handle, bool value)
        {
            IntPtr socket = IntPtr.Zero;
            NativeMethods.uv_fileno(handle.Handle, ref socket);
            int optionValue = value ? 1 : 0;

            int status = 0;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                status = setsockopt(socket.ToInt32(), SOL_SOCKET_LINUX, SO_REUSEPORT_LINUX, (IntPtr)(&optionValue), sizeof(int));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                status = setsockopt(socket.ToInt32(), SOL_SOCKET_OSX, SO_REUSEPORT_OSX, (IntPtr)(&optionValue), sizeof(int));
            }
            if (status != 0)
            {
                throw new SocketException(Marshal.GetLastWin32Error());
            }
        }
    }
}
