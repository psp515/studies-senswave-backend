using Senswave.DataSources.Domain.Brokers.Clients.Options;

namespace Senswave.DataSources.Domain.Brokers.Brokers.Options;

public class BrokerOptions
{
    public const string SectionName = "Modules:DataSource:Brokers";

    public bool TestConnectionOnChange { get; set; } = true;
    public bool IsCluster { get; set; } = false;

    /// <summary>
    /// When true, many users may register a broker with the same URL and port.
    /// Client names must still be unique per broker.
    /// </summary>
    public bool AllowSharedBrokers { get; set; } = false;
    public ClientOptions Client { get; set; } = new();
    public LimitsOptions Limits { get; set; } = new();
}
