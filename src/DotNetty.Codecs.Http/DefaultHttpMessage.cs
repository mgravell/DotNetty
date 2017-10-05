// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;

    public abstract class DefaultHttpMessage : DefaultHttpObject, IHttpMessage
    {
        const int HashCodePrime = 31;
        HttpVersion version;
        readonly HttpHeaders headers;

        protected DefaultHttpMessage(HttpVersion version, bool validateHeaders = true, bool singleFieldHeaders = false)
        {
            Contract.Requires(version != null);

            this.version = version;
            this.headers = singleFieldHeaders 
                ? new CombinedHttpHeaders(validateHeaders) 
                : new DefaultHttpHeaders(validateHeaders);
        }

        protected DefaultHttpMessage(HttpVersion version, HttpHeaders headers)
        {
            Contract.Requires(version != null);
            Contract.Requires(headers != null);

            this.version = version;
            this.headers = headers;
        }

        public HttpHeaders Headers => this.headers;

        public HttpVersion ProtocolVersion
        {
            get => this.version;
            set
            {
                Contract.Requires(value != null);
                this.version = value;
            }
        }

        public override int GetHashCode()
        {
            int result = 1;
            result = HashCodePrime * result + this.headers.GetHashCode();
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            result = HashCodePrime * result + this.version.GetHashCode();
            result = HashCodePrime * result + base.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj is DefaultHttpMessage other)
            {
                return this.headers.Equals(other.headers) && this.version.Equals(other.version) 
                    && base.Equals(obj);
            }

            return false;
        }
    }
}
