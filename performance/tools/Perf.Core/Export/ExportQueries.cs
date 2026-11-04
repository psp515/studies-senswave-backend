using Perf.Core.Runs;

namespace Perf.Core.Export;

public sealed record ExportQuery(string File, bool Range, string PromQl);

public static class ExportQueries
{
    public static string ContainerName(Transport transport) => transport switch
    {
        Transport.Rest => "Senswave.Perf.Rest",
        Transport.Grpc => "Senswave.Perf.Grpc",
        _ => "Senswave.Perf.GrpcWeb",
    };

    public static IReadOnlyList<ExportQuery> For(RunMeta meta)
    {
        // Only one API runs at a time and every query is bound to the run's measurement window, so transport selects the run.
        // run_id is not a metric label (it would create new series every run; see stack/alloy/config.alloy).
        var run = $"transport=\"{meta.Transport}\"";
        // The Docker health check calls the API over plain HTTP (port 5001); only the HTTPS traffic is the load under test.
        var http = $"{run},url_scheme=\"https\"";
        var container = $"name=\"{ContainerName(meta.Transport)}\"";
        var window = meta.MeasureStartUtc is { } start && meta.MeasureEndUtc is { } end ? (int)(end - start).TotalSeconds : 0;

        var list = new List<ExportQuery>();

        // Raw data only: request counts per histogram bucket over the whole window. Any percentile is computed later in the
        // analysis from these counts, so no pre-computed (interpolated) quantile is stored.
        list.Add(new("latency-buckets.csv", false,
            $"sum by (le) (increase(http_server_request_duration_seconds_bucket{{{http}}}[{window}s]))"));
        list.Add(new("latency-buckets.csv", false,
            $"sum by (le, http_response_status_code) (increase(http_server_request_duration_seconds_bucket{{{http}}}[{window}s]))"));

        list.Add(new("throughput.csv", true, $"sum(rate(http_server_request_duration_seconds_count{{{http}}}[30s]))"));
        list.Add(new("throughput.csv", true, $"sum by (error_type) (rate(http_server_request_duration_seconds_count{{{http},error_type!=\"\"}}[30s]))"));
        list.Add(new("throughput.csv", true, $"sum by (http_response_status_code) (rate(http_server_request_duration_seconds_count{{{http}}}[30s]))"));
        list.Add(new("cpu.csv", true, $"rate(container_cpu_usage_seconds_total{{{container}}}[30s])"));
        list.Add(new("cpu.csv", true, $"rate(process_cpu_time_seconds_total{{{run}}}[30s])"));
        list.Add(new("memory.csv", true, $"container_memory_working_set_bytes{{{container}}}"));
        list.Add(new("gc.csv", true, $"rate(dotnet_gc_heap_total_allocated_bytes_total{{{run}}}[30s])"));
        list.Add(new("gc.csv", true, $"sum by (dotnet_gc_heap_generation) (rate(dotnet_gc_collections_total{{{run}}}[30s]))"));
        list.Add(new("gc.csv", true, $"dotnet_thread_pool_queue_length{{{run}}}"));
        list.Add(new("network.csv", true, $"rate(container_network_receive_bytes_total{{{container}}}[30s])"));
        list.Add(new("network.csv", true, $"rate(container_network_transmit_bytes_total{{{container}}}[30s])"));
        list.Add(new("connections.csv", true, $"kestrel_active_connections{{{run}}}"));
        list.Add(new("connections.csv", false,
            $"sum by (le) (increase(kestrel_tls_handshake_duration_seconds_bucket{{{run}}}[{window}s]))"));

        return list;
    }
}
