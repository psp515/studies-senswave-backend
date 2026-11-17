using System.Text.Json.Nodes;

namespace PerfAgent.Docker;

public interface IDockerEngine
{
    Task<JsonObject> Inspect(string name, CancellationToken ct);
    Task Stop(string name, CancellationToken ct);
    Task Remove(string name, CancellationToken ct);
    Task Create(string name, JsonObject body, CancellationToken ct);
    Task Start(string name, CancellationToken ct);
    Task<string> Logs(string name, long sinceUnix, CancellationToken ct);
}
