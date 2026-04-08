using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton service that distributes Server-Sent Events to all subscribers
/// watching a given businessId for meeting changes.
/// </summary>
public class MeetingSseService
{
    // businessId → list of open channels (one per connected client)
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _channels = new();

    public Channel<string> Subscribe(int businessId)
    {
        var ch = Channel.CreateUnbounded<string>();
        _channels.AddOrUpdate(
            businessId,
            _ => new List<Channel<string>> { ch },
            (_, list) => { lock (list) { list.Add(ch); } return list; });
        return ch;
    }

    public void Unsubscribe(int businessId, Channel<string> ch)
    {
        if (_channels.TryGetValue(businessId, out var list))
            lock (list) { list.Remove(ch); }
    }

    /// <summary>
    /// Broadcast an SSE event to all clients subscribed to <paramref name="businessId"/>.
    /// </summary>
    public void Notify(int businessId, string eventType, object? data = null)
    {
        if (!_channels.TryGetValue(businessId, out var list)) return;

        var json = data != null
            ? System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                })
            : "{}";

        var msg = $"event: {eventType}\ndata: {json}\n\n";

        lock (list)
            foreach (var ch in list)
                ch.Writer.TryWrite(msg);
    }
}
