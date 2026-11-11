using DesktopNMS.Core.Intelligence;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Writes saved chats off the UI thread (#229), as desktop's chat writer does
/// (desktop #321): serialising a long chat with graphs, encrypting it and
/// writing it is too much work for the thread that is drawing a streaming answer.
/// </summary>
/// <remarks>
/// <para>
/// One worker writes in order, so a delete never races a save of the same chat.
/// A save still waiting is replaced by a newer one of the same chat, and a delete
/// drops any waiting save of it, so a busy chat is written as often as the disk
/// keeps up and the queue never holds more than one save and one delete per chat.
/// </para>
/// <para>
/// A deleted chat stays deleted: once a delete is queued, any save of that chat
/// arriving later - a debounce firing late, a suspension flush - is refused, so a
/// stale snapshot can't bring it back. <see cref="Invalidate"/> drops everything
/// waiting (sign-out); the storage's generation refuses a write already under way
/// once it has been wiped, so neither can write a signed-out chat back.
/// </para>
/// <para>
/// Each operation carries the store it was opened with
/// (<see cref="IntelligenceChatStorage.OpenAsync"/>). Callers pass a snapshot the
/// UI thread won't change afterwards. Nothing from a chat is logged, and nor are
/// exception messages, which can carry paths.
/// </para>
/// </remarks>
public sealed class IntelligenceChatWriter(ILogger logger)
{
    private readonly object _sync = new();
    private readonly LinkedList<Operation> _pending = new();
    private readonly HashSet<(string Scope, Guid Id)> _deleted = new();
    private Task _worker = Task.CompletedTask;
    private bool _running;
    private long _epoch;

    /// <summary>
    /// A write failed, on the writer's thread: the tag it was queued with, and
    /// whether the storage was unavailable (wiped, closed or without its key)
    /// rather than the write itself failing.
    /// </summary>
    public event Action<long, bool>? Failed;

    /// <summary>How many operations are waiting, for tests of coalescing.</summary>
    public int Pending
    {
        get
        {
            lock (_sync)
            {
                return _pending.Count;
            }
        }
    }

    /// <summary>Queues <paramref name="snapshot"/>; false when it was refused because that chat was deleted.</summary>
    public bool Save(IIntelligenceChatStore store, string scope, SavedIntelligenceChat snapshot, bool makeActive, long tag = 0)
    {
        lock (_sync)
        {
            if (_deleted.Contains((scope, snapshot.Id)))
            {
                return false;
            }

            Remove(scope, snapshot.Id, savesOnly: true);
            Enqueue(new Operation(store, scope, snapshot.Id, snapshot, makeActive, tag, _epoch));
            return true;
        }
    }

    /// <summary>Queues a delete, dropping any save of that chat still waiting, and refuses later saves of it.</summary>
    public void Delete(IIntelligenceChatStore store, string scope, Guid id, long tag = 0)
    {
        lock (_sync)
        {
            _deleted.Add((scope, id));
            Remove(scope, id, savesOnly: false);
            Enqueue(new Operation(store, scope, id, null, false, tag, _epoch));
        }
    }

    /// <summary>
    /// Drops everything waiting, and anything the worker hasn't started yet
    /// (sign-out). A write already under way finishes or is refused by the
    /// storage's own generation.
    /// </summary>
    public void Invalidate()
    {
        lock (_sync)
        {
            _epoch++;
            _pending.Clear();
        }
    }

    /// <summary>Completes once everything queued so far has been written or refused - before reading chats back, and on suspension.</summary>
    public async Task FlushAsync()
    {
        while (true)
        {
            Task worker;
            lock (_sync)
            {
                if (!_running && _pending.Count == 0)
                {
                    return;
                }

                worker = _worker;
            }

            await worker.ConfigureAwait(false);
        }
    }

    // Callers hold _sync.
    private void Remove(string scope, Guid id, bool savesOnly)
    {
        for (var node = _pending.First; node is not null;)
        {
            var next = node.Next;
            if (node.Value.Scope == scope && node.Value.ChatId == id && (!savesOnly || node.Value.Snapshot is not null))
            {
                _pending.Remove(node);
            }

            node = next;
        }
    }

    // Callers hold _sync.
    private void Enqueue(Operation operation)
    {
        _pending.AddLast(operation);
        if (!_running)
        {
            _running = true;
            _worker = Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            Operation operation;
            lock (_sync)
            {
                if (_pending.First is not { } first)
                {
                    _running = false;
                    return;
                }

                operation = first.Value;
                _pending.RemoveFirst();

                // Queued before a sign-out, or a save of a chat deleted since.
                if (operation.Epoch != _epoch
                    || operation.Snapshot is not null && _deleted.Contains((operation.Scope, operation.ChatId)))
                {
                    continue;
                }
            }

            Run(operation);
        }
    }

    private void Run(Operation operation)
    {
        try
        {
            if (operation.Snapshot is { } snapshot)
            {
                operation.Store.Save(operation.Scope, snapshot, operation.MakeActive);
            }
            else
            {
                operation.Store.Delete(operation.Scope, operation.ChatId);
            }
        }
        catch (DirectoryNotFoundException) when (operation.Snapshot is null)
        {
            // Deleting a chat whose first save was dropped: nothing was ever written for this server.
        }
        catch (ChatStorageUnavailableException)
        {
            // Wiped or closed since it was queued: refused, as it should be.
            Failed?.Invoke(operation.Tag, true);
        }
        catch (Exception)
        {
            logger.LogWarning("A saved chat could not be {Action}", operation.Snapshot is null ? "deleted" : "saved");
            Failed?.Invoke(operation.Tag, false);
        }
    }

    private sealed record Operation(IIntelligenceChatStore Store, string Scope, Guid ChatId, SavedIntelligenceChat? Snapshot, bool MakeActive, long Tag, long Epoch);
}
