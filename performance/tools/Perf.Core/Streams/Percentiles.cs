namespace Perf.Core.Streams;

public static class Percentiles
{
    public static double[] Of(double[] values, params double[] ps)
    {
        if (values.Length == 0)
            return ps.Select(_ => double.NaN).ToArray();

        var sorted = values.Order().ToArray();

        return ps.Select(p => sorted[Math.Clamp((int)Math.Ceiling(p / 100.0 * sorted.Length) - 1, 0, sorted.Length - 1)]).ToArray();
    }
}
