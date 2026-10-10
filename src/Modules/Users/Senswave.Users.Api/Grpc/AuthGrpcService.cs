using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Senswave.Users.Domain.Entity;
using Senswave.Users.Infrastructure.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Grpc;

/// <summary>
/// gRPC port of the ASP.NET Identity API authentication routes (<c>MapIdentityApi&lt;User&gt;</c>).
/// Bearer only: the cookie modes have no gRPC equivalent.
/// </summary>
[AllowAnonymous]
internal sealed class AuthGrpcService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IUserStore<User> userStore,
    IEmailSender<User> emailSender,
    IAccessTokenIssuer accessTokenIssuer) : Proto.AuthService.AuthServiceBase
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public override async Task<Proto.RegisterResponse> Register(Proto.RegisterRequest request, ServerCallContext context)
    {
        var email = request.Email;

        if (string.IsNullOrEmpty(email) || !EmailAddressAttribute.IsValid(email))
            IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)).ThrowIfFailure();

        if (!userManager.SupportsUserEmail)
            throw new NotSupportedException("Registration requires a user store with email support.");

        // Store-level setters, as in the framework: the user is not persisted yet, so UserManager setters would fail.
        var user = new User();
        await userStore.SetUserNameAsync(user, email, context.CancellationToken);
        await ((IUserEmailStore<User>)userStore).SetEmailAsync(user, email, context.CancellationToken);

        var result = await userManager.CreateAsync(user, request.Password);
        result.ThrowIfFailure();

        await userManager.SendConfirmationEmailAsync(emailSender, context.GetHttpContext(), user, email);

        return new Proto.RegisterResponse();
    }

    public override async Task<Proto.AccessTokenResponse> Login(Proto.LoginRequest request, ServerCallContext context)
    {
        // PasswordSignInAsync semantics without SignInAsync (whose bearer handler would write JSON into the response).
        var user = await userManager.FindByNameAsync(request.Email);

        if (user is null)
            throw IdentityGrpcExtensions.Unauthenticated(IdentitySignInResult.Failed.ToString());

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
            throw IdentityGrpcExtensions.Unauthenticated(result.ToString());

        if (await IsTwoFactorRequired(user))
            await VerifyTwoFactor(user, request);

        var token = await accessTokenIssuer.IssueAsync(user, context.CancellationToken);

        return token.ToProto();
    }

    public override async Task<Proto.AccessTokenResponse> Refresh(Proto.RefreshRequest request, ServerCallContext context)
    {
        var result = await accessTokenIssuer.RefreshAsync(request.RefreshToken, context.CancellationToken);

        if (result.IsFailure)
            throw IdentityGrpcExtensions.Unauthenticated();

        return result.Data.ToProto();
    }

    public override async Task<Proto.ConfirmEmailResponse> ConfirmEmail(Proto.ConfirmEmailRequest request, ServerCallContext context)
    {
        if (await userManager.FindByIdAsync(request.UserId) is not { } user)
            throw IdentityGrpcExtensions.Unauthenticated();

        string code;

        try
        {
            code = IdentityGrpcExtensions.DecodeCode(request.Code);
        }
        catch (FormatException)
        {
            throw IdentityGrpcExtensions.Unauthenticated();
        }

        IdentityResult result;

        if (!request.HasChangedEmail || string.IsNullOrEmpty(request.ChangedEmail))
        {
            result = await userManager.ConfirmEmailAsync(user, code);
        }
        else
        {
            // As in the framework: the user name and email are kept in sync on email change.
            result = await userManager.ChangeEmailAsync(user, request.ChangedEmail, code);

            if (result.Succeeded)
                result = await userManager.SetUserNameAsync(user, request.ChangedEmail);
        }

        if (!result.Succeeded)
            throw IdentityGrpcExtensions.Unauthenticated();

        return new Proto.ConfirmEmailResponse();
    }

    public override async Task<Proto.ResendConfirmationEmailResponse> ResendConfirmationEmail(
        Proto.ResendConfirmationEmailRequest request, ServerCallContext context)
    {
        // Always succeeds, so the response does not reveal whether the user exists.
        if (await userManager.FindByEmailAsync(request.Email) is { } user)
            await userManager.SendConfirmationEmailAsync(emailSender, context.GetHttpContext(), user, request.Email);

        return new Proto.ResendConfirmationEmailResponse();
    }

    public override async Task<Proto.ForgotPasswordResponse> ForgotPassword(Proto.ForgotPasswordRequest request, ServerCallContext context)
    {
        // Always succeeds, so the response does not reveal whether the user exists.
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            await emailSender.SendPasswordResetCodeAsync(user, request.Email, HtmlEncoder.Default.Encode(code));
        }

        return new Proto.ForgotPasswordResponse();
    }

    public override async Task<Proto.ResetPasswordResponse> ResetPassword(Proto.ResetPasswordRequest request, ServerCallContext context)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken()).ThrowIfFailure();

        IdentityResult result;

        try
        {
            var code = IdentityGrpcExtensions.DecodeCode(request.ResetCode);
            result = await userManager.ResetPasswordAsync(user!, code, request.NewPassword);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken());
        }

        result.ThrowIfFailure();

        return new Proto.ResetPasswordResponse();
    }

    private async Task<bool> IsTwoFactorRequired(User user) =>
        userManager.SupportsUserTwoFactor &&
        await userManager.GetTwoFactorEnabledAsync(user) &&
        (await userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0;

    private async Task VerifyTwoFactor(User user, Proto.LoginRequest request)
    {
        if (request.HasTwoFactorCode && !string.IsNullOrEmpty(request.TwoFactorCode))
        {
            var code = request.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var isValid = await userManager.VerifyTwoFactorTokenAsync(
                user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

            if (isValid)
                return;

            await userManager.AccessFailedAsync(user);
            throw IdentityGrpcExtensions.Unauthenticated(IdentitySignInResult.Failed.ToString());
        }

        if (request.HasTwoFactorRecoveryCode && !string.IsNullOrEmpty(request.TwoFactorRecoveryCode))
        {
            var result = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.TwoFactorRecoveryCode);

            if (result.Succeeded)
                return;

            await userManager.AccessFailedAsync(user);
            throw IdentityGrpcExtensions.Unauthenticated(IdentitySignInResult.Failed.ToString());
        }

        throw IdentityGrpcExtensions.Unauthenticated(IdentitySignInResult.TwoFactorRequired.ToString());
    }
}
