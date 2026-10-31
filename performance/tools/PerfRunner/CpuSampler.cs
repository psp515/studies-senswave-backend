using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Samples the busy fraction of the load machine's CPUs (/proc/stat) every 5 s; averages are taken over a time window.</summary>
public sealed class CpuSampler : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly CpuSamples _samples = new();
    private readonly Task _loop;

    public CpuSampler() => _loop = Loop();

    public double AverageBetween(DateTimeOffset from, DateTimeOffset to) => _samples.AverageBetween(from, to);

    private async Task Loop()
    {
        if (!File.Exists("/proc/stat"))
        {
            Console.Error.WriteLine("warning: /proc/stat not available, the load generator CPU gate is not enforced");
            return;
        }

        try
        {
            var previous = Read();
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _stop.Token);
                var current = Read();
                _samples.Add(DateTimeOffset.UtcNow, ProcStat.Utilisation(previous, current));
                previous = current;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"warning: CPU sampling stopped ({ex.Message}), the load generator CPU gate is not enforced for the rest of this run");
        }
    }

    private static CpuTimes Read() => ProcStat.Parse(File.ReadLines("/proc/stat").First());

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
    }
}
