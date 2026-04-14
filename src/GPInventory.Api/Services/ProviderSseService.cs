using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton service that manages Server-Sent Events connections for provider changes.
/// When a provider is created, updated, or deleted, all connected clients receive a notification.
/// </summary>
public class ProviderSseService
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    /// <summary>Registers a new SSE client and returns its channel to stream from.</summary>
    public (Guid id, ChannelReader<string> reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _clients[id] = channel;
        return (id, channel.Reader);
    }

    /// <summary>Removes a client when the connection is closed.</summary>
    public void Unsubscribe(Guid id)
    {
        if (_clients.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    /// <summary>Broadcasts a provider event to all connected SSE clients.</summary>
    public async Task BroadcastAsync(string eventType, int providerId)
    {
        var payload = $"event: {eventType}\ndata: {{\"id\":{providerId}}}\n\n";
        var dead = new List<Guid>();

        foreach (var (id, channel) in _clients)
        {
            try
            {
                if (!channel.Writer.TryWrite(payload))
                    dead.Add(id);
            }
            catch
            {
                dead.Add(id);
            }
        }

        foreach (var id in dead)
            Unsubscribe(id);

        await Task.CompletedTask;
    }
}
