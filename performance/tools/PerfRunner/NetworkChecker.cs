using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Perf.Core.Campaigns;
using Perf.Core.Network;

namespace PerfRunner;

/// <summary>
/// Measures the network path between the load side and the VM: TCP connect time to the agent port (one round trip, no ICMP
/// needed; the agent runs on the same VM and, unlike the API, is up between runs)
/// and download / upload throughput against the agent's /netcheck endpoints over parallel TLS connections.
/// </summary>
public static class NetworkChecker
{
    private const int RttSamples = 30;
    private const int Streams = 4;
    private const long DownloadBytesPerStream = 25L * 1024 * 1024;
    private const int UploadBytesPerStream = 8 * 1024 * 1024;

    public static async Task<NetworkCheck> Measure(CampaignTarget target, string agentToken, CancellationToken ct)
    {
        var errors = new List<string>();
        var host = target.Agent.Host;
        var port = target.Agent.Port;

        // Resolve once, so DNS time never enters the round-trip samples.
        var address = (await System.Net.Dns.GetHostAddressesAsync(host, ct)).First();
        var rtt = new List<double>();
        for (var i = 0; i < RttSamples; i++)
        {
            try
            {
                using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                var watch = Stopwatch.StartNew();
                await socket.ConnectAsync(address, port, ct);
                rtt.Add(watch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"tcp connect {i}: {ex.Message}");
            }

            await Task.Delay(100, ct);
        }

        var (downMbps, downBytes) = await Throughput(target, agentToken, upload: false, errors, ct);
        var (upMbps, upBytes) = await Throughput(target, agentToken, upload: true, errors, ct);

        return new NetworkCheck(DateTimeOffset.UtcNow, host, port, NetworkStats.Rtt(rtt), downMbps, upMbps, Streams, downBytes, upBytes, errors);
    }

    private static async Task<(double Mbps, long Bytes)> Throughput(CampaignTarget target, string token, bool upload, List<string> errors, CancellationToken ct)
    {
        // One HttpClient per stream: each stream gets its own TCP + TLS connection, like the parallel streams of a speed test.
        var watch = Stopwatch.StartNew();
        var results = await Task.WhenAll(Enumerable.Range(0, Streams).Select(async i =>
        {
            var handler = new SocketsHttpHandler();
            if (target.InsecureTls)
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            using var http = new HttpClient(handler) { BaseAddress = target.Agent, Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                if (upload)
                {
                    using var content = new ByteArrayContent(new byte[UploadBytesPerStream]);
                    using var response = await http.PostAsync("netcheck/upload", content, ct);
                    response.EnsureSuccessStatusCode();
                    return (long)UploadBytesPerStream;
                }

                using var download = await http.GetAsync($"netcheck/download?bytes={DownloadBytesPerStream}", HttpCompletionOption.ResponseHeadersRead, ct);
                download.EnsureSuccessStatusCode();
                await using var body = await download.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, ct)) > 0)
                    total += read;
                return total;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (errors)
                    errors.Add($"{(upload ? "upload" : "download")} stream {i}: {ex.Message}");
                return 0L;
            }
        }));

        var bytes = results.Sum();
        return (NetworkStats.Mbps(bytes, watch.Elapsed), bytes);
    }
}
