using System.Net;
using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class BackendApiErrorMessagesTests
{
    [Fact]
    public void GetFriendlyMessage_ShouldReturnVietnameseMessageForTooManyRequests()
    {
        var exception = new HttpRequestException(
            "Response status code does not indicate success: 429 (Too Many Requests).",
            null,
            HttpStatusCode.TooManyRequests);

        var message = BackendApiErrorMessages.GetFriendlyMessage(exception);

        message.Should().Be(BackendApiErrorMessages.TooManyRequestsMessage);
    }

    [Fact]
    public void GetFriendlyMessage_ShouldKeepOriginalMessageForOtherErrors()
    {
        var exception = new HttpRequestException("Backend failed.", null, HttpStatusCode.InternalServerError);

        var message = BackendApiErrorMessages.GetFriendlyMessage(exception);

        message.Should().Be("Backend failed.");
    }
}
