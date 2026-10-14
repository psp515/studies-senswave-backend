using Senswave.Presentation.Seed.Users.Options;

namespace Senswave.Presentation.Seed.Seed.Options;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string Instance { get; set; } = string.Empty;

    public UserOptions? DefaultUser { get; set; }

    public List<UserOptions> Users { get; set; } = [];

    public GeneratedUsersOptions GeneratedUsers { get; set; } = new();
}
