using System.Text.Json.Nodes;

namespace Perf.Client;

/// <summary>One example response of a scenario, recorded once per campaign before the measurements.</summary>
/// <param name="Call">The REST path or the rpc that produced it.</param>
/// <param name="Encoding">json (REST, SignalR) or protobuf (gRPC, gRPC-Web).</param>
/// <param name="PayloadBytes">Size of the response payload only: the JSON body (UTF-8) or the serialized protobuf message,
/// without HTTP/2 frames, headers, the gRPC length prefix or TLS.</param>
/// <param name="Body">The response as JSON. Protobuf messages are rendered with Google.Protobuf's JsonFormatter.</param>
public sealed record ResponseSample(string Scenario, string Transport, string Call, string Encoding, int PayloadBytes, JsonNode? Body);
