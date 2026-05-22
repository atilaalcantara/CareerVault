using System.Threading.Channels;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class TelegramUpdateQueue
{
    private readonly Channel<TelegramUpdate> _queue;

    public TelegramUpdateQueue(IOptions<TelegramOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _queue = Channel.CreateBounded<TelegramUpdate>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(TelegramUpdate update, CancellationToken cancellationToken) =>
        _queue.Writer.WriteAsync(update, cancellationToken);

    public IAsyncEnumerable<TelegramUpdate> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
