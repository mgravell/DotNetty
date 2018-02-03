// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * Handler for outbound HTTP/2 traffic.
    */
    public interface Http2ConnectionEncoder : Http2FrameWriter
    {
        /**
         * Sets the lifecycle manager. Must be called as part of initialization before the encoder is used.
         */
        void lifecycleManager(Http2LifecycleManager lifecycleManager);

        /**
         * Provides direct access to the underlying connection.
         */
        Http2Connection connection();

        /**
         * Provides the remote flow controller for managing outbound traffic.
         */
        Http2RemoteFlowController flowController();

        /**
         * Provides direct access to the underlying frame writer object.
         */
        Http2FrameWriter frameWriter();

        /**
         * Gets the local settings on the top of the queue that has been sent but not ACKed. This may
         * return {@code null}.
         */
        Http2Settings pollSentSettings();

        /**
         * Sets the settings for the remote endpoint of the HTTP/2 connection.
         */
        void remoteSettings(Http2Settings settings);
    }
}