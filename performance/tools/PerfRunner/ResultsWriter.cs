using System.Text.Json;
using Perf.Core;

namespace PerfRunner;

public static class ResultsWriter
{
    public static async Task Json<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PerfJson.Options), ct);
    }
}
