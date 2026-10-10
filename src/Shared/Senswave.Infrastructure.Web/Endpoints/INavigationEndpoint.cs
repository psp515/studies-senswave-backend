namespace Senswave.Infrastructure.Web.Endpoints;

/// <summary>
/// Marks endpoints that are browser navigation targets (e.g. links opened from an email) rather than API calls.
/// They stay HTTP in every transport mode.
/// </summary>
public interface INavigationEndpoint;
