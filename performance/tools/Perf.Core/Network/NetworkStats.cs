using Perf.Core.Streams;

namespace Perf.Core.Network;

public sealed record RttStats(int Samples, double MinMs, double MedianMs, double P95Ms, double MaxMs);

/// <summary>Network conditions between the load side and the VM, measured before and after a session.</summary>
public sealed record NetworkCheck(DateTimeOffset AtUtc, string Host, int Port, RttStats TcpConnectRtt,
    double DownloadMbps, double UploadMbps, int Streams, long DownloadBytes, long UploadBytes, IReadOnlyList<string> Errors);

public static class NetworkStats
{
    public static RttStats Rtt(IReadOnlyCollection<double> samplesMs)
    {
        if (samplesMs.Count == 0)
            return new RttStats(0, 0, 0, 0, 0);

        var p = Percentiles.Of(samplesMs.ToArray(), 50, 95);
        return new RttStats(samplesMs.Count, samplesMs.Min(), p[0], p[1], samplesMs.Max());
    }

    public static double Mbps(long bytes, TimeSpan elapsed) => elapsed <= TimeSpan.Zero ? 0 : bytes * 8 / elapsed.TotalSeconds / 1_000_000;
}
