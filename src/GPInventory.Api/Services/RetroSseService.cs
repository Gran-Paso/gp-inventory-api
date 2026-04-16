using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton SSE service for Retrospective sessions.
/// Scope: session-level — all participants of a retro session receive real-time updates.
/// </summary>
public class RetroSseService
{
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _sessionChannels = new();

    // presence: sessionId → { userId → displayName }
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, string>> _presence = new();

    public Channel<string> Subscribe(int sessionId)   => AddChannel(_sessionChannels, sessionId);
    public void Unsubscribe(int sessionId, Channel<string> ch) => RemoveChannel(_sessionChannels, sessionId, ch);
    public void Notify(int sessionId, string eventType, object? data = null)
        => BroadcastTo(_sessionChannels, sessionId, eventType, data);

    // ── presence ──────────────────────────────────────────────────────────

    public void AddPresence(int sessionId, int userId, string displayName)
    {
        var dict = _presence.GetOrAdd(sessionId, _ => new ConcurrentDictionary<int, string>());
        dict[userId] = displayName;
    }

    public void RemovePresence(int sessionId, int userId)
    {
        if (_presence.TryGetValue(sessionId, out var dict))
            dict.TryRemove(userId, out _);
    }

    public List<object> GetPresence(int sessionId)
    {
        if (!_presence.TryGetValue(sessionId, out var dict))
            return new List<object>();
        return dict.Select(kv => (object)new { userId = kv.Key, name = kv.Value }).ToList();
    }

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
