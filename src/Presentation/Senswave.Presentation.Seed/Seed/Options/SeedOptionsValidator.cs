using Microsoft.Extensions.Options;

namespace Senswave.Presentation.Seed.Seed.Options;

/// <summary>Stops the seed at startup when generated users would share a topic.</summary>
public sealed class SeedOptionsValidator : IValidateOptions<SeedOptions>
{
    public ValidateOptionsResult Validate(string? name, SeedOptions options)
    {
        var errors = options.GeneratedUsers.TopicPatternErrors().ToList();

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}
