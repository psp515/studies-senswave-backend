namespace Perf.Core.Runs;

public sealed record CpuTimes(long Busy, long Total);

/// <summary>Parses the aggregate <c>cpu</c> line of <c>/proc/stat</c>. Idle = idle + iowait.</summary>
public static class ProcStat
{
    public static CpuTimes Parse(string line)
    {
        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToArray();
        var total = values.Take(8).Sum();
        var idle = values[3] + values[4];

        return new CpuTimes(total - idle, total);
    }

    public static double Utilisation(CpuTimes a, CpuTimes b) =>
        b.Total == a.Total ? 0 : (double)(b.Busy - a.Busy) / (b.Total - a.Total);
}
