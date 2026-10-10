using Microsoft.AspNetCore.Mvc.Testing;
using Senswave.Api;
using Senswave.Contracts.Users.V1;

namespace Senswave.Users.EndTestsGrpc;

public abstract class UsersGrpcFeatureTest(WebApplicationFactory<Program> factory) : GrpcFeatureTest(factory)
{
    protected const string Password = "Abecadło712576!!@#$%$";

    /// <summary>Registers and confirms a fresh user over gRPC, so seeded users stay untouched.</summary>
    protected async Task<string> RegisterConfirmedUserAsync()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        var emails = new EmailCapture(Environment, email);
        var auth = new AuthService.AuthServiceClient(CreateChannel());

        await auth.RegisterAsync(new RegisterRequest { Email = email, Password = Password });
        var (userId, code) = emails.ConfirmationLinkQuery();
        await auth.ConfirmEmailAsync(new ConfirmEmailRequest { UserId = userId, Code = code });

        return email;
    }
}
