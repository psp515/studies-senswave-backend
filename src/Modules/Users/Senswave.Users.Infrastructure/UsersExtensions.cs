using MassTransit.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Senswave.Users.Domain.Entity;
using Senswave.Users.Domain.Interfaces;
using Senswave.Users.Domain.Repositories;
using Senswave.Users.Infrastructure.Consumers;
using Senswave.Users.Infrastructure.Options;
using Senswave.Users.Infrastructure.Repositories;
using Senswave.Users.Infrastructure.Services;
using Senswave.Users.Infrastructure.Tokens;
using Senswave.Users.Infrastructure.Workers;

namespace Senswave.Users.Infrastructure;

public static class UsersExtensions
{
    public static void AddUsersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsersContext>();

        services.AddIdentity<User, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<UsersContext>()
            .AddApiEndpoints()
            .AddDefaultTokenProviders();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAccessTokenIssuer, AccessTokenIssuer>();

        services.AddTransient<IEmailSender<User>, HtmlEmailSender>();
        services.AddTransient<IDeleteAccountService, HtmlEmailSender>();
        services.AddTransient<IEmailService, MailkitEmailService>();

        services.Configure<EmailServiceOptions>(configuration.GetSection(EmailServiceOptions.SectionName));
        services.Configure<EmailSenderOptions>(configuration.GetSection(EmailSenderOptions.SectionName));
        services.Configure<UserServiceOptions>(configuration.GetSection(UserServiceOptions.SectionName));

        services.AddScoped<ICommandUserRepository, CommandUserRepository>();
        services.AddScoped<IQueryUserRepository, QueryUserRepository>();
        services.AddScoped<IQueryLegalRepository, QueryLegalRepository>();

        services.AddHostedService<DeleteAccountBackgroundWorker>();

        services.AddScoped<IUserService, UserService>();
        services.AddTransient<ILegalService, LagalPpAndTcService>();

        services.RegisterConsumer<UserByEmailConsumer>();
        services.RegisterConsumer<UsersEmailsConsumer>();
    }
}
