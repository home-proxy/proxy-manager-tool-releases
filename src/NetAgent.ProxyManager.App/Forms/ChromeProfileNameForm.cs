using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ChromeProfileNameForm : Form
{
    private const int MaxNameLength = 80;
    private const int FormWidth = 480;
    private const int CompactClientHeight = 160;
    private const int ErrorClientHeight = 188;

    private readonly AppTextInput _nameInput = new("Tên Chrome profile", "Ví dụ: Shop Facebook 1");
    private readonly AppPrimaryButton _saveButton = new();
    private readonly AppButton _cancelButton = new();

    public ChromeProfileNameForm()
    {
        Text = "Tạo Chrome profile";
        ClientSize = new Size(FormWidth, CompactClientHeight);
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
            Padding = new Padding(24, 20, 24, 16),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _nameInput.Dock = DockStyle.Top;
        _nameInput.LabelInputGap = 8;
        _nameInput.Margin = new Padding(0, 0, 0, 16);
        _nameInput.MaxLength = MaxNameLength;
        _nameInput.TextChanged += (_, _) => SetClientHeight(hasError: false);
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
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_cancelButton);
        layout.Controls.Add(buttons, 0, 1);

        Controls.Add(layout);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Shown += (_, _) => _nameInput.FocusInput();
    }

    private void SetClientHeight(bool hasError)
    {
        var height = hasError ? ErrorClientHeight : CompactClientHeight;
        if (ClientSize.Height != height)
        {
            ClientSize = new Size(FormWidth, height);
        }
    }

    private void Submit()
    {
        if (ProfileName.Length == 0)
        {
            _nameInput.SetError("Hãy nhập tên profile.");
            SetClientHeight(hasError: true);
            return;
        }

        _nameInput.ClearError();
        SetClientHeight(hasError: false);
        DialogResult = DialogResult.OK;
        Close();
    }
}
