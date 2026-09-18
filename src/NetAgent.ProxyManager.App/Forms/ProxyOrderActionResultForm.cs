using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ProxyOrderActionResultForm : Form
{
    private readonly TextBox _valueTextBox = new();
    private readonly Button _copyButton;

    public ProxyOrderActionResultForm(string title, string label, string value)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        Width = 620;
        Height = 220;
        MinimumSize = new Size(620, 220);

        _copyButton = LightTheme.CreateToolbarButton("Copy", (_, _) => CopyValue(), LightTheme.Accent, UiIcons.NewCopyHover);
        BuildUi(label, value);
        LightTheme.Apply(this);
        _copyButton.Enabled = !string.IsNullOrWhiteSpace(value);
    }

    private void BuildUi(string label, string value)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        var valueRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _valueTextBox.Dock = DockStyle.Fill;
        _valueTextBox.Multiline = true;
        _valueTextBox.ScrollBars = ScrollBars.Vertical;
        _valueTextBox.ReadOnly = true;
        _valueTextBox.Text = value;
        _valueTextBox.Margin = new Padding(0, 0, 8, 0);

        _copyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _copyButton.MinimumSize = new Size(96, 36);

        valueRow.Controls.Add(_valueTextBox, 0, 0);
        valueRow.Controls.Add(_copyButton, 1, 0);
        root.Controls.Add(valueRow, 0, 1);

        var closeButton = LightTheme.CreateToolbarButton("Đóng", (_, _) => Close(), LightTheme.Muted, UiIcons.Clear);
        closeButton.Anchor = AnchorStyles.Right;

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        };
        footer.Controls.Add(closeButton);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
    }

    private void CopyValue()
    {
        if (string.IsNullOrWhiteSpace(_valueTextBox.Text))
        {
            return;
        }

        Clipboard.SetText(_valueTextBox.Text);
    }
}
