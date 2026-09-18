using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.App.Theme;

internal static class UiFeedback
{
    public static void ShowInfo(IWin32Window owner, string message, string title = "Thông báo")
    {
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void ShowWarning(IWin32Window owner, Exception exception, string title)
    {
        MessageBox.Show(owner, GetFriendlyErrorMessage(exception), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void ShowWarning(IWin32Window owner, string message, string title = "Cảnh báo")
    {
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static string GetFriendlyErrorMessage(Exception exception) =>
        BackendApiErrorMessages.GetFriendlyMessage(exception);
}
