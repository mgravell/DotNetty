// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    /**
      * Provides an extensibility point for users to define the validity of push requests.
      * @see <a href="https://tools.ietf.org/html/draft-ietf-httpbis-http2-17#section-8.2">[RFC http2], Section 8.2</a>.
      */
    public interface Http2PromisedRequestVerifier
    {
        /**
     * Determine if a {@link Http2Headers} are authoritative for a particular {@link ChannelHandlerContext}.
     * @param ctx The context on which the {@code headers} where received on.
     * @param headers The headers to be verified.
     * @return {@code true} if the {@code ctx} is authoritative for the {@code headers}, {@code false} otherwise.
     * @see
     * <a href="https://tools.ietf.org/html/draft-ietf-httpbis-http2-17#section-10.1">[RFC http2], Section 10.1</a>.
     */
        bool isAuthoritative(IChannelHandlerContext ctx, Http2Headers headers);

        /**
     * Determine if a request is cacheable.
     * @param headers The headers for a push request.
     * @return {@code true} if the request associated with {@code headers} is known to be cacheable,
     * {@code false} otherwise.
     * @see <a href="https://tools.ietf.org/html/rfc7231#section-4.2.3">[RFC 7231], Section 4.2.3</a>.
     */
        bool isCacheable(Http2Headers headers);

        /**
     * Determine if a request is safe.
     * @param headers The headers for a push request.
     * @return {@code true} if the request associated with {@code headers} is known to be safe,
     * {@code false} otherwise.
     * @see <a href="https://tools.ietf.org/html/rfc7231#section-4.2.1">[RFC 7231], Section 4.2.1</a>.
     */
        bool isSafe(Http2Headers headers);
    }
}