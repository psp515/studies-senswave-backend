using Perf.Core.Export;

namespace Perf.UnitTests;

public class BlobUrlTests
{
    [Fact]
    public void InsertsBlobNameBeforeSasQuery()
    {
        var url = BlobUrl.Build(new Uri("https://acct.blob.core.windows.net/perf?sv=2022&sig=abc"), "smoke-2026.zip");

        Assert.Equal("https://acct.blob.core.windows.net/perf/smoke-2026.zip?sv=2022&sig=abc", url.ToString());
    }
}
