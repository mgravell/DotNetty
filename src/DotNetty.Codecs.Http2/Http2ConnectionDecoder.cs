// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /**
     * Handler for inbound traffic on behalf of {@link Http2ConnectionHandler}. Performs basic protocol
     * conformance on inbound frames before calling the delegate {@link Http2FrameListener} for
     * application-specific processing. Note that frames of an unknown type (i.e. HTTP/2 extensions)
     * will skip all protocol checks and be given directly to the listener for processing.
     */

    public interface Http2ConnectionDecoder : IDisposable
    {
        /**
           * Sets the lifecycle manager. Must be called as part of initialization before the decoder is used.
           */
        void lifecycleManager(Http2LifecycleManager lifecycleManager);

        /**
           * Provides direct access to the underlying connection.
           */
        Http2Connection connection();

        /**
           * Provides the local flow controller for managing inbound traffic.
           */
        Http2LocalFlowController flowController();

        /**
           * Set the {@link Http2FrameListener} which will be notified when frames are decoded.
           * <p>
           * This <strong>must</strong> be set before frames are decoded.
           */
        void frameListener(Http2FrameListener listener);

        /**
           * Get the {@link Http2FrameListener} which will be notified when frames are decoded.
           */
        Http2FrameListener frameListener();

        /**
           * Called by the {@link Http2ConnectionHandler} to decode the next frame from the input buffer.
           */
        void decodeFrame(IChannelHandlerContext ctx, IByteBuffer @in, List<object> @out);

        /**
           * Gets the local settings for this endpoint of the HTTP/2 connection.
           */
        Http2Settings localSettings();

        /**
           * Indicates whether or not the first initial {@code SETTINGS} frame was received from the remote endpoint.
           */
        bool prefaceReceived();
    }
}