using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ChromeProfileNameForm : Form
{
    private const int MaxNameLength = 80;

    private readonly AppTextInput _nameInput = new("Tên Chrome profile", "Ví dụ: Shop Facebook 1");
    private readonly AppPrimaryButton _saveButton = new();
    private readonly AppButton _cancelButton = new();

    public ChromeProfileNameForm()
    {
        Text = "Tạo Chrome profile";
        Width = 460;
        Height = 230;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
    }

    public string ProfileName
    {
        get => _nameInput.TextValue.Trim();
        set => _nameInput.Text = value;
    }

    public void ConfigureForRename(string currentName)
    {
        Text = "Đổi tên Chrome profile";
        _saveButton.Text = "Lưu";
        ProfileName = currentName;
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 18, 20, 16),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _nameInput.Dock = DockStyle.Top;
        _nameInput.MaxLength = MaxNameLength;
        layout.Controls.Add(_nameInput, 0, 0);

        _saveButton.Text = "Tạo profile";
        _saveButton.AutoSize = true;
        _saveButton.Margin = new Padding(8, 0, 0, 0);
        _saveButton.Click += (_, _) => Submit();

        _cancelButton.Text = "Huỷ";
        _cancelButton.AutoSize = true;
        _cancelButton.Margin = Padding.Empty;
        _cancelButton.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_cancelButton);
        layout.Controls.Add(buttons, 0, 1);

        Controls.Add(layout);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Shown += (_, _) => _nameInput.FocusInput();
    }

    private void Submit()
    {
        if (ProfileName.Length == 0)
        {
            _nameInput.SetError("Hãy nhập tên profile.");
            return;
        }

        _nameInput.ClearError();
        DialogResult = DialogResult.OK;
        Close();
    }
}
