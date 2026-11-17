namespace Perf.Core.Knee;

/// <summary>
/// Search for the knee of one transport and scenario: the highest rate the server sustains. Doubles from
/// <paramref name="start"/> up to <paramref name="max"/> until a step fails, then bisects between the highest sustained and
/// the lowest failed rate (midpoints rounded down to a multiple of <see cref="RateStep"/>) until the gap is at most the
/// fraction <paramref name="precision"/> of the knee. The knee is then repeated once: if the repeat fails, that rate counts
/// as failed and the search continues below it.
/// </summary>
public sealed class KneeSearch(int start, int max, double precision)
{
    public const int RateStep = 10;

    private readonly SortedSet<int> _sustained = [];
    private int? _lowestFailed;
    private int? _repeated;

    /// <summary>Highest sustained rate so far (0 if none).</summary>
    public int Knee => _sustained.Count > 0 ? _sustained.Max : 0;

    /// <summary>True when the max rate itself was sustained, so the knee is at least the max.</summary>
    public bool AtLeastMax => _lowestFailed is null && Knee >= max;

    /// <summary>True when the load generator was the limit, so the knee is at least <see cref="Knee"/>.</summary>
    public bool GeneratorLimited { get; private set; }

    /// <summary>The next rate to test, or null when the search is over.</summary>
    public int? Next
    {
        get
        {
            if (GeneratorLimited)
                return null;

            var knee = Knee;
            if (_lowestFailed is { } failed)
            {
                if (failed - knee > Math.Max(RateStep, knee * precision))
                {
                    var mid = (knee + failed) / 2 / RateStep * RateStep;
                    if (mid <= knee)
                        mid = knee + RateStep;
                    if (mid < failed)
                        return mid;
                }
            }
            else if (knee < max)
                return knee == 0 ? start : Math.Min(knee * 2, max);

            return knee > 0 && _repeated != knee ? knee : null;
        }
    }

    public void Record(int rate, bool sustained)
    {
        if (sustained)
        {
            if (!_sustained.Add(rate))
                _repeated = rate;
            return;
        }

        _sustained.Remove(rate);
        _lowestFailed = _lowestFailed is { } failed ? Math.Min(failed, rate) : rate;
    }

    /// <summary>Ends the search: the step says nothing about the server.</summary>
    public void StopAtGeneratorLimit() => GeneratorLimited = true;
}
