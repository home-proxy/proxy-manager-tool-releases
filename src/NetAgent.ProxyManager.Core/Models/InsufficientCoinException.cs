namespace NetAgent.ProxyManager.Core.Models;

public sealed class InsufficientCoinException : Exception
{
    public InsufficientCoinException(string? serverMessage = null)
        : base(string.IsNullOrWhiteSpace(serverMessage)
            ? "Số dư ví không đủ để tạo đơn hàng."
            : serverMessage)
    {
    }
}
