namespace Perf.Core.Runs;

public sealed record UserData(string Email, string AccessToken, string HomeId, string DeviceId, string DashboardId);

public sealed record RunData(IReadOnlyList<UserData> Users);
