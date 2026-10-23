using System.Net;
using System.Net.Sockets;

namespace NetAgent.ProxyManager.Core.Services;

public static class BackendApiErrorMessages
{
    public const string TooManyRequestsMessage =
        "Hệ thống ProxyManager đang nhận quá nhiều yêu cầu. Vui lòng chờ một lát rồi thử lại.";

    public const string ConnectionFailedMessage =
        "Không kết nối được máy chủ. Phiên đăng nhập vẫn còn. Hãy bấm Làm mới để thử lại.";

    public static string GetFriendlyMessage(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
        {
            return TooManyRequestsMessage;
        }

        if (IsConnectionFailure(exception))
        {
            return ConnectionFailedMessage;
        }

        return exception.Message;
    }

    public static bool IsConnectionFailure(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException http)
            {
                if (http.StatusCode is not null)
                {
                    return false;
                }

                if (IsConnectionMessage(http.Message))
                {
                    return true;
                }
            }

            if (current is SocketException or IOException)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConnectionMessage(string message) =>
        message.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Không kết nối", StringComparison.OrdinalIgnoreCase);
}
