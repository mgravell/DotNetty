// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    /**
     * The default {@link Http2SettingsFrame} implementation.
     */
    public class DefaultHttp2SettingsFrame : Http2SettingsFrame
    {
        readonly Http2Settings _settings;

        public DefaultHttp2SettingsFrame(Http2Settings settings)
        {
            Contract.Requires(settings != null);
            this._settings = settings;
        }

        public Http2Settings settings()
        {
            return this._settings;
        }

        public string name()
        {
            return "SETTINGS";
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(settings=" + this._settings + ')';
        }
    }
}