namespace Senswave.Infrastructure.Persistence;

public class PostgreSqlOptions
{
    public const string SectionName = "PostgreSql";

    public string Host { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string ErrorDetail { get; set; } = "true";

    /// <summary>Npgsql connection pool size; null keeps the Npgsql default (100).</summary>
    public int? MaxPoolSize { get; set; }

    public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Include Error Detail={ErrorDetail};"
        + (MaxPoolSize is { } size ? $"Maximum Pool Size={size};" : string.Empty);
}
