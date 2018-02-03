// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
 using System;
 using DotNetty.Transport.Channels;

 /**
     * Implementations of this interface are used to progressively write chunks of the underlying
     * payload to the stream. A payload is considered to be fully written if {@link #write} has
     * been called at least once and it's {@link #size} is now zero.
     */
 public interface FlowControlled 
    {
        /**
         * The size of the payload in terms of bytes applied to the flow-control window.
         * Some payloads like {@code HEADER} frames have no cost against flow control and would
         * return 0 for this value even though they produce a non-zero number of bytes on
         * the wire. Other frames like {@code DATA} frames have both their payload and padding count
         * against flow-control.
         */
        int size();

        /**
         * Called to indicate that an error occurred before this object could be completely written.
         * <p>
         * The {@link Http2RemoteFlowController} will make exactly one call to either
         * this method or {@link #writeComplete()}.
         * </p>
         *
         * @param ctx The context to use if any communication needs to occur as a result of the error.
         * This may be {@code null} if an exception occurs when the connection has not been established yet.
         * @param cause of the error.
         */
        void error(IChannelHandlerContext ctx, Exception cause);

        /**
         * Called after this object has been successfully written.
         * <p>
         * The {@link Http2RemoteFlowController} will make exactly one call to either
         * this method or {@link #error(IChannelHandlerContext, Throwable)}.
         * </p>
         */
        void writeComplete();

        /**
         * Writes up to {@code allowedBytes} of the encapsulated payload to the stream. Note that
         * a value of 0 may be passed which will allow payloads with flow-control size == 0 to be
         * written. The flow-controller may call this method multiple times with different values until
         * the payload is fully written, i.e it's size after the write is 0.
         * <p>
         * When an exception is thrown the {@link Http2RemoteFlowController} will make a call to
         * {@link #error(IChannelHandlerContext, Throwable)}.
         * </p>
         *
         * @param ctx The context to use for writing.
         * @param allowedBytes an upper bound on the number of bytes the payload can write at this time.
         */
        void write(IChannelHandlerContext ctx, int allowedBytes);

        /**
         * Merge the contents of the {@code next} message into this message so they can be written out as one unit.
         * This allows many small messages to be written as a single DATA frame.
         *
         * @return {@code true} if {@code next} was successfully merged and does not need to be enqueued,
         *     {@code false} otherwise.
         */
        bool merge(IChannelHandlerContext ctx, FlowControlled next);
    }

}