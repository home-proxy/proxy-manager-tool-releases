using System.Net;
using System.Net.Sockets;
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

    [Fact]
    public void GetFriendlyMessage_ShouldExplainConnectionRefusedWithoutClearingTheSession()
    {
        var exception = new HttpRequestException(
            "No connection could be made because the target machine actively refused it. (api.homeproxy.vn:443)",
            new SocketException((int)SocketError.ConnectionRefused));

        var message = BackendApiErrorMessages.GetFriendlyMessage(exception);

        message.Should().Be(BackendApiErrorMessages.ConnectionFailedMessage);
        BackendApiErrorMessages.IsConnectionFailure(exception).Should().BeTrue();
    }
}
