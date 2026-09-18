using System.Net;

namespace NetAgent.ProxyManager.Core.Models;

public sealed class AuthApiException : Exception
{
    public AuthApiException(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, string> fieldErrors,
        string message = "Authentication request failed.")
        : base(message)
    {
        StatusCode = statusCode;
        FieldErrors = fieldErrors;
    }

    public HttpStatusCode StatusCode { get; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; }

    public string? GetFieldError(string fieldName) =>
        FieldErrors.TryGetValue(fieldName, out var code) ? code : null;

    public bool HasError(string fieldName, string errorCode) =>
        string.Equals(GetFieldError(fieldName), errorCode, StringComparison.OrdinalIgnoreCase);
}
