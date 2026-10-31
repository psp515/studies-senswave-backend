namespace Perf.Core.Streams;

public sealed record StreamResult(long Published, long Delivered, long Lost, long Unmatched, double[] LatenciesMs);

/// <summary>
/// Live updates carry only a device id, so each delivery is matched to the oldest pending publish for that device (FIFO).
/// Publishes made during warm-up are consumed but not recorded. Thread-safe.
/// </summary>
public sealed class DeliveryTracker
{
    private readonly Dictionary<string, Queue<(long Ticks, bool Measured)>> _pending = new();
    private readonly List<double> _latencies = new();
    private readonly object _lock = new();
    private long _published, _delivered, _unmatched;

    public void Published(string deviceId, long timestampTicks, bool measured)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(deviceId, out var queue))
                _pending[deviceId] = queue = new Queue<(long, bool)>();

            queue.Enqueue((timestampTicks, measured));
            if (measured)
                _published++;
        }
    }

    /// <summary>Removes the pending publish stamped <paramref name="timestampTicks"/> for the device (its publish failed, so no delivery will come).</summary>
    public void Retract(string deviceId, long timestampTicks)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(deviceId, out var queue))
                return;

            var kept = new Queue<(long Ticks, bool Measured)>(queue.Count);
            var removed = false;
            foreach (var entry in queue)
            {
                if (!removed && entry.Ticks == timestampTicks)
                {
                    removed = true;
                    if (entry.Measured)
                        _published--;
                    continue;
                }

                kept.Enqueue(entry);
            }

            _pending[deviceId] = kept;
        }
    }

    public bool Delivered(string deviceId, long timestampTicks)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(deviceId, out var queue) || queue.Count == 0)
            {
                _unmatched++;
                return false;
            }

            var (published, measured) = queue.Dequeue();
            if (measured)
            {
                _delivered++;
                _latencies.Add(TimeSpan.FromTicks(timestampTicks - published).TotalMilliseconds);
            }

            return true;
        }
    }

    public StreamResult Finish(long nowTicks, TimeSpan grace)
    {
        lock (_lock)
        {
            var cutoff = nowTicks - grace.Ticks;
            var lost = _pending.Values.SelectMany(q => q).LongCount(p => p.Measured && p.Ticks <= cutoff);

            return new StreamResult(_published, _delivered, lost, _unmatched, _latencies.ToArray());
        }
    }
}
