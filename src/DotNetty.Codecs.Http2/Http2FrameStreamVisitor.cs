// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    public interface Http2FrameStreamVisitor
    {
        /**
     * This method is called once for each stream of the collection.
     *
     * <p>If an {@link Exception} is thrown, the loop is stopped.
     *
     * @return <ul>
     *         <li>{@code true} if the visitor wants to continue the loop and handle the stream.</li>
     *         <li>{@code false} if the visitor wants to stop handling the stream and abort the loop.</li>
     *         </ul>
     */
        bool visit(Http2FrameStream stream);
    }
}