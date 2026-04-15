using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton SSE service for Retrospective sessions.
/// Scope: session-level — all participants of a retro session receive real-time updates.
/// </summary>
public class RetroSseService
{
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _sessionChannels = new();

    public Channel<string> Subscribe(int sessionId)   => AddChannel(_sessionChannels, sessionId);
    public void Unsubscribe(int sessionId, Channel<string> ch) => RemoveChannel(_sessionChannels, sessionId, ch);
    public void Notify(int sessionId, string eventType, object? data = null)
        => BroadcastTo(_sessionChannels, sessionId, eventType, data);

    private static Channel<string> AddChannel(
        ConcurrentDictionary<int, List<Channel<string>>> dict, int key)
    {
        var ch = Channel.CreateUnbounded<string>();
        dict.AddOrUpdate(key,
            _ => new List<Channel<string>> { ch },
            (_, list) => { lock (list) { list.Add(ch); } return list; });
        return ch;
    }

    private static void RemoveChannel(
        ConcurrentDictionary<int, List<Channel<string>>> dict, int key, Channel<string> ch)
    {
        if (dict.TryGetValue(key, out var list))
            lock (list) { list.Remove(ch); }
    }

    private static void BroadcastTo(
        ConcurrentDictionary<int, List<Channel<string>>> dict,
        int key, string eventType, object? data)
    {
        if (!dict.TryGetValue(key, out var list)) return;
        var json = data != null
            ? System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions
                { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })
            : "{}";
        var msg = $"event: {eventType}\ndata: {json}\n\n";
        lock (list)
            foreach (var ch in list)
                ch.Writer.TryWrite(msg);
    }
}
