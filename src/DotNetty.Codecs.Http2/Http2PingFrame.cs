// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    public interface Http2PingFrame : Http2Frame, IByteBufferHolder
    {
        /**
           * When {@code true}, indicates that this ping is a ping response.
           */
        bool ack();
    }
}