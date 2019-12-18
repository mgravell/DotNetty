// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Proxy
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Base64;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class HttpProxyHandler : ProxyHandler
    {
        static readonly string PROTOCOL = "http";
        static readonly string AuthBasic = "basic";

        readonly HttpClientCodec codec = new HttpClientCodec();
        readonly string username;
        readonly string password;
        readonly ICharSequence authorization;
        readonly HttpHeaders outboundHeaders;
        readonly bool ignoreDefaultPortsInConnectHostHeader;
        HttpResponseStatus status;
        HttpHeaders inboundHeaders;

        public HttpProxyHandler(EndPoint proxyAddress)
            : this(proxyAddress, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers)
            : this(proxyAddress, headers, false)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers, bool ignoreDefaultPortsInConnectHostHeader)
            : base(proxyAddress)
        {
            username = null;
            password = null;
            authorization = null;
            this.outboundHeaders = headers;
            this.ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password)
            : this(proxyAddress, username, password, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password, HttpHeaders headers)
            : this(proxyAddress, username, password, headers, false)
        {
        }

        public HttpProxyHandler(
            EndPoint proxyAddress,
            string username,
            string password,
            HttpHeaders headers,
            bool ignoreDefaultPortsInConnectHostHeader)
            : base(proxyAddress)
        {
            Contract.Requires(username != null);
            Contract.Requires(password != null);

            IByteBuffer authz = Unpooled.CopiedBuffer(username + ':' + password, Encoding.UTF8);
            IByteBuffer authzBase64 = Base64.Encode(authz, false);

            authorization = new AsciiString("Basic " + authzBase64.ToString(Encoding.ASCII));

            authz.Release();
            authzBase64.Release();

            this.outboundHeaders = headers;
            this.ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public override string Protocol => PROTOCOL;

        public override string AuthScheme => authorization != null ? AuthBasic : AuthNone;

        public string Username => this.username;

        public string Password => this.password;

        protected override void AddCodec(IChannelHandlerContext ctx)
        {
            IChannelPipeline p = ctx.Channel.Pipeline;
            string name = ctx.Name;
            p.AddBefore(name, null, codec);
        }

        protected override void RemoveEncoder(IChannelHandlerContext ctx)
        {
            codec.RemoveOutboundHandler();
        }

        protected override void RemoveDecoder(IChannelHandlerContext ctx)
        {
            codec.RemoveInboundHandler();
        }

        protected override object NewInitialMessage(IChannelHandlerContext ctx)
        {
            if (!TryParseEndpoint(this.DestinationAddress, out string hostnameString, out int port))
            {
                throw new NotSupportedException($"Endpoint {this.DestinationAddress} is not supported as http proxy destination");
            }
            
            string url = hostnameString + ":" + port;
            string hostHeader = this.ignoreDefaultPortsInConnectHostHeader && (port == 80 || port == 443) ? hostnameString : url;

            IFullHttpRequest req = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Connect, url, Unpooled.Empty, false);

            req.Headers.Set(HttpHeaderNames.Host, hostHeader);

            if (authorization != null)
            {
                req.Headers.Set(HttpHeaderNames.ProxyAuthorization, authorization);
            }

            if (outboundHeaders != null)
            {
                req.Headers.Add(outboundHeaders);
            }

            return req;
        }

        protected override bool HandleResponse(IChannelHandlerContext ctx, object response)
        {
            if (response is IHttpResponse)
            {
                if (status != null)
                {
                    throw new HttpProxyConnectException(this.ExceptionMessage("too many responses"), /*headers=*/ null);
                }

                IHttpResponse res = (IHttpResponse)response;
                status = res.Status;
                inboundHeaders = res.Headers;
            }

            bool finished = response is ILastHttpContent;
            if (finished)
            {
                if (status == null)
                {
                    throw new HttpProxyConnectException(this.ExceptionMessage("missing response"), inboundHeaders);
                }

                if (status.Code != 200)
                {
                    throw new HttpProxyConnectException(this.ExceptionMessage("status: " + status), inboundHeaders);
                }
            }

            return finished;
        }
        
        /**
         * Formats the host string of an address so it can be used for computing an HTTP component
         * such as a URL or a Host header
         *
         * @param addr the address
         * @return the formatted String
         */
        static bool TryParseEndpoint(EndPoint addr, out string hostnameString, out int port) 
        {
            if (addr is DnsEndPoint eDns)
            {
                hostnameString = eDns.Host;
                port = eDns.Port;
                return true;
            } 
            else if (addr is IPEndPoint eIp)
            {
                hostnameString = eIp.Address.ToString();
                port = eIp.Port;
                return true;
            }
            else
            {
                hostnameString = null;
                port = 0;
                return false;
            }
        }
    }
}