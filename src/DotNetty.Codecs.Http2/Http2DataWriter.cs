// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public interface Http2DataWriter
    {
        /**
           * Writes a {@code DATA} frame to the remote endpoint. This will result in one or more
           * frames being written to the context.
           *
           * @param ctx the context to use for writing.
           * @param streamId the stream for which to send the frame.
           * @param data the payload of the frame. This will be released by this method.
           * @param padding additional bytes that should be added to obscure the true content size. Must be between 0 and
           *                256 (inclusive). A 1 byte padding is encoded as just the pad length field with value 0.
           *                A 256 byte padding is encoded as the pad length field with value 255 and 255 padding bytes
           *                appended to the end of the frame.
           * @param endStream indicates if this is the last frame to be sent for the stream.
           * @param promise the promise for the write.
           * @return the future for the write.
           */
        Task writeData(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endStream, TaskCompletionSource promise);
    }
}