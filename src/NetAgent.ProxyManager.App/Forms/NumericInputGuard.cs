namespace NetAgent.ProxyManager.App.Forms;

internal static class NumericInputGuard
{
    public static void AllowDigitsOnly(NumericUpDown input)
    {
        var updatingText = false;

        input.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };

        input.TextChanged += (_, _) =>
        {
            if (updatingText || input.Text.All(char.IsDigit))
            {
                return;
            }

            updatingText = true;
            input.Text = new string(input.Text.Where(char.IsDigit).ToArray());
            updatingText = false;
        };
    }
}
