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

app.UseMiddleware<BearerTokenMiddleware>();

app.MapGet("/status", (ActivationService s, CancellationToken ct) => s.Status(ct));
app.MapPost("/activate", (ActivateRequest request, ActivationService s, CancellationToken ct) => Guard(() => s.Activate(request, ct)));
app.MapPost("/stop", (ActivationService s, CancellationToken ct) => Guard(() => s.Stop(ct)));
app.MapPost("/prepare", (ActivationService s, CancellationToken ct) => Guard(async () => new { seedLog = await s.Prepare(ct) }));
app.MapGet("/logs/{transport}", async (Transport transport, long? sinceUnix, ActivationService s, CancellationToken ct) =>
    Results.Text(await s.Logs(transport, sinceUnix ?? 0, ct)));

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
