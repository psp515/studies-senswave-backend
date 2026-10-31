using System.IO.Compression;
using Perf.Core.Export;

namespace PerfRunner.Export;

/// <summary>Zips a campaign directory and uploads it with one Put Blob request to a container SAS URL.</summary>
public static class Archiver
{
    public static async Task<Uri> Upload(string campaignDir, string containerSasUrl, CancellationToken ct)
    {
        var zip = Path.Combine(Path.GetTempPath(), Path.GetFileName(campaignDir.TrimEnd('/', '\\')) + ".zip");
        File.Delete(zip);
        ZipFile.CreateFromDirectory(campaignDir, zip, CompressionLevel.Optimal, includeBaseDirectory: true);

        try
        {
            var url = BlobUrl.Build(new Uri(containerSasUrl), Path.GetFileName(zip));
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            await using var file = File.OpenRead(zip);
            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new StreamContent(file) };
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.Add("x-ms-version", "2021-08-06");
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Blob upload {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

            return new Uri(url.GetLeftPart(UriPartial.Path));
        }
        finally
        {
            File.Delete(zip);
        }
    }
}
