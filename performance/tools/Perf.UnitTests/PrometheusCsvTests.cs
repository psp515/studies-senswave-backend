using System.Text;
using System.Text.Json.Nodes;
using Perf.Core.Export;

namespace Perf.UnitTests;

public class PrometheusCsvTests
{
    [Fact]
    public void RangeResultProducesOneRowPerPoint()
    {
        var data = JsonNode.Parse("""
            {"resultType":"matrix","result":[{"metric":{"name":"a"},"values":[[1700000000,"1.5"],[1700000010,"2"]]}]}
            """)!;
        var csv = new StringBuilder(PrometheusCsv.Header);

        PrometheusCsv.Append(csv, "up{x=\"y\"}", data);

        var lines = csv.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal("\"up{x=\"\"y\"\"}\",\"{\"\"name\"\":\"\"a\"\"}\",1700000000,1.5", lines[1]);
        Assert.EndsWith(",1700000010,2", lines[2]);
    }

    [Fact]
    public void InstantResultProducesOneRow()
    {
        var data = JsonNode.Parse("""{"resultType":"vector","result":[{"metric":{},"value":[1700000000,"0.25"]}]}""")!;
        var csv = new StringBuilder(PrometheusCsv.Header);

        PrometheusCsv.Append(csv, "q", data);

        Assert.EndsWith(",1700000000,0.25\n", csv.ToString());
    }

    [Fact]
    public void EmptyResultAddsNothing()
    {
        var csv = new StringBuilder(PrometheusCsv.Header);

        PrometheusCsv.Append(csv, "q", JsonNode.Parse("""{"resultType":"vector","result":[]}""")!);

        Assert.Equal(PrometheusCsv.Header, csv.ToString());
    }
}
