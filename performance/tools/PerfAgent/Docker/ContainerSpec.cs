using System.Text.Json.Nodes;

namespace PerfAgent.Docker;

/// <summary>
/// Builds a Docker Engine "create container" body from an inspect result, so a container can be recreated with
/// different env vars. Bind mounts keep the host paths compose resolved; runtime-only fields are dropped.
/// </summary>
public static class ContainerSpec
{
    private static readonly string[] RuntimeConfigFields = ["Hostname", "Domainname"];

    public static JsonObject BuildCreateBody(JsonObject inspect, IReadOnlyDictionary<string, string> envOverrides)
    {
        var body = inspect["Config"]!.DeepClone().AsObject();
        foreach (var field in RuntimeConfigFields)
            body.Remove(field);

        var env = (body["Env"]?.AsArray().Select(e => e!.GetValue<string>()) ?? [])
            .Where(e => !envOverrides.ContainsKey(e.Split('=', 2)[0]))
            .Concat(envOverrides.Select(kv => $"{kv.Key}={kv.Value}"));
        body["Env"] = new JsonArray(env.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray());

        body["HostConfig"] = inspect["HostConfig"]?.DeepClone() ?? new JsonObject();

        var endpoints = new JsonObject();
        if (inspect["NetworkSettings"]?["Networks"] is JsonObject networks)
            foreach (var (name, network) in networks)
                endpoints[name] = new JsonObject { ["Aliases"] = network?["Aliases"]?.DeepClone() ?? new JsonArray() };
        body["NetworkingConfig"] = new JsonObject { ["EndpointsConfig"] = endpoints };

        return body;
    }
}
