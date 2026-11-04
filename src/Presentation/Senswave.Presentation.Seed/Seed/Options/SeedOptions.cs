using Senswave.Presentation.Seed.Users.Options;

namespace Senswave.Presentation.Seed.Seed.Options;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string Instance { get; set; } = string.Empty;

    public UserOptions? DefaultUser { get; set; }

    public List<UserOptions> Users { get; set; } = [];

    public GeneratedUsersOptions GeneratedUsers { get; set; } = new();

    /// <summary>How many users are seeded at the same time. 1 keeps the original one-after-another order.</summary>
    public int Parallelism { get; set; } = 1;
}
