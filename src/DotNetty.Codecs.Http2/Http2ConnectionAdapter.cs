// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /**
     * Provides empty implementations of all {@link Http2Connection.Listener} methods.
     */
    public class Http2ConnectionAdapter : Http2ConnectionListener
    {
        public virtual void onStreamAdded(Http2Stream stream)
        {
        }

        public virtual void onStreamActive(Http2Stream stream)
        {
        }

        public void onStreamHalfClosed(Http2Stream stream)
        {
        }

        public virtual void onStreamClosed(Http2Stream stream)
        {
        }

        public virtual void onStreamRemoved(Http2Stream stream)
        {
        }

        public void onGoAwaySent(int lastStreamId, long errorCode, IByteBuffer debugData)
        {
        }

        public void onGoAwayReceived(int lastStreamId, long errorCode, IByteBuffer debugData)
        {
        }
    }
}