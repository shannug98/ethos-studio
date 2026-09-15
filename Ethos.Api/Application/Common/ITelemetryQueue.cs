using Ethos.Api.Domain.Entities;
using System.Threading.Channels;

namespace Ethos.Api.Application.Common;

public interface ITelemetryQueue
{
    void Enqueue(ApiRequestLog log);
    ValueTask<ApiRequestLog> DequeueAsync(CancellationToken cancellationToken);
    bool TryDequeue(out ApiRequestLog? log);
}

public class ChannelTelemetryQueue : ITelemetryQueue
{
    private readonly Channel<ApiRequestLog> _channel;

    public ChannelTelemetryQueue()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<ApiRequestLog>(options);
    }

    public void Enqueue(ApiRequestLog log)
    {
        _channel.Writer.TryWrite(log);
    }

    public ValueTask<ApiRequestLog> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public bool TryDequeue(out ApiRequestLog? log)
    {
        return _channel.Reader.TryRead(out log);
    }
}