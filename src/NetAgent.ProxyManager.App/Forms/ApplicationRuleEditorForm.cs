using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ApplicationRuleEditorForm : Form
{
    private readonly AppTextInput _executableNameTextBox = new();
    private readonly AppButton _browseButton = new();
    private Button? _saveButton;
    private ApplicationRule? _target;
    private bool _isSaving;

    public ApplicationRuleEditorForm()
    {
        Text = "Thêm / Sửa file .exe";
        Width = 620;
        Height = 240;
        MinimumSize = new Size(620, 240);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(BuildLayout());
        LightTheme.Apply(this);
    }

    public ApplicationRule? Rule => _target;

    public void LoadRule(ApplicationRule rule)
    {
        _target = rule;
        _executableNameTextBox.Text = rule.ExecutableName;
        SetExecutableEditorEnabled(rule.TargetType == ApplicationTargetType.Executable);
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 3,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "File .exe", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _executableNameTextBox.Dock = DockStyle.Top;
        _executableNameTextBox.ReadOnly = true;
        layout.Controls.Add(_executableNameTextBox, 1, 0);

        _browseButton.Text = "Chọn file";
        _browseButton.AutoSize = true;
        _browseButton.Variant = AppButtonVariant.Secondary;
        _browseButton.AccentColor = LightTheme.Accent;
        _browseButton.Click += (_, _) => BrowseExecutable();
        layout.Controls.Add(_browseButton, 2, 0);

        var note = new Label
        {
            Text = "Chỉ có thể đổi ứng dụng bằng cách chọn file .exe từ máy tính.",
            AutoSize = true,
            ForeColor = LightTheme.Muted
        };
        layout.Controls.Add(note, 0, 1);
        layout.SetColumnSpan(note, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var cancel = LightTheme.CreateToolbarButton("Hủy", (_, _) => DialogResult = DialogResult.Cancel, LightTheme.Muted);
        var save = LightTheme.CreateToolbarButton("Lưu", (_, _) => SaveRule(), LightTheme.Success);
        _saveButton = save;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 3);
        AcceptButton = _saveButton;
        return layout;
    }

    private void SetExecutableEditorEnabled(bool enabled)
    {
        _executableNameTextBox.ReadOnly = true;
        _browseButton.Enabled = enabled;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            SaveRule();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BrowseExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Ứng dụng Windows (*.exe)|*.exe",
            Title = "Chọn file .exe"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _executableNameTextBox.Text = dialog.FileName;
        }
    }

    private void SaveRule()
    {
        if (_isSaving || DialogResult == DialogResult.OK)
        {
            return;
        }

        _isSaving = true;
        if (_target is null)
        {
            DialogResult = DialogResult.Cancel;
            return;
        }

        if (_target.TargetType != ApplicationTargetType.Executable)
        {
            MessageBox.Show(this, "Chỉ có thể sửa file .exe của ứng dụng thông thường.", "Không thể sửa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _isSaving = false;
            return;
        }

        var executableName = _executableNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(executableName) ||
            !ApplicationRule.GetExecutableDisplayName(executableName).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "File ứng dụng phải kết thúc bằng .exe.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _isSaving = false;
            return;
        }

        _target.ExecutableName = executableName;
        DialogResult = DialogResult.OK;
    }
}
