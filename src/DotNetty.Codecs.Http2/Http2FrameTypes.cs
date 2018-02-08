// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * Registry of all standard frame types defined by the HTTP/2 specification.
    */
    public enum Http2FrameTypes : byte
    {
        DATA = 0x0,
        HEADERS = 0x1,
        PRIORITY = 0x2,
        RST_STREAM = 0x3,
        SETTINGS = 0x4,
        PUSH_PROMISE = 0x5,
        PING = 0x6,
        GO_AWAY = 0x7,
        WINDOW_UPDATE = 0x8,
        CONTINUATION = 0x9
    }
}