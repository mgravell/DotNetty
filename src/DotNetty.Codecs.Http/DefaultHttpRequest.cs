// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;
    using System.Text;
    
    public class DefaultHttpRequest : DefaultHttpMessage, IHttpRequest
    {
        const int HashCodePrime = 31;

        HttpMethod method;
        string uri;

        public DefaultHttpRequest(HttpVersion version, HttpMethod method, string uri, bool validateHeaders = true)
            : base(version, validateHeaders)
        {
            Contract.Requires(method != null);
            Contract.Requires(uri != null);

            this.method = method;
            this.uri = uri;
        }

        public DefaultHttpRequest(HttpVersion version, HttpMethod method, string uri, HttpHeaders headers) 
            : base(version, headers)
        {
            Contract.Requires(method != null);
            Contract.Requires(uri != null);

            this.method = method;
            this.uri = uri;
        }

        public HttpMethod Method
        {
            get => this.method;
            set
            {
                Contract.Requires(value != null);
                this.method = value;
            }
        }

        public string Uri
        {
            get => this.uri;
            set
            {
                Contract.Requires(value != null);
                this.uri = value;
            }
        }

        // ReSharper disable NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
        {
            int result = 1;
            result = HashCodePrime * result + this.method.GetHashCode();
            result = HashCodePrime * result + this.uri.GetHashCode();
            result = HashCodePrime * result + base.GetHashCode();

            return result;
        }
        // ReSharper restore NonReadonlyMemberInGetHashCode

        public override bool Equals(object obj)
        {
            if (obj is DefaultHttpRequest other)
            {
                return this.method.Equals(other.method) && this.uri.Equals(other.uri) 
                    && base.Equals(obj);
            }

            return false;
        }

        public override string ToString() => HttpMessageUtil.AppendRequest(new StringBuilder(256), this).ToString();
    }
}
