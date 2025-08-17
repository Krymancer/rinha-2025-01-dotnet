using System.Threading.Channels;
using Api.Contracts.Requests;

namespace Api.Channels
{
  public sealed class ProcessingChannel
  {
    private readonly Channel<PaymentRequest> _channel;

    public ProcessingChannel()
    {
      var options = new UnboundedChannelOptions
      {
        SingleReader = true,
        SingleWriter = false
      };

      _channel = Channel.CreateUnbounded<PaymentRequest>(options);
    }

    public ValueTask WriteAsync(PaymentRequest data, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(data, cancellationToken);

    public ChannelReader<PaymentRequest> GetReader() => _channel.Reader;
  }
}