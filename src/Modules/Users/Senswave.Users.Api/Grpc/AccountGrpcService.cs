using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Senswave.Users.Domain.Entity;
using System.ComponentModel.DataAnnotations;
using Proto = Senswave.Contracts.Users.V1;

namespace Senswave.Users.Api.Grpc;

/// <summary>
/// gRPC port of the ASP.NET Identity API <c>/manage</c> routes (<c>MapIdentityApi&lt;User&gt;</c>).
/// </summary>
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class AccountGrpcService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IEmailSender<User> emailSender) : Proto.AccountService.AccountServiceBase
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public override async Task<Proto.GetInfoResponse> GetInfo(Proto.GetInfoRequest request, ServerCallContext context)
    {
        var user = await GetUser(context);

        return new Proto.GetInfoResponse
        {
            Email = await userManager.GetEmailAsync(user) ?? throw new NotSupportedException("Users must have an email."),
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
        };
    }

    public override async Task<Proto.UpdateInfoResponse> UpdateInfo(Proto.UpdateInfoRequest request, ServerCallContext context)
    {
        var user = await GetUser(context);

        if (request.HasNewEmail && !string.IsNullOrEmpty(request.NewEmail) && !EmailAddressAttribute.IsValid(request.NewEmail))
            IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(request.NewEmail)).ThrowIfFailure();

        if (request.HasNewPassword && !string.IsNullOrEmpty(request.NewPassword))
        {
            if (!request.HasOldPassword || string.IsNullOrEmpty(request.OldPassword))
                throw IdentityGrpcExtensions.ValidationFailure("OldPasswordRequired",
                    "The old password is required to set a new password. If the old password is forgotten, use ResetPassword.");

            var changePasswordResult = await userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
            changePasswordResult.ThrowIfFailure();
        }

        if (request.HasNewEmail && !string.IsNullOrEmpty(request.NewEmail))
        {
            var email = await userManager.GetEmailAsync(user);

            if (email != request.NewEmail)
                await userManager.SendConfirmationEmailAsync(emailSender, context.GetHttpContext(), user, request.NewEmail, isChange: true);
        }

        return new Proto.UpdateInfoResponse
        {
            Email = await userManager.GetEmailAsync(user) ?? throw new NotSupportedException("Users must have an email."),
            IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
        };
    }

    public override async Task<Proto.TwoFactorResponse> TwoFactor(Proto.TwoFactorRequest request, ServerCallContext context)
    {
        var user = await GetUser(context);

        if (request.HasEnable && request.Enable)
        {
            if (request.ResetSharedKey)
                throw IdentityGrpcExtensions.ValidationFailure("CannotResetSharedKeyAndEnable",
                    "Resetting the 2fa shared key must disable 2fa until a 2fa token based on the new shared key is validated.");

            if (!request.HasTwoFactorCode || string.IsNullOrEmpty(request.TwoFactorCode))
                throw IdentityGrpcExtensions.ValidationFailure("RequiresTwoFactor",
                    "No 2fa token was provided by the request. A valid 2fa token is required to enable 2fa.");

            if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.TwoFactorCode))
                throw IdentityGrpcExtensions.ValidationFailure("InvalidTwoFactorCode",
                    "The 2fa token provided by the request was invalid. A valid 2fa token is required to enable 2fa.");

            await userManager.SetTwoFactorEnabledAsync(user, true);
        }
        else if ((request.HasEnable && !request.Enable) || request.ResetSharedKey)
        {
            await userManager.SetTwoFactorEnabledAsync(user, false);
        }

        if (request.ResetSharedKey)
            await userManager.ResetAuthenticatorKeyAsync(user);

        string[]? recoveryCodes = null;

        if (request.ResetRecoveryCodes || (request.HasEnable && request.Enable && await userManager.CountRecoveryCodesAsync(user) == 0))
            recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();

        if (request.ForgetMachine)
            await signInManager.ForgetTwoFactorClientAsync();

        var key = await userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);

            if (string.IsNullOrEmpty(key))
                throw new NotSupportedException("The user manager must produce an authenticator key after reset.");
        }

        var response = new Proto.TwoFactorResponse
        {
            SharedKey = key,
            RecoveryCodesLeft = recoveryCodes?.Length ?? await userManager.CountRecoveryCodesAsync(user),
            IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
        };

        if (recoveryCodes is not null)
            response.RecoveryCodes.AddRange(recoveryCodes);

        return response;
    }

    private async Task<User> GetUser(ServerCallContext context) =>
        await userManager.GetUserAsync(context.GetHttpContext().User) ?? throw IdentityGrpcExtensions.UserNotFound();
}
