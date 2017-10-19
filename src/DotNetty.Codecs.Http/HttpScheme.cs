// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    public sealed class HttpScheme
    {
        readonly int port;
        readonly AsciiString name;

        HttpScheme(int port, string name)
        {
            this.port = port;
            this.name = new AsciiString(name);
        }

        public AsciiString Name => this.name;

        public int Port => this.port;

        public override bool Equals(object obj)
        {
            if (obj is HttpScheme other)
            {
                return other.port == this.port && other.name.Equals(this.name);
            }

            return false;
        }

        public override int GetHashCode() => this.port * 31 + this.name.GetHashCode();

        public override string ToString() => this.name.ToString();
    }
}
