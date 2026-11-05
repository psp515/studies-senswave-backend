namespace Perf.Core.Knee;

/// <summary>
/// Binary search for the knee of one transport and scenario: the highest rate the server sustains.
/// Starts at <paramref name="max"/>; a sustained step raises the lower bound, a failed step lowers the upper bound, and
/// the next rate is the midpoint rounded down to a multiple of <paramref name="precision"/>. The search ends when the
/// max rate is sustained or the gap between the highest sustained and the lowest failed rate is at most the precision.
/// </summary>
public sealed class KneeSearch(int max, int precision)
{
    private int _highestValid;
    private int? _lowestFailed;

    public int Max => max;
    public int Precision => precision;

    /// <summary>Highest sustained rate so far (0 if none).</summary>
    public int Knee => _highestValid;

    /// <summary>True when the max rate itself was sustained, so the knee is at least <see cref="Max"/>.</summary>
    public bool AtLeastMax => _lowestFailed is null && _highestValid >= max;

    /// <summary>The next rate to test, or null when the search is over.</summary>
    public int? Next
    {
        get
        {
            if (_lowestFailed is not { } failed)
                return _highestValid >= max ? null : max;

            if (failed - _highestValid <= precision)
                return null;

            var mid = (_highestValid + failed) / 2 / precision * precision;
            if (mid <= _highestValid)
                mid = _highestValid + precision;

            return mid < failed ? mid : null;
        }
    }

    public void Record(int rate, bool sustained)
    {
        if (sustained)
            _highestValid = Math.Max(_highestValid, rate);
        else
            _lowestFailed = _lowestFailed is { } failed ? Math.Min(failed, rate) : rate;
    }
}
