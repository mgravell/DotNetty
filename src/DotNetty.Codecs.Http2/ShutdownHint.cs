// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
     * Provides a hint as to if shutdown is justified, what type of shutdown should be executed.
     */
    public enum ShutdownHint
    {
        /**
         * Do not shutdown the underlying channel.
         */
        NO_SHUTDOWN,

        /**
         * Attempt to execute a "graceful" shutdown. The definition of "graceful" is left to the implementation.
         * An example of "graceful" would be wait for some amount of time until all active streams are closed.
         */
        GRACEFUL_SHUTDOWN,

        /**
         * Close the channel immediately after a {@code GOAWAY} is sent.
         */
        HARD_SHUTDOWN
    }
}