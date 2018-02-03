// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
     * The default {@link Http2WindowUpdateFrame} implementation.
     */
    public class DefaultHttp2WindowUpdateFrame : AbstractHttp2StreamFrame, Http2WindowUpdateFrame
    {
        readonly int windowUpdateIncrement;

        public DefaultHttp2WindowUpdateFrame(int windowUpdateIncrement)
        {
            this.windowUpdateIncrement = windowUpdateIncrement;
        }

        public override string name()
        {
            return "WINDOW_UPDATE";
        }

        public int windowSizeIncrement()
        {
            return this.windowUpdateIncrement;
        }
    }
}