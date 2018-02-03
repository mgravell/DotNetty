// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
 * Listener to the number of flow-controlled bytes written per stream.
 */
    public interface Http2FlowControlListener
    {
        /**
         * Notification that {@link Http2RemoteFlowController#isWritable(Http2Stream)} has changed for {@code stream}.
         * <p>
         * This method should not throw. Any thrown exceptions are considered a programming error and are ignored.
         * @param stream The stream which writability has changed for.
         */
        void writabilityChanged(Http2Stream stream);
    }
}