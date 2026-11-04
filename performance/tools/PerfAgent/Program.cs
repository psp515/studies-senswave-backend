using Perf.Core;
using Perf.Core.Agent;
using PerfAgent;
using PerfAgent.Activation;
using PerfAgent.Database;
using PerfAgent.Docker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = PerfJson.Options.PropertyNamingPolicy;
    foreach (var converter in PerfJson.Options.Converters)
        o.SerializerOptions.Converters.Add(converter);
});
builder.Services.AddSingleton<IDockerEngine, DockerEngine>();
builder.Services.AddSingleton<IDatabaseAdmin, DatabaseAdmin>();
builder.Services.AddSingleton<ActivationService>();

var app = builder.Build();

if (AgentTokenPolicy.Problem(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value.Token) is { } tokenProblem)
    app.Logger.LogError("{Problem}: every request will be answered with 401. Generate one with 'openssl rand -hex 32' and set it as AGENT_TOKEN in performance/.env.", tokenProblem);

app.UseMiddleware<BearerTokenMiddleware>();

app.MapGet("/status", (ActivationService s, CancellationToken ct) => s.Status(ct));
// Mutating operations are multi-step and must not be cancelled by a client timeout/disconnect: they only stop with the host.
app.MapPost("/activate", (ActivateRequest request, ActivationService s, IHostApplicationLifetime life) => Guard(() => s.Activate(request, life.ApplicationStopping)));
app.MapPost("/stop", (ActivationService s, IHostApplicationLifetime life) => Guard(() => s.Stop(life.ApplicationStopping)));
app.MapPost("/prepare", (ActivationService s, IHostApplicationLifetime life) => Guard(async () => new { seedLog = await s.Prepare(life.ApplicationStopping) }));
app.MapGet("/logs/{transport}", async (Transport transport, long? sinceUnix, ActivationService s, CancellationToken ct) =>
    Results.Text(await s.Logs(transport, sinceUnix ?? 0, ct)));

// Network check (PerfRunner, before and after a session): throughput of the path between the load side and this VM.
const long MaxNetcheckBytes = 256L * 1024 * 1024;
app.MapGet("/netcheck/download", async (long bytes, HttpContext context, CancellationToken ct) =>
{
    if (bytes is <= 0 or > MaxNetcheckBytes)
        return Results.BadRequest(new { error = $"bytes must be 1..{MaxNetcheckBytes}" });

    context.Response.ContentType = "application/octet-stream";
    context.Response.ContentLength = bytes;
    var chunk = new byte[64 * 1024];
    for (var left = bytes; left > 0; left -= chunk.Length)
        await context.Response.Body.WriteAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, left)), ct);
    return Results.Empty;
});
app.MapPost("/netcheck/upload", async (HttpRequest request, CancellationToken ct) =>
{
    var buffer = new byte[64 * 1024];
    long total = 0;
    int read;
    while ((read = await request.Body.ReadAsync(buffer, ct)) > 0)
        total += read;
    return Results.Ok(new { bytes = total });
});

app.Run();

static async Task<IResult> Guard<T>(Func<Task<T>> operation)
{
    try
    {
        return Results.Ok(await operation());
    }
    catch (AgentBusyException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
}
