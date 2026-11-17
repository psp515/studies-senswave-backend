using System.Text;
using System.Text.Json.Nodes;

namespace Perf.Core.Export;

/// <summary>Pure conversion of a Prometheus query/query_range "data" object into CSV rows.</summary>
public static class PrometheusCsv
{
    public const string Header = "query,series,timestamp,value\n";

    public static void Append(StringBuilder csv, string query, JsonNode data)
    {
        foreach (var series in data["result"]!.AsArray())
        {
            var labels = Quote(series!["metric"]!.ToJsonString());
            var points = series["values"]?.AsArray() ?? new JsonArray(series["value"]!.DeepClone());
            foreach (var point in points)
                csv.Append(Quote(query)).Append(',').Append(labels).Append(',')
                    .Append(point![0]!.ToString()).Append(',').Append(point[1]!.GetValue<string>()).Append('\n');
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
