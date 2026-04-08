using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton service that distributes Server-Sent Events at two scopes:
///  - Business scope  (meeting list): keyed by businessId
///  - Meeting scope   (meeting detail): keyed by meetingId
/// </summary>
public class MeetingSseService
{
    // ── business-level channels ────────────────────────────────────────────
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _bizChannels  = new();
    // ── meeting-level channels ─────────────────────────────────────────────
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _mtgChannels  = new();

    // ── business scope ─────────────────────────────────────────────────────

    public Channel<string> Subscribe(int businessId)       => AddChannel(_bizChannels, businessId);
    public void Unsubscribe(int businessId, Channel<string> ch) => RemoveChannel(_bizChannels, businessId, ch);
    public void Notify(int businessId, string eventType, object? data = null)
        => BroadcastTo(_bizChannels, businessId, eventType, data);

    // ── meeting scope ──────────────────────────────────────────────────────

    public Channel<string> SubscribeMeeting(int meetingId)       => AddChannel(_mtgChannels, meetingId);
    public void UnsubscribeMeeting(int meetingId, Channel<string> ch) => RemoveChannel(_mtgChannels, meetingId, ch);
    public void NotifyMeeting(int meetingId, string eventType, object? data = null)
        => BroadcastTo(_mtgChannels, meetingId, eventType, data);

    // ── shared helpers ─────────────────────────────────────────────────────

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
