// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
     * The allowed states of an HTTP2 stream.
     */
    public class Http2StreamState
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
        public bool localSideOpen() => this.Item1;

        /**
         * Indicates whether the remote side of this stream is open (i.e. the state is either
         * {@link State#OPEN} or {@link State#HALF_CLOSED_LOCAL}).
         */
        public bool remoteSideOpen() => this.Item2;

        readonly bool Item1;
        readonly bool Item2;

        Http2StreamState(bool localSideOpen, bool remoteSideOpen)
        {
            this.Item1 = localSideOpen;
            this.Item2 = remoteSideOpen;
        }
/*
        public static bool operator ==(Http2StreamState left, Http2StreamState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null))
            {
                return false;
            }
            
            if (ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }
        

        public static bool operator !=(Http2StreamState left, Http2StreamState right) => !(left == right);

        public bool Equals(Http2StreamState other) => this.Equals((object)other);*/
        
    }
}