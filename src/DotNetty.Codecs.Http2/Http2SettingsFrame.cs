// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
     * HTTP/2 SETTINGS frame.
     */
    public interface Http2SettingsFrame : Http2Frame
    {
        Http2Settings settings();
    }
}