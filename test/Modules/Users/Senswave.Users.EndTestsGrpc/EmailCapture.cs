using Moq;
using System.Text.RegularExpressions;

namespace Senswave.Users.EndTestsGrpc;

/// <summary>
/// Captures the last email sent to an address through the test host's <c>IEmailService</c> mock.
/// </summary>
internal sealed partial class EmailCapture
{
    private string _lastBody = string.Empty;

    public EmailCapture(GrpcTestEnvironment environment, string email)
    {
        environment.EmailServiceMock
            .Setup(x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string _, string _, string body) =>
            {
                _lastBody = body;
                return Task.CompletedTask;
            });
    }

    public (string UserId, string Code) ConfirmationLinkQuery()
    {
        var match = ConfirmationLinkRegex().Match(_lastBody);
        Assert.True(match.Success, "No confirmation link was sent.");

        return (Uri.UnescapeDataString(match.Groups["userId"].Value), Uri.UnescapeDataString(match.Groups["code"].Value));
    }

    public string ResetCode()
    {
        var match = ResetCodeRegex().Match(_lastBody);
        Assert.True(match.Success, "No password reset code was sent.");

        return match.Groups["code"].Value;
    }

    [GeneratedRegex(@"userId=(?<userId>[^&""]+)&(?:amp;)?code=(?<code>[^&""]+)")]
    private static partial Regex ConfirmationLinkRegex();

    [GeneratedRegex(@"<h2\s*>(?<code>[^<]+)</h2>")]
    private static partial Regex ResetCodeRegex();
}
