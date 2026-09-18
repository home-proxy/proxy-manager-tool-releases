using NetAgent.ProxyManager.App.Controls;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class AddApplicationRuleForm : Form
{
    private const int FooterHeight = 60;

    private static readonly Color FooterBackground = ColorTranslator.FromHtml("#F7F7F9");
    private static readonly Color FooterBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color PrimaryButtonColor = Color.FromArgb(15, 108, 189);

    private readonly RunningApplicationSelectionControl _selectionControl;
    private bool _isConfirming;

    public AddApplicationRuleForm(
        IEmulatorProcessScanner emulatorProcessScanner,
        IAppProcessScanner processScanner)
    {
        Text = "Thêm ứng dụng";
        Size = new Size(1400, 900);
        MinimumSize = new Size(960, 551);
        StartPosition = FormStartPosition.CenterParent;

        _selectionControl = new RunningApplicationSelectionControl(
            emulatorProcessScanner,
            processScanner,
            ApplicationSelectionMode.AddMultiple);

        Controls.Add(BuildLayout());
        LightTheme.Apply(this);
        RestoreTaggedBackColors(this);
        Shown += async (_, _) => await _selectionControl.EnsureLoadedAsync();
    }

    public IReadOnlyList<ApplicationRule> Rules { get; private set; } = [];

    public void LoadExistingRules(IReadOnlyList<ApplicationRule> rules) =>
        _selectionControl.LoadAddContext(rules);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            Confirm();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15)
        };
        body.Controls.Add(_selectionControl);

        root.Controls.Add(body, 0, 0);
        root.Controls.Add(BuildFooter(), 0, 1);
        return root;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Tag = FooterBackground,
            Padding = Padding.Empty
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(FooterBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 15, 10),
            BackColor = FooterBackground,
            Tag = FooterBackground
        };
        buttons.Controls.Add(CreateFooterButton("Hủy", (_, _) => DialogResult = DialogResult.Cancel, filled: false));
        buttons.Controls.Add(CreateFooterButton("OK", (_, _) => Confirm(), filled: true));
        footer.Controls.Add(buttons);
        return footer;
    }

    private static Button CreateFooterButton(string text, EventHandler onClick, bool filled)
    {
        var button = new Button
        {
            Text = text,
            Name = filled ? "AddApplicationFooterOkButton" : "AddApplicationFooterCancelButton",
            Size = new Size(text == "OK" ? 66 : 86, 40),
            MinimumSize = new Size(text == "OK" ? 66 : 86, 40),
            AutoSize = false,
            Margin = new Padding(0, 0, 8, 0),
            Padding = Padding.Empty,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = filled ? PrimaryButtonColor : Color.White,
            ForeColor = filled ? Color.White : Color.FromArgb(36, 36, 36),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Tag = LightTheme.SkipButtonThemeTag
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = filled ? PrimaryButtonColor : Color.FromArgb(209, 209, 209);
        button.FlatAppearance.MouseOverBackColor = filled ? Color.FromArgb(18, 121, 211) : Color.FromArgb(245, 245, 245);
        button.FlatAppearance.MouseDownBackColor = filled ? Color.FromArgb(12, 88, 154) : Color.FromArgb(238, 238, 238);
        button.Click += onClick;
        return button;
    }

    private void RestoreTaggedBackColors(Control control)
    {
        if (control.Tag is Color color)
        {
            control.BackColor = color;
        }

        if (control is Button button)
        {
            RestoreFooterButtonStyle(button);
        }

        foreach (Control child in control.Controls)
        {
            RestoreTaggedBackColors(child);
        }
    }

    private static void RestoreFooterButtonStyle(Button button)
    {
        switch (button.Name)
        {
            case "AddApplicationFooterOkButton":
                button.BackColor = PrimaryButtonColor;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = PrimaryButtonColor;
                break;
            case "AddApplicationFooterCancelButton":
                button.BackColor = Color.White;
                button.ForeColor = Color.FromArgb(36, 36, 36);
                button.FlatAppearance.BorderColor = Color.FromArgb(209, 209, 209);
                break;
        }
    }

    private void Confirm()
    {
        if (_isConfirming || DialogResult == DialogResult.OK)
        {
            return;
        }

        _isConfirming = true;
        if (!_selectionControl.TryCreateSelectedRules(this, out var rules))
        {
            _isConfirming = false;
            return;
        }

        Rules = rules;
        DialogResult = DialogResult.OK;
    }
}
