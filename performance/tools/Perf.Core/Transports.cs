namespace Perf.Core;

public enum Transport { Rest, Grpc, GrpcWeb }

public enum Scenario { S1, S2, S3, S4, S5, S6a, S6b }

public static class ScenarioExtensions
{
    public static bool IsStream(this Scenario scenario) => scenario is Scenario.S6a or Scenario.S6b;
}
