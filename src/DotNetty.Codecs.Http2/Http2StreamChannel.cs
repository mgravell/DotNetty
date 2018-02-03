
namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    // TODO: Should we have an extra method to "open" the stream and so Channel and take care of sending the
    //       Http2HeadersFrame under the hood ?
    // TODO: Should we extend SocketChannel and map input and output state to the stream state ?
    public interface Http2StreamChannel : IChannel 
    {
        /**
         * Returns the {@link Http2FrameStream} that belongs to this channel.
         */
        Http2FrameStream stream();
    }
}
