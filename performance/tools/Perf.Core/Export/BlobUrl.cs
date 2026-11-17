namespace Perf.Core.Export;

public static class BlobUrl
{
    public static Uri Build(Uri containerSasUrl, string blobName)
    {
        var builder = new UriBuilder(containerSasUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/" + Uri.EscapeDataString(blobName);
        return builder.Uri;
    }
}
