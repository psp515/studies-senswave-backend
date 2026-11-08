namespace Perf.Core;

public enum Transport { Rest, Grpc, GrpcWeb }

/// <summary>S1-S5 request/response; S6 live updates from the device (MQTT messages the API receives and pushes to subscribers).</summary>
public enum Scenario { S1, S2, S3, S4, S5, S6 }

public static class ScenarioExtensions
{
    public static bool IsStream(this Scenario scenario) => scenario is Scenario.S6;
}
