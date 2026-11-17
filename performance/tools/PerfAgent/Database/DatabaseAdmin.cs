using Microsoft.Extensions.Options;
using Npgsql;

namespace PerfAgent.Database;

public sealed class DatabaseAdmin(IOptions<AgentOptions> options) : IDatabaseAdmin
{
    public async Task Recreate(string database, string? template, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(options.Value.PostgresAdmin);
        await connection.OpenAsync(ct);

        await Execute(connection, $"DROP DATABASE IF EXISTS {Quote(database)} WITH (FORCE)", ct);
        await Execute(connection, template is null
            ? $"CREATE DATABASE {Quote(database)}"
            : $"CREATE DATABASE {Quote(database)} TEMPLATE {Quote(template)}", ct);
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
