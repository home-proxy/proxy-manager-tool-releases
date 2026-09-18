using NetAgent.ProxyManager.App.Theme;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppTextInput : UserControl
{
    // Manual sizing: SearchIconSize/SearchIconTextGap/HorizontalPadding control the search icon layout.
    private const int LabelHeight = 24;
    private const int InputHeight = 38;
    private const int ErrorHeight = 24;
    private const int TextBoxVerticalPadding = 8;
    private const int HorizontalPadding = 8;
    private const int SearchIconSize = 16;
    private const int SearchIconTextGap = 4;
    private const int LeadingIconSize = 20;
    private const int LeadingIconTextGap = 8;
    private const int PasswordButtonWidth = 36;
    private const int BorderRadius = 4;

    private static readonly Color InputBackColor = Color.White;
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color PlaceholderIconColor = Color.FromArgb(115, 115, 115);
    private static readonly Color PlaceholderTextColor = Color.FromArgb(115, 115, 115);
    private static readonly Color BorderColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DisabledBackColor = Color.FromArgb(250, 250, 250);
    private static readonly Color DisabledTextColor = Color.FromArgb(161, 161, 161);

    private readonly Label _label = new();
    private readonly TextBox _textBox = new();
    private readonly Label _placeholderLabel = new();
    private readonly Button _togglePasswordButton = new();
    private readonly Label _errorLabel = new();
    private string _placeholderText = string.Empty;
    private Image? _leadingIcon;
    private int _labelHeight = LabelHeight;
    private int _labelInputGap;
    private int _errorMessageHeight = ErrorHeight;
    private bool _showPasswordToggle;
    private bool? _showSearchIconOverride;

    public AppTextInput()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        BackColor = LightTheme.Surface;
        Height = InputHeight;
        MinimumSize = new Size(80, InputHeight);

        _label.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _label.ForeColor = TextColor;
        _label.BackColor = LightTheme.Surface;
        _label.TextAlign = ContentAlignment.MiddleLeft;
        _label.Visible = false;

        _textBox.AutoSize = false;
        _textBox.BorderStyle = BorderStyle.None;
        _textBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _textBox.ForeColor = TextColor;
        _textBox.BackColor = InputBackColor;
        _textBox.Margin = Padding.Empty;
        _textBox.PlaceholderText = string.Empty;
        _textBox.Enter += (_, _) =>
        {
            UpdatePlaceholderVisibility();
            Invalidate();
        };
        _textBox.Leave += (_, _) =>
        {
            UpdatePlaceholderVisibility();
            Invalidate();
        };
        _textBox.TextChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(ErrorText))
            {
                ErrorText = string.Empty;
            }

            UpdatePlaceholderVisibility();
            TextChanged?.Invoke(this, e);
        };
        _textBox.FontChanged += (_, _) => LayoutChildren();

        _placeholderLabel.Font = _textBox.Font;
        _placeholderLabel.ForeColor = PlaceholderTextColor;
        _placeholderLabel.BackColor = InputBackColor;
        _placeholderLabel.TextAlign = ContentAlignment.MiddleLeft;
        _placeholderLabel.AutoEllipsis = true;
        _placeholderLabel.UseMnemonic = false;
        _placeholderLabel.Visible = false;
        _placeholderLabel.Click += (_, _) => _textBox.Focus();

        _togglePasswordButton.FlatStyle = FlatStyle.Flat;
        _togglePasswordButton.FlatAppearance.BorderSize = 0;
        _togglePasswordButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
        _togglePasswordButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 238, 238);
        _togglePasswordButton.BackColor = InputBackColor;
        _togglePasswordButton.Image = UiIcons.PasswordView;
        _togglePasswordButton.Cursor = Cursors.Hand;
        _togglePasswordButton.TabStop = false;
        _togglePasswordButton.Visible = false;
        _togglePasswordButton.AccessibleDescription = "Hiện mật khẩu";
        _togglePasswordButton.Click += (_, _) => TogglePassword();

        _errorLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _errorLabel.ForeColor = LightTheme.Danger;
        _errorLabel.BackColor = LightTheme.Surface;
        _errorLabel.TextAlign = ContentAlignment.TopLeft;
        _errorLabel.Padding = new Padding(0, 4, 0, 0);
        _errorLabel.Visible = false;

        Controls.Add(_label);
        Controls.Add(_textBox);
        Controls.Add(_placeholderLabel);
        Controls.Add(_togglePasswordButton);
        Controls.Add(_errorLabel);
        LayoutChildren();
    }

    public AppTextInput(string label, string placeholder, bool isPassword = false)
        : this()
    {
        LabelText = label;
        PlaceholderText = placeholder;
        UseSystemPasswordChar = isPassword;
        ShowPasswordToggle = isPassword;
    }

    public new event EventHandler? TextChanged;

    public TextBox InnerTextBox => _textBox;

    [AllowNull]
    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value ?? string.Empty;
    }

    public string TextValue => _textBox.Text;

    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value ?? string.Empty;
            _placeholderLabel.Text = _placeholderText;
            _textBox.PlaceholderText = string.Empty;
            LayoutChildren();
        }
    }

    public string LabelText
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            _label.Visible = !string.IsNullOrWhiteSpace(value);
            UpdateHeight();
        }
    }

    public int LabelInputGap
    {
        get => _labelInputGap;
        set
        {
            _labelInputGap = Math.Max(0, value);
            UpdateHeight();
        }
    }

    public int LabelContentHeight
    {
        get => _labelHeight;
        set
        {
            _labelHeight = Math.Max(LabelHeight, value);
            UpdateHeight();
        }
    }

    public int ErrorMessageHeight
    {
        get => _errorMessageHeight;
        set
        {
            _errorMessageHeight = Math.Max(ErrorHeight, value);
            UpdateHeight();
        }
    }

    public Image? LeadingIcon
    {
        get => _leadingIcon;
        set
        {
            _leadingIcon = value;
            LayoutChildren();
        }
    }

    public string ErrorText
    {
        get => _errorLabel.Text;
        set
        {
            _errorLabel.Text = value;
            _errorLabel.Visible = !string.IsNullOrWhiteSpace(value);
            UpdateHeight();
            Invalidate();
        }
    }

    public bool ReadOnly
    {
        get => _textBox.ReadOnly;
        set => _textBox.ReadOnly = value;
    }

    public bool UseSystemPasswordChar
    {
        get => _textBox.UseSystemPasswordChar;
        set
        {
            _textBox.UseSystemPasswordChar = value;
            _togglePasswordButton.Image = value ? UiIcons.PasswordView : UiIcons.PasswordHide;
        }
    }

    public int MaxLength
    {
        get => _textBox.MaxLength;
        set => _textBox.MaxLength = value;
    }

    public int SelectionStart
    {
        get => _textBox.SelectionStart;
        set => _textBox.SelectionStart = value;
    }

    public bool ShowPasswordToggle
    {
        get => _showPasswordToggle;
        set
        {
            if (_showPasswordToggle == value)
            {
                return;
            }

            _showPasswordToggle = value;
            _togglePasswordButton.Visible = value;
            LayoutChildren();
        }
    }

    public bool ShowSearchIcon
    {
        get => ShouldShowSearchIcon();
        set
        {
            _showSearchIconOverride = value;
            LayoutChildren();
        }
    }

    public void FocusInput() => _textBox.Focus();

    public void Clear() => _textBox.Clear();

    public void SetError(string message) => ErrorText = message;

    public void ClearError() => ErrorText = string.Empty;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChildren();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (GetInputBounds().Contains(e.Location))
        {
            _textBox.Focus();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _textBox.ForeColor = Enabled ? TextColor : DisabledTextColor;
        _textBox.BackColor = Enabled ? InputBackColor : DisabledBackColor;
        _placeholderLabel.ForeColor = Enabled ? PlaceholderTextColor : DisabledTextColor;
        _placeholderLabel.BackColor = Enabled ? InputBackColor : DisabledBackColor;
        UpdatePlaceholderVisibility();
        Invalidate();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _label.BackColor = BackColor;
        _errorLabel.BackColor = BackColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        var hasError = !string.IsNullOrWhiteSpace(ErrorText);
        var borderColor = hasError
            ? LightTheme.Danger
            : _textBox.Focused
                ? LightTheme.Accent
                : BorderColor;
        var inputBounds = GetInputBounds();

        using (var path = AppButton.CreateRoundedRectangle(inputBounds, BorderRadius))
        using (var background = new SolidBrush(Enabled ? InputBackColor : DisabledBackColor))
        using (var pen = new Pen(borderColor, _textBox.Focused || hasError ? 1.5f : 1f))
        {
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(pen, path);
        }

        if (ShouldShowSearchIcon())
        {
            DrawSearchIcon(e.Graphics, inputBounds);
        }

        if (_leadingIcon is not null)
        {
            DrawLeadingIcon(e.Graphics, inputBounds);
        }
    }

    private void TogglePassword()
    {
        UseSystemPasswordChar = !UseSystemPasswordChar;
        _togglePasswordButton.AccessibleDescription = UseSystemPasswordChar ? "Hiện mật khẩu" : "Ẩn mật khẩu";
        _textBox.Focus();
    }

    private void UpdateHeight()
    {
        var height = (_label.Visible ? _labelHeight : 0) +
            (_label.Visible ? _labelInputGap : 0) +
            InputHeight +
            (_errorLabel.Visible ? _errorMessageHeight : 0);
        Height = height;
        MinimumSize = new Size(MinimumSize.Width, height);
        LayoutChildren();
    }

    private void LayoutChildren()
    {
        var inputTop = _label.Visible ? _labelHeight + _labelInputGap : 0;
        _label.SetBounds(0, 0, Width, _label.Visible ? _labelHeight : 0);

        var textLeft = HorizontalPadding;
        if (ShouldShowSearchIcon())
        {
            textLeft += SearchIconSize + SearchIconTextGap;
        }
        else if (_leadingIcon is not null)
        {
            textLeft += LeadingIconSize + LeadingIconTextGap;
        }

        var rightPadding = HorizontalPadding;
        if (_showPasswordToggle)
        {
            rightPadding += PasswordButtonWidth + 2;
        }

        var textBoxHeight = GetTextBoxHeight();
        var textTop = inputTop + Math.Max(0, (InputHeight - textBoxHeight) / 2);
        _textBox.SetBounds(
            textLeft,
            textTop,
            Math.Max(20, Width - textLeft - rightPadding),
            textBoxHeight);
        _placeholderLabel.SetBounds(
            textLeft,
            inputTop + 1,
            Math.Max(20, Width - textLeft - rightPadding),
            InputHeight - 2);

        _togglePasswordButton.SetBounds(
            Math.Max(HorizontalPadding, Width - HorizontalPadding - PasswordButtonWidth),
            inputTop + 2,
            PasswordButtonWidth,
            InputHeight - 4);

        _errorLabel.SetBounds(
            0,
            inputTop + InputHeight,
            Math.Max(1, Width),
            _errorLabel.Visible ? _errorMessageHeight : 0);

        UpdatePlaceholderVisibility();
        _togglePasswordButton.BringToFront();
        Invalidate();
    }

    private int GetTextBoxHeight()
    {
        var preferredHeight = _textBox.PreferredHeight;
        var fontHeight = _textBox.Font.Height + TextBoxVerticalPadding;
        var maximumHeight = Math.Max(1, InputHeight - 6);
        return Math.Min(maximumHeight, Math.Max(preferredHeight, fontHeight));
    }

    private Rectangle GetInputBounds()
    {
        var inputTop = _label.Visible ? _labelHeight + _labelInputGap : 0;
        return new Rectangle(0, inputTop, Math.Max(1, Width - 1), InputHeight - 1);
    }

    private bool ShouldShowSearchIcon() =>
        _showSearchIconOverride ?? LooksLikeSearchInput(_placeholderText);

    private void UpdatePlaceholderVisibility()
    {
        _placeholderLabel.Visible = Enabled &&
            !_textBox.Focused &&
            string.IsNullOrEmpty(_textBox.Text) &&
            !string.IsNullOrWhiteSpace(_placeholderText);

        if (_placeholderLabel.Visible)
        {
            _placeholderLabel.BringToFront();
        }
    }

    private static bool LooksLikeSearchInput(string placeholder)
    {
        if (string.IsNullOrWhiteSpace(placeholder))
        {
            return false;
        }

        return placeholder.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            placeholder.Contains("tìm", StringComparison.OrdinalIgnoreCase) ||
            placeholder.Contains("tim", StringComparison.OrdinalIgnoreCase) ||
            placeholder.Contains("lọc", StringComparison.OrdinalIgnoreCase) ||
            placeholder.Contains("loc", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawSearchIcon(Graphics graphics, Rectangle inputBounds)
    {
        var iconLeft = inputBounds.Left + HorizontalPadding;
        var iconTop = inputBounds.Top + (InputHeight - SearchIconSize) / 2;
        using var pen = new Pen(PlaceholderIconColor, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawEllipse(pen, iconLeft + 2, iconTop + 2, 9, 9);
        graphics.DrawLine(pen, iconLeft + 10, iconTop + 10, iconLeft + 14, iconTop + 14);
    }

    private void DrawLeadingIcon(Graphics graphics, Rectangle inputBounds)
    {
        if (_leadingIcon is null)
        {
            return;
        }

        var iconLeft = inputBounds.Left + HorizontalPadding;
        var iconTop = inputBounds.Top + (InputHeight - LeadingIconSize) / 2;
        graphics.DrawImage(_leadingIcon, iconLeft, iconTop, LeadingIconSize, LeadingIconSize);
    }
}
