using Perf.Core.Runs;

namespace Perf.UnitTests;

public class ProcStatTests
{
    [Fact]
    public void ComputesUtilisationBetweenSamples()
    {
        // user nice system idle iowait irq softirq steal
        var a = ProcStat.Parse("cpu  100 0 100 800 0 0 0 0 0 0");
        var b = ProcStat.Parse("cpu  200 0 200 1400 0 0 0 0 0 0");

        Assert.Equal(200.0 / 800.0, ProcStat.Utilisation(a, b), 6);
    }
}
