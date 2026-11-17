using Perf.Core.Runs;

namespace Perf.UnitTests;

public class CpuSamplesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Average_only_covers_samples_inside_window()
    {
        var samples = new CpuSamples();
        samples.Add(T0.AddSeconds(5), 0.95);   // warm-up
        samples.Add(T0.AddSeconds(10), 0.95);  // warm-up
        samples.Add(T0.AddSeconds(15), 0.20);
        samples.Add(T0.AddSeconds(20), 0.40);

        Assert.Equal(0.30, samples.AverageBetween(T0.AddSeconds(10), T0.AddSeconds(20)), 6);
    }

    [Fact]
    public void Empty_window_is_zero()
    {
        var samples = new CpuSamples();
        samples.Add(T0, 0.5);

        Assert.Equal(0, samples.AverageBetween(T0.AddSeconds(1), T0.AddSeconds(9)));
    }
}
