using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace Perf.Core.Knee;

/// <summary>
/// Knee mode: once a step of a (round, OTel, transport, scenario) ladder ends in anything but <see cref="RunStatus.Valid"/>,
/// the higher steps of that ladder are skipped. The planner orders each ladder by ascending rate.
/// </summary>
public sealed class KneeClimb
{
    private readonly HashSet<string> _stopped = [];

    public bool ShouldSkip(RunSpec run) => _stopped.Contains(Key(run));

    public void Record(RunSpec run, string finalStatus)
    {
        if (finalStatus != RunStatus.Valid)
            _stopped.Add(Key(run));
    }

    private static string Key(RunSpec run) => $"{run.Round}|{run.Otel}|{run.Transport}|{run.Scenario}";
}
