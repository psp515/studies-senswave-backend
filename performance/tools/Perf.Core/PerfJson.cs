using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perf.Core;

public static class PerfJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
