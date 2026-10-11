using Grpc.Core;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Senswave.Abstractions.Resulting;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Users.Domain.Entity;
using System.Text;
using System.Text.Encodings.Web;
using BearerAccessTokenResponse = Microsoft.AspNetCore.Authentication.BearerToken.AccessTokenResponse;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Grpc;

/// <summary>
/// Shared pieces of the Identity API port (<c>IdentityApiEndpointRouteBuilderExtensions</c>, .NET 10).
/// </summary>
internal static class IdentityGrpcExtensions
{
    /// <summary>The email link targets <c>ConfirmEmailV2Endpoint</c>, which stays HTTP in every transport mode.</summary>
    private const string ConfirmEmailV2Path = "/api/v2/auth/confirmEmail";

    public static Proto.AccessTokenResponse ToProto(this BearerAccessTokenResponse token) => new()
    {
        TokenType = token.TokenType,
        AccessToken = token.AccessToken,
        ExpiresIn = token.ExpiresIn,
        RefreshToken = token.RefreshToken,
    };

    /// <summary>Counterpart of the framework's <c>CreateValidationProblem(IdentityResult)</c>.</summary>
    public static void ThrowIfFailure(this IdentityResult result)
    {
        if (result.Succeeded)
            return;

        throw new SenswaveRpcException(result.Errors
            .Select(x => Error.Validation(x.Code, x.Description))
            .ToArray());
    }

    public static SenswaveRpcException ValidationFailure(string code, string description) =>
        new([Error.Validation(code, description)]);

    public static RpcException Unauthenticated(string detail = "Unauthorized") =>
        new(new Status(StatusCode.Unauthenticated, detail));

    public static RpcException UserNotFound() =>
        new(new Status(StatusCode.NotFound, "Not Found"));

    public static string DecodeCode(string code) =>
        Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

    /// <summary>Port of the framework's <c>SendConfirmationEmailAsync</c>.</summary>
    public static async Task SendConfirmationEmailAsync(
        this UserManager<User> userManager,
        IEmailSender<User> emailSender,
        HttpContext httpContext,
        User user,
        string email,
        bool isChange = false)
    {
        var code = isChange
            ? await userManager.GenerateChangeEmailTokenAsync(user, email)
            : await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var userId = await userManager.GetUserIdAsync(user);

        var query = new Dictionary<string, string?>
        {
            ["userId"] = userId,
            ["code"] = code,
        };

        if (isChange)
            query.Add("changedEmail", email);

        var request = httpContext.Request;
        var confirmEmailUrl = UriHelper.BuildAbsolute(
            request.Scheme,
            request.Host,
            request.PathBase,
            ConfirmEmailV2Path,
            QueryString.Create(query));

        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
    }
}
