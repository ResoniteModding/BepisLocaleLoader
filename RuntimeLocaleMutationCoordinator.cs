namespace BepisLocaleLoader;

internal sealed class RuntimeLocaleMutationCoordinator<T>
{
    private readonly record struct Entry(long Id, T Data, bool Force);

    private readonly object _gate = new();
    private readonly List<Entry> _registered = new();
    private readonly Queue<Entry> _pending = new();
    private long _nextId;

    internal int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    internal void Enqueue(T localeData, bool force)
    {
        lock (_gate)
        {
            var entry = new Entry(_nextId++, localeData, force);
            _registered.Add(entry);
            _pending.Enqueue(entry);
        }
    }

    internal int FlushPending(Func<T, bool, bool> tryApply)
    {
        var failed = new List<Entry>();
        int applied = 0;

        while (true)
        {
            Entry next;

            lock (_gate)
            {
                if (_pending.Count == 0)
                    break;

                next = _pending.Dequeue();
            }

            if (tryApply(next.Data, next.Force))
            {
                applied++;
            }
            else
            {
                failed.Add(next);
            }
        }

        if (failed.Count == 0)
            return applied;

        lock (_gate)
        {
            foreach (var item in failed)
            {
                _pending.Enqueue(item);
            }
        }

        return applied;
    }

    internal int ReplayRegistered(Func<T, bool, bool> tryApply)
    {
        List<Entry> snapshot;
        lock (_gate)
        {
            snapshot = [.. _registered];
        }

        int applied = 0;
        var acknowledgedIds = new HashSet<long>();
        foreach (var entry in snapshot)
        {
            if (tryApply(entry.Data, entry.Force))
            {
                applied++;
                acknowledgedIds.Add(entry.Id);
            }
        }

        AcknowledgeLeadingPendingEntries(acknowledgedIds);
        return applied;
    }

    private void AcknowledgeLeadingPendingEntries(HashSet<long> acknowledgedIds)
    {
        if (acknowledgedIds.Count == 0)
            return;

        lock (_gate)
        {
            while (_pending.Count > 0 && acknowledgedIds.Contains(_pending.Peek().Id))
            {
                _pending.Dequeue();
            }
        }
    }
}
