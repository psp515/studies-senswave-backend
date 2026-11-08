namespace PerfAgent.Database;

public interface IDatabaseAdmin
{
    /// <summary>Drops <paramref name="database"/> (forcing out connections) and creates it empty or from <paramref name="template"/>.</summary>
    Task Recreate(string database, string? template, CancellationToken ct);
}
