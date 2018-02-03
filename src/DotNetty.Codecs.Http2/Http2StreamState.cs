// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
     * The allowed states of an HTTP2 stream.
     */
    public class Http2StreamState : Tuple<bool, bool>
    {
        public static readonly Http2StreamState IDLE = new Http2StreamState(false, false);
        public static readonly Http2StreamState RESERVED_LOCAL = new Http2StreamState(false, false);
        public static readonly Http2StreamState RESERVED_REMOTE = new Http2StreamState(false, false);
        public static readonly Http2StreamState OPEN = new Http2StreamState(true, true);
        public static readonly Http2StreamState HALF_CLOSED_LOCAL = new Http2StreamState(false, true);
        public static readonly Http2StreamState HALF_CLOSED_REMOTE = new Http2StreamState(true, false);
        public static readonly Http2StreamState CLOSED = new Http2StreamState(false, false);

        /**
         * Indicates whether the local side of this stream is open (i.e. the state is either
         * {@link State#OPEN} or {@link State#HALF_CLOSED_REMOTE}).
         */
        public bool LocalSideOpen => this.Item1;

        /**
         * Indicates whether the remote side of this stream is open (i.e. the state is either
         * {@link State#OPEN} or {@link State#HALF_CLOSED_LOCAL}).
         */
        public bool RemoteSideOpen => this.Item2;

        public Http2StreamState(bool localSideOpen, bool remoteSideOpen)
            : base(localSideOpen, remoteSideOpen)
        {
        }
    }
}