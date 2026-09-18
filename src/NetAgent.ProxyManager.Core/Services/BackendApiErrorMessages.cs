using System.Net;

namespace NetAgent.ProxyManager.Core.Services;

public static class BackendApiErrorMessages
{
    public const string TooManyRequestsMessage =
        "Hệ thống ProxyManager đang nhận quá nhiều yêu cầu. Vui lòng chờ một lát rồi thử lại.";

    public static string GetFriendlyMessage(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
        {
            return TooManyRequestsMessage;
        }

        return exception.Message;
    }
}
