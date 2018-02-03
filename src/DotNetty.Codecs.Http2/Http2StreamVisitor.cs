// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    public interface Http2StreamVisitor
    {
        /**
     * @return <ul>
     *         <li>{@code true} if the visitor wants to continue the loop and handle the entry.</li>
     *         <li>{@code false} if the visitor wants to stop handling headers and abort the loop.</li>
     *         </ul>
     */
        bool visit(Http2Stream stream);
    }
}