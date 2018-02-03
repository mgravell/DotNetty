// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    /**
     * A default implementation of {@link Http2PromisedRequestVerifier} which always returns positive responses for
     * all verification challenges.
     */
    public class AlwaysVerifyPromisedRequestVerifier : Http2PromisedRequestVerifier
    {
        public bool isAuthoritative(IChannelHandlerContext ctx, Http2Headers headers)
        {
            return true;
        }

        public bool isCacheable(Http2Headers headers)
        {
            return true;
        }

        public bool isSafe(Http2Headers headers)
        {
            return true;
        }
    }
}