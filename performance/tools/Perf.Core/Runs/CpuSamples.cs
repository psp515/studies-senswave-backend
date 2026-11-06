namespace Perf.Core.Runs;

/// <summary>Thread-safe timestamped CPU utilisation samples; each sample is stamped at the end of the interval it covers.</summary>
public sealed class CpuSamples
{
    private readonly object _gate = new();
    private readonly List<(DateTimeOffset At, double Value)> _samples = new();

    public void Add(DateTimeOffset at, double value)
    {
        lock (_gate)
            _samples.Add((at, value));
    }

    /// <summary>Average of the samples stamped in (from, to]; 0 when none fall in the window.</summary>
    public double AverageBetween(DateTimeOffset from, DateTimeOffset to)
    {
        lock (_gate)
        {
            var window = _samples.Where(s => s.At > from && s.At <= to).Select(s => s.Value).ToList();
            return window.Count == 0 ? 0 : window.Average();
        }
    }
}
