using System;

namespace HttpClient
{
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    class Program
    {
        static async Task Main(string[] args)
        {
            IEventLoopGroup workGroup = new MultithreadEventLoopGroup(1);
            
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap.Group(workGroup);
                bootstrap.Channel<TcpSocketChannel>();

                bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;

                        pipeline.AddLast(TlsHandler.Client("httpbin.org"));
                        
                        pipeline.AddLast("encoder", new HttpRequestEncoder());
                        pipeline.AddLast("decoder", new HttpResponseDecoder(4096, 8192, 8192, false));
                        pipeline.AddLast("handler", new HelloClientHandler());
                    }));

                
                var channel = await bootstrap.ConnectAsync("httpbin.org", 443);

                var body = Encoding.UTF8.GetBytes("abcdef");
                
                var headers = new DefaultHttpHeaders(true);
                
                headers.Add(HttpHeaderNames.Host, "httpbin.org");
                headers.Add(HttpHeaderNames.ContentLength, body.Length);
                //headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                
                
                var req = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/post", headers);
                
                await channel.WriteAndFlushAsync(req);
                Console.WriteLine("Request headers sent");
                for (var i = 0; i < body.Length; i++)
                {
                    await channel.WriteAndFlushAsync(new DefaultHttpContent(Unpooled.WrappedBuffer(body, i, 1)));
                }

                await channel.WriteAndFlushAsync(EmptyLastHttpContent.Default);
                Console.WriteLine("Response body sent");

                Console.ReadLine();

            }
            finally
            {
                workGroup.ShutdownGracefullyAsync().Wait();
            }
        }
    }

    class HelloClientHandler : SimpleChannelInboundHandler<IHttpObject>
    {
        int length;
        
        protected override void ChannelRead0(IChannelHandlerContext ctx, IHttpObject msg)
        {
            if (msg is IHttpResponse req)
            {
                Console.WriteLine($"Response headers received: {msg}");  
                this.length = req.Headers.TryGetInt(HttpHeaderNames.ContentLength, out var len) ? len : 0;
            }
            else if (msg is IHttpContent content)
            {
                if (this.length > 0)
                {
                    var seq = content.Content.ReadCharSequence(this.length, Encoding.UTF8);
                    Console.WriteLine($"Response body received: {seq}");
                }
            }
        }
    }
}