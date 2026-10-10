using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Senswave.Users.Domain;
using Senswave.Users.Domain.Entity;
using System.Text;

namespace Senswave.Users.Api.Auth.ConfirmEmailV2;

public class ConfirmEmailV2Endpoint : IEndpoint, INavigationEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("auth/confirmEmail", ConfirmEmailV2)
            .MapToApiVersion(2)
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithTags(UsersModule.AuthorizationTag)
            .WithGroupName(UsersModule.GroupName);
    }

    private async Task<IResult> ConfirmEmailV2(
        IOptionsSnapshot<UsersOptions> options,
        ILogger<ConfirmEmailV2Endpoint> logger,
        UserManager<User> userManager,
        [FromQuery] string userId,
        [FromQuery] string code)
    {
        User? user = null;

        try
        {
            user = await userManager.FindByIdAsync(userId);

            if (user is null)
            {
                logger.LogWarning("[User: {userId}] User not found.", userId);
                return TypedResults.Unauthorized();
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            logger.LogError("[User: {userId}] Failed to decode code when confirming email.", userId);
            return TypedResults.Unauthorized();
        }

        var result = await userManager.ConfirmEmailAsync(user, code);

        if (!result.Succeeded)
        {
            logger.LogError("[User: {userId}] Failed to confirm email.", userId);
            return TypedResults.Redirect(options.Value.ConfirmEmailV2.FailureRedirect);
        }

        logger.LogInformation("[User: {userId}] Email confirmed.", userId);
        return Results.Redirect(options.Value.ConfirmEmailV2.SuccessRedirect);
    }
}
