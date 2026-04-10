using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Text.Json;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton SSE service for GP Assets module.
/// Business-scoped: each business has its own channel list.
/// Events: asset.created | asset.updated | asset.deleted
/// </summary>
public class AssetSseService
{
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _bizChannels = new();

    public Channel<string> Subscribe(int businessId)
        => AddChannel(_bizChannels, businessId);

    public void Unsubscribe(int businessId, Channel<string> ch)
        => RemoveChannel(_bizChannels, businessId, ch);

    public void Notify(int businessId, string eventType, object? data = null)
        => BroadcastTo(_bizChannels, businessId, eventType, data);

    // ── helpers ───────────────────────────────────────────────────────────

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

        var json   = data != null ? JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) : "{}";
        var message = $"event: {eventType}\ndata: {json}\n\n";

        List<Channel<string>> snapshot;
        lock (list) { snapshot = list.ToList(); }

        foreach (var ch in snapshot)
            ch.Writer.TryWrite(message);
    }
}
