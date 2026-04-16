using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GPInventory.Api.Services;

/// <summary>
/// Singleton SSE service for the GP Control module.
/// Provides two broadcast scopes:
///   - Board scope: notifies all users watching a specific board (kanban, backlog, iterations).
///   - Task scope:  notifies all users with a specific task detail open (comments, subtasks).
/// </summary>
public class ControlSseService
{
    // board-level channels (keyed by boardId)
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _boardChannels = new();

    // task-detail channels (keyed by taskId)
    private readonly ConcurrentDictionary<int, List<Channel<string>>> _taskChannels = new();

    // presence: boardId → { userId → displayName }
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, string>> _presence = new();

    // ── board scope ────────────────────────────────────────────────────────

    public Channel<string> SubscribeBoard(int boardId) => AddChannel(_boardChannels, boardId);
    public void UnsubscribeBoard(int boardId, Channel<string> ch) => RemoveChannel(_boardChannels, boardId, ch);
    public void NotifyBoard(int boardId, string eventType, object? data = null)
        => BroadcastTo(_boardChannels, boardId, eventType, data);

    // ── task-detail scope ──────────────────────────────────────────────────

    public Channel<string> SubscribeTask(int taskId) => AddChannel(_taskChannels, taskId);
    public void UnsubscribeTask(int taskId, Channel<string> ch) => RemoveChannel(_taskChannels, taskId, ch);
    public void NotifyTask(int taskId, string eventType, object? data = null)
        => BroadcastTo(_taskChannels, taskId, eventType, data);

    // ── presence ───────────────────────────────────────────────────────────

    public void AddPresence(int boardId, int userId, string displayName)
    {
        var dict = _presence.GetOrAdd(boardId, _ => new ConcurrentDictionary<int, string>());
        dict[userId] = displayName;
    }

    public void RemovePresence(int boardId, int userId)
    {
        if (_presence.TryGetValue(boardId, out var dict))
            dict.TryRemove(userId, out _);
    }

    public List<object> GetPresence(int boardId)
    {
        if (!_presence.TryGetValue(boardId, out var dict))
            return new List<object>();
        return dict.Select(kv => (object)new { userId = kv.Key, name = kv.Value }).ToList();
    }

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
