using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Abstractions.Modules;
using Senswave.Abstractions.Persistence;
using Senswave.Infrastructure.Grpc.Modules;
using Senswave.Users.Api.Grpc;
using Senswave.Users.Application;
using Senswave.Users.Domain.Diagnostics;
using Senswave.Users.Domain.ValueObjects;
using Senswave.Users.Infrastructure;

namespace Senswave.Users.Api;

public class UsersModule : ISenswaveModule, ISenswaveGrpcModule
{
    public const string ModuleName = "User";

    public const string DefaultListenerName = UsersActivityProvider.DefaultListenerName;

    public const string UserTag = $"{ModuleName} - Users";
    public const string AuthorizationTag = $"{ModuleName} - Authorization";
    public const string LegalTag = $"{ModuleName} - Legal";

    public static string GroupName => ModuleName.ToLower();

    public string Name => ModuleName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(UsersModule).Assembly;

        services.AddMinimalApiEndpoints(assembly);

        services.Configure<IdentityOptions>(options =>
        {
            // Password settings.
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 10;
            options.Password.RequiredUniqueChars = 1;

            // Lockout settings.
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings.
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            options.User.RequireUniqueEmail = true;

            // SignIn settings.
            options.SignIn.RequireConfirmedEmail = true;

            // ForgotPasswordSettings
            options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
        });

        services.Configure<DataProtectionTokenProviderOptions>(o =>
        {
            o.TokenLifespan = TimeSpan.FromDays(1);
        });

        services.Configure<DataProtectionTokenProviderOptions>(TokenOptions.DefaultEmailProvider, o =>
        {
            o.TokenLifespan = TimeSpan.FromMinutes(15);
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(RoleTypes.User.Policy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
                    policy.RequireRole([RoleTypes.User.Policy]);
                })
            .AddPolicy(RoleTypes.Admin.Policy, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
                    policy.RequireRole([RoleTypes.Admin.Policy]);
                });

        services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        services.AddTransient<IDatabaseInitializer, RoleInitializer>();

        services.AddUsersApplication(configuration)
            .AddUsersInfrastructure(configuration);
    }

    public void MapGrpcServices(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<AuthGrpcService>();
        endpoints.MapGrpcService<AccountGrpcService>();
        endpoints.MapGrpcService<UsersGrpcService>();
        endpoints.MapGrpcService<LegalGrpcService>();
    }
}