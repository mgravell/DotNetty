// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /** An HTTP/2 frame. */
    public interface Http2Frame
    {
        /**
         * Returns the name of the HTTP/2 frame e.g. DATA, GOAWAY, etc.
         */
        string name();
    }
}