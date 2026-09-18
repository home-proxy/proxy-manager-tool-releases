using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class FetchProxyFromKeyForm : Form
{
    private readonly IProxyOrderApiClient _proxyOrderApiClient;
    private readonly TextBox _keysTextBox = new();
    private readonly TextBox _resultsTextBox = new();
    private readonly Button _checkButton;
    private readonly Button _rotateButton;
    private readonly Button _copyButton;
    private readonly List<ProxyRotateResult> _results = [];

    public FetchProxyFromKeyForm(IProxyOrderApiClient proxyOrderApiClient)
    {
        _proxyOrderApiClient = proxyOrderApiClient;
        _checkButton = LightTheme.CreateToolbarButton("Xem proxy hien tai", async (_, _) => await FetchAsync(checkOnly: true), LightTheme.Accent, UiIcons.NewViewProxy);
        _rotateButton = LightTheme.CreateToolbarButton("Xoay", async (_, _) => await FetchAsync(checkOnly: false), LightTheme.Warning, UiIcons.NewRotateProxyHover);
        _copyButton = LightTheme.CreateToolbarButton("Copy proxy thanh cong", (_, _) => CopySuccessfulProxies(), LightTheme.Success, UiIcons.NewCopyHover);

        Text = "Lay Proxy tu key";
        Width = 760;
        Height = 620;
        MinimumSize = new Size(760, 620);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Nhap key, moi dong mot key", AutoSize = true }, 0, 0);
        _keysTextBox.Dock = DockStyle.Fill;
        _keysTextBox.Multiline = true;
        _keysTextBox.ScrollBars = ScrollBars.Both;
        _keysTextBox.WordWrap = false;
        _keysTextBox.Font = new Font("Consolas", 9);
        layout.Controls.Add(_keysTextBox, 0, 1);

        layout.Controls.Add(new Label { Text = "Ket qua", AutoSize = true, Margin = new Padding(0, 10, 0, 4) }, 0, 2);
        _resultsTextBox.Dock = DockStyle.Fill;
        _resultsTextBox.Multiline = true;
        _resultsTextBox.ReadOnly = true;
        _resultsTextBox.ScrollBars = ScrollBars.Both;
        _resultsTextBox.WordWrap = false;
        _resultsTextBox.Font = new Font("Consolas", 9);
        layout.Controls.Add(_resultsTextBox, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var closeButton = LightTheme.CreateToolbarButton("Dong", (_, _) => DialogResult = DialogResult.Cancel, LightTheme.Muted);
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(_copyButton);
        buttons.Controls.Add(_rotateButton);
        buttons.Controls.Add(_checkButton);
        layout.Controls.Add(buttons, 0, 4);

        Controls.Add(layout);
        CancelButton = closeButton;
    }

    private async Task FetchAsync(bool checkOnly)
    {
        var keys = _keysTextBox.Text
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();
        if (keys.Count == 0)
        {
            MessageBox.Show(this, "Hay nhap it nhat 1 key.", "Thieu du lieu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        _results.Clear();
        _resultsTextBox.Clear();

        try
        {
            foreach (var key in keys)
            {
                try
                {
                    var result = await _proxyOrderApiClient.FetchProxyByRotateTokenAsync(key, checkOnly, CancellationToken.None);
                    _results.Add(result);
                    AppendResult(key, result);
                }
                catch (Exception ex)
                {
                    var failed = new ProxyRotateResult { Message = ex.Message };
                    _results.Add(failed);
                    AppendResult(key, failed);
                }
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AppendResult(string key, ProxyRotateResult result)
    {
        var status = result.IsSuccess ? "OK" : "FAIL";
        var message = result.IsSuccess ? result.Proxy : result.Message;
        _resultsTextBox.AppendText($"{status}\t{key}\t{message}{Environment.NewLine}");
    }

    private void CopySuccessfulProxies()
    {
        var text = string.Join(Environment.NewLine, _results.Where(result => result.IsSuccess).Select(result => result.Proxy));
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(this, "Khong co proxy thanh cong de copy.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(text);
    }

    private void SetBusy(bool busy)
    {
        _checkButton.Enabled = !busy;
        _rotateButton.Enabled = !busy;
        _copyButton.Enabled = !busy;
        _keysTextBox.Enabled = !busy;
    }
}
