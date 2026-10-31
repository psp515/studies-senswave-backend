using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Average busy fraction of the load machine's CPUs (/proc/stat), sampled every 5 s.</summary>
public sealed class CpuSampler : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly List<double> _samples = new();
    private readonly Task _loop;

    public CpuSampler() => _loop = Loop();

    public double Average => _samples.Count == 0 ? 0 : _samples.Average();

    private async Task Loop()
    {
        if (!File.Exists("/proc/stat"))
            return;
        var previous = Read();
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _stop.Token);
                var current = Read();
                _samples.Add(ProcStat.Utilisation(previous, current));
                previous = current;
            }
        }
        catch (OperationCanceledException) { }
    }

    private static CpuTimes Read() => ProcStat.Parse(File.ReadLines("/proc/stat").First());

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
    }
}
