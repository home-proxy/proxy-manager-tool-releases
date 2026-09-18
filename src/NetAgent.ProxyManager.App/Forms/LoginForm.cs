using System.Net;
using System.Text.RegularExpressions;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class LoginForm : Form
{
    private const int FormWidth = 560;
    private const int LoginFormHeight = 620;
    private const int RegisterFormHeight = 820;
    private const int ContentWidth = 444;
    private const int FooterHeight = 76;
    private const int TitleHeight = 48;

    private const string GenericLoginError = "Số điện thoại hoặc mật khẩu không đúng.";
    private const string GenericRegisterError = "Không thể đăng ký với thông tin này.";
    private const string PhoneRequiredMessage = "Vui lòng nhập số điện thoại.";
    private const string PhoneInvalidMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
    private const string PasswordRequiredMessage = "Vui lòng nhập mật khẩu.";
    private const string RegisterPasswordInvalidMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
    private const string EmailRequiredMessage = "Vui lòng nhập email.";
    private const string EmailInvalidMessage = "Email không hợp lệ.";
    private const string FullNameRequiredMessage = "Vui lòng nhập họ tên.";
    private const string FullNameInvalidMessage = "Vui lòng nhập đầy đủ họ và tên.";

    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAuthService _authService;
    private readonly Panel _bodyPanel = new();
    private readonly Panel _content = new();
    private readonly Panel _fieldsHost = new();
    private readonly Label _titleLabel = new();
    private readonly AppPrimaryButton _submitButton = new();
    private readonly FlowLayoutPanel _switchPanel = new();
    private readonly LinkLabel _switchModeLink = new();
    private readonly Label _switchPromptLabel = new();
    private int _nextFieldTop;

    private readonly AuthTextInput _loginPhoneInput = new(
        "Số điện thoại",
        "Nhập số điện thoại của bạn",
        "new-design/phone-number.svg");
    private readonly AuthTextInput _loginPasswordInput = new(
        "Mật khẩu",
        "Nhập mật khẩu của bạn",
        "new-design/lock.svg",
        isPassword: true);
    private readonly AuthTextInput _registerFullNameInput = new(
        "Họ tên",
        "Nhập họ tên của bạn",
        "new-design/full-name.svg");
    private readonly AuthTextInput _registerEmailInput = new(
        "Email",
        "Nhập email của bạn",
        "new-design/email.svg");
    private readonly AuthTextInput _registerPhoneInput = new(
        "Số điện thoại",
        "Nhập số điện thoại của bạn",
        "new-design/phone-number.svg");
    private readonly AuthTextInput _registerPasswordInput = new(
        "Mật khẩu",
        "Nhập mật khẩu của bạn",
        "new-design/lock.svg",
        isPassword: true);

    private bool _isRegisterMode;

    public LoginForm(IAuthService authService)
    {
        _authService = authService;

        Text = AppBranding.AppDisplayName;
        SetAuthFormSize(LoginFormHeight);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = LightTheme.Surface;
        Icon = AppBranding.LoadApplicationIcon() ?? Icon;

        BuildUi();
        RenderMode();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = LightTheme.Surface,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        _bodyPanel.Dock = DockStyle.Fill;
        _bodyPanel.AutoScroll = false;
        _bodyPanel.BackColor = LightTheme.Surface;
        _bodyPanel.Padding = new Padding(24, 0, 24, 0);

        _content.Width = ContentWidth;
        _content.BackColor = LightTheme.Surface;

        var logoBox = BuildLogoBox();
        _content.Controls.Add(logoBox);

        _titleLabel.AutoSize = false;
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _titleLabel.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
        _titleLabel.ForeColor = Color.FromArgb(36, 36, 36);
        _content.Controls.Add(_titleLabel);

        _switchPanel.AutoSize = false;
        _switchPanel.FlowDirection = FlowDirection.LeftToRight;
        _switchPanel.WrapContents = false;
        _switchPanel.Resize += (_, _) => CenterSwitchPanel(_switchPanel);

        _switchPromptLabel.AutoSize = true;
        _switchPromptLabel.Font = new Font("Segoe UI", 12.5f, FontStyle.Regular);
        _switchPromptLabel.ForeColor = Color.FromArgb(115, 115, 115);
        _switchPromptLabel.Margin = new Padding(0, 1, 12, 0);

        _switchModeLink.AutoSize = true;
        _switchModeLink.Font = new Font("Segoe UI", 12.5f, FontStyle.Regular);
        _switchModeLink.LinkColor = Color.FromArgb(15, 108, 189);
        _switchModeLink.ActiveLinkColor = Color.FromArgb(10, 80, 145);
        _switchModeLink.VisitedLinkColor = Color.FromArgb(15, 108, 189);
        _switchModeLink.Margin = new Padding(0, 1, 0, 0);
        _switchModeLink.LinkBehavior = LinkBehavior.AlwaysUnderline;
        _switchModeLink.Click += (_, _) =>
        {
            _isRegisterMode = !_isRegisterMode;
            RenderMode();
        };

        _switchPanel.Controls.Add(_switchPromptLabel);
        _switchPanel.Controls.Add(_switchModeLink);
        _content.Controls.Add(_switchPanel);

        _fieldsHost.BackColor = LightTheme.Surface;
        _content.Controls.Add(_fieldsHost);

        _bodyPanel.Controls.Add(_content);
        CenterContent();
        _bodyPanel.Resize += (_, _) => LayoutContent();

        var footer = BuildFooter();
        root.Controls.Add(_bodyPanel, 0, 0);
        root.Controls.Add(footer, 0, 1);

        Controls.Add(root);
        AcceptButton = _submitButton;
    }

    private Panel BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(247, 247, 249),
            Padding = new Padding(34, 16, 34, 16)
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(224, 224, 224));
            e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
        };

        _submitButton.Text = "Đăng nhập";
        _submitButton.Dock = DockStyle.Fill;
        _submitButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _submitButton.Margin = Padding.Empty;
        _submitButton.MinimumSize = new Size(0, 40);
        _submitButton.Click += async (_, _) => await SubmitAsync();

        footer.Controls.Add(_submitButton);
        return footer;
    }

    private static PictureBox BuildLogoBox()
    {
        var pictureBox = new PictureBox
        {
            Size = new Size(110, 44),
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Top,
            Margin = new Padding(0, 0, 0, 0)
        };

        pictureBox.Image = AppBranding.LoadLogoImage();
        return pictureBox;
    }

    private void RenderMode()
    {
        _fieldsHost.Controls.Clear();
        SetStatusInfo(string.Empty);

        if (_isRegisterMode)
        {
            SetAuthFormSize(RegisterFormHeight);
            Text = "Đăng ký ProxyManager";
            _titleLabel.Text = "Tạo tài khoản mới";
            _switchPromptLabel.Text = "Bạn đã có tài khoản?";
            _switchModeLink.Text = "Đăng nhập";
            _submitButton.Text = "Đăng ký";
            BuildRegisterFields();
        }
        else
        {
            SetAuthFormSize(LoginFormHeight);
            Text = "Đăng nhập ProxyManager";
            _titleLabel.Text = "Đăng nhập";
            _switchPromptLabel.Text = "Bạn chưa có tài khoản?";
            _switchModeLink.Text = "Đăng ký";
            _submitButton.Text = "Đăng nhập";
            BuildLoginFields();
        }

        CenterSwitchPanel(_switchPanel);
        LayoutContent();
    }

    private void SetAuthFormSize(int height)
    {
        MinimumSize = new Size(FormWidth, height);
        Size = new Size(FormWidth, height);
    }

    private void BuildLoginFields()
    {
        _nextFieldTop = 0;
        AddFullWidthField(_loginPhoneInput);
        AddFullWidthField(_loginPasswordInput);

        var forgotPassword = new Label
        {
            Text = "Quên mật khẩu?",
            AutoSize = true,
            ForeColor = Color.FromArgb(207, 19, 34),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Location = new Point(0, _nextFieldTop + 2),
            Margin = Padding.Empty
        };
        _fieldsHost.Controls.Add(forgotPassword);
        _fieldsHost.Height = forgotPassword.Bottom;
    }

    private void BuildRegisterFields()
    {
        _nextFieldTop = 0;
        AddFullWidthField(_registerFullNameInput);
        AddFullWidthField(_registerEmailInput);
        AddFullWidthField(_registerPhoneInput);
        AddFullWidthField(_registerPasswordInput);
    }

    private void AddFullWidthField(Control field)
    {
        field.Width = ContentWidth;
        field.Location = new Point(0, _nextFieldTop);
        field.Margin = Padding.Empty;
        _fieldsHost.Controls.Add(field);
        _nextFieldTop += field.Height + 4;
        _fieldsHost.Height = _nextFieldTop;
    }

    private static void CenterSwitchPanel(FlowLayoutPanel panel)
    {
        var contentWidth = panel.Controls.Cast<Control>().Sum(control => control.Width + control.Margin.Left + control.Margin.Right);
        panel.Padding = new Padding(Math.Max(0, (panel.Width - contentWidth) / 2), 0, 0, 0);
    }

    private void CenterContent()
    {
        LayoutContent();
    }

    private void LayoutContent()
    {
        if (_bodyPanel.ClientSize.Width <= 0)
        {
            return;
        }

        var width = Math.Min(ContentWidth, Math.Max(280, _bodyPanel.ClientSize.Width - _bodyPanel.Padding.Horizontal));
        var logoTop = 26;
        var titleTop = 94;
        var switchTop = _isRegisterMode ? 148 : 148;
        var fieldsTop = _isRegisterMode ? 200 : 200;
        var contentHeight = Math.Max(fieldsTop + _fieldsHost.Height, _bodyPanel.ClientSize.Height - 1);

        _content.Width = width;
        _content.Height = contentHeight;
        _content.Left = Math.Max(_bodyPanel.Padding.Left, (_bodyPanel.ClientSize.Width - width) / 2);
        _content.Top = 0;

        if (_content.Controls.Count > 0)
        {
            var logo = _content.Controls[0];
            logo.Location = new Point((width - logo.Width) / 2, logoTop);
        }

        _titleLabel.SetBounds(0, titleTop, width, TitleHeight);
        _switchPanel.SetBounds(0, switchTop, width, 32);
        _fieldsHost.SetBounds(0, fieldsTop, width, Math.Max(1, _fieldsHost.Height));
        CenterSwitchPanel(_switchPanel);
    }

    private async Task SubmitAsync()
    {
        if (_isRegisterMode)
        {
            await RegisterAsync();
            return;
        }

        await LoginAsync();
    }

    private async Task LoginAsync()
    {
        ClearAuthErrors();
        if (!ValidateLoginInputs())
        {
            return;
        }

        await RunAuthActionAsync(
            "Đang đăng nhập...",
            async ct => await _authService.LoginAsync(
                _loginPhoneInput.TextValue,
                _loginPasswordInput.TextValue,
                rememberSession: true,
                ct),
            HandleLoginAuthError);
    }

    private async Task RegisterAsync()
    {
        ClearAuthErrors();
        if (!ValidateRegisterInputs())
        {
            return;
        }

        var (firstName, lastName) = SplitFullName(_registerFullNameInput.TextValue);
        var request = new AuthRegisterRequest
        {
            Email = _registerEmailInput.TextValue.Trim(),
            Phone = _registerPhoneInput.TextValue.Trim(),
            Password = _registerPasswordInput.TextValue,
            FirstName = firstName,
            LastName = lastName
        };

        await RunAuthActionAsync(
            "Đang đăng ký...",
            async ct => await _authService.RegisterAsync(request, rememberSession: true, ct),
            HandleRegisterAuthError);
    }

    private async Task RunAuthActionAsync(
        string busyText,
        Func<CancellationToken, Task<AuthSession>> action,
        Action<AuthApiException> handleAuthError)
    {
        try
        {
            ClearAuthErrors();
            _submitButton.Enabled = false;
            SetStatusInfo(busyText);
            var session = await action(CancellationToken.None);
            SetStatusInfo($"Đã đăng nhập: {session.User?.DisplayName ?? session.User?.Phone ?? AppBranding.AppDisplayName}");
            DialogResult = DialogResult.OK;
        }
        catch (AuthApiException ex)
        {
            handleAuthError(ex);
        }
        catch (ArgumentException ex)
        {
            HandleArgumentException(ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            SetStatusError(UiFeedback.GetFriendlyErrorMessage(ex));
        }
        catch (Exception ex)
        {
            var message = UiFeedback.GetFriendlyErrorMessage(ex);
            if (string.Equals(message, ex.Message, StringComparison.Ordinal))
            {
                message = _isRegisterMode
                    ? "Không thể đăng ký tài khoản. Vui lòng thử lại sau."
                    : "Không thể xác thực tài khoản. Vui lòng thử lại sau.";
            }

            SetStatusError(message);
        }
        finally
        {
            _submitButton.Enabled = true;
        }
    }

    private void HandleLoginAuthError(AuthApiException exception)
    {
        if (exception.HasError("phone", "invalidPhone"))
        {
            SetFieldError(_loginPhoneInput, PhoneInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng nhập.");
            return;
        }

        if (exception.HasError("phone", "notFound"))
        {
            SetFieldError(_loginPhoneInput, "Số điện thoại chưa được đăng ký.");
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng nhập.");
            return;
        }

        if (exception.HasError("password", "incorrectPassword"))
        {
            SetFieldError(_loginPasswordInput, "Mật khẩu không đúng.");
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng nhập.");
            return;
        }

        SetStatusError(GenericLoginError);
    }

    private void HandleRegisterAuthError(AuthApiException exception)
    {
        if (exception.HasError("phone", "phoneExists"))
        {
            SetFieldError(_registerPhoneInput, "Số điện thoại đã được sử dụng.");
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            return;
        }

        if (exception.HasError("phone", "invalidPhone"))
        {
            SetFieldError(_registerPhoneInput, PhoneInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            return;
        }

        if (exception.HasError("email", "emailExists"))
        {
            SetFieldError(_registerEmailInput, "Email đã được sử dụng.");
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            return;
        }

        if (exception.HasError("email", "invalidEmail"))
        {
            SetFieldError(_registerEmailInput, EmailInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            return;
        }

        if (exception.HasError("password", "weakPassword") ||
            exception.HasError("password", "invalidPassword"))
        {
            SetFieldError(_registerPasswordInput, RegisterPasswordInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            return;
        }

        SetStatusError(GenericRegisterError);
    }

    private bool ValidateLoginInputs()
    {
        AuthTextInput? firstInvalid = null;
        var isValid = true;

        isValid &= ValidatePhone(_loginPhoneInput, ref firstInvalid);
        isValid &= ValidateRequired(_loginPasswordInput, PasswordRequiredMessage, ref firstInvalid);

        if (!isValid)
        {
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng nhập.");
            firstInvalid?.FocusInput();
        }
        else
        {
            SetStatusInfo(string.Empty);
        }

        return isValid;
    }

    private bool ValidateRegisterInputs()
    {
        AuthTextInput? firstInvalid = null;
        var isValid = true;

        isValid &= ValidateFullName(_registerFullNameInput, ref firstInvalid);
        isValid &= ValidateEmail(_registerEmailInput, ref firstInvalid);
        isValid &= ValidatePhone(_registerPhoneInput, ref firstInvalid);
        isValid &= ValidateRegisterPassword(_registerPasswordInput, ref firstInvalid);

        if (!isValid)
        {
            SetStatusError("Vui lòng kiểm tra lại thông tin đăng ký.");
            firstInvalid?.FocusInput();
        }
        else
        {
            SetStatusInfo(string.Empty);
        }

        return isValid;
    }

    private static bool ValidateRequired(AuthTextInput input, string message, ref AuthTextInput? firstInvalid)
    {
        if (!string.IsNullOrWhiteSpace(input.TextValue))
        {
            return true;
        }

        MarkInvalid(input, message, ref firstInvalid);
        return false;
    }

    private static bool ValidateFullName(AuthTextInput input, ref AuthTextInput? firstInvalid)
    {
        var normalizedName = NormalizeFullName(input.TextValue);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            MarkInvalid(input, FullNameRequiredMessage, ref firstInvalid);
            return false;
        }

        if (TrySplitFullName(normalizedName, out _, out _))
        {
            return true;
        }

        MarkInvalid(input, FullNameInvalidMessage, ref firstInvalid);
        return false;
    }

    private static bool ValidatePhone(AuthTextInput input, ref AuthTextInput? firstInvalid)
    {
        var value = input.TextValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MarkInvalid(input, PhoneRequiredMessage, ref firstInvalid);
            return false;
        }

        if (PhoneRegex.IsMatch(value))
        {
            return true;
        }

        MarkInvalid(input, PhoneInvalidMessage, ref firstInvalid);
        return false;
    }

    private static bool ValidateEmail(AuthTextInput input, ref AuthTextInput? firstInvalid)
    {
        var value = input.TextValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MarkInvalid(input, EmailRequiredMessage, ref firstInvalid);
            return false;
        }

        if (EmailRegex.IsMatch(value))
        {
            return true;
        }

        MarkInvalid(input, EmailInvalidMessage, ref firstInvalid);
        return false;
    }

    private static bool ValidateRegisterPassword(AuthTextInput input, ref AuthTextInput? firstInvalid)
    {
        if (string.IsNullOrWhiteSpace(input.TextValue))
        {
            MarkInvalid(input, PasswordRequiredMessage, ref firstInvalid);
            return false;
        }

        if (input.TextValue.Length >= 8)
        {
            return true;
        }

        MarkInvalid(input, RegisterPasswordInvalidMessage, ref firstInvalid);
        return false;
    }

    private void HandleArgumentException(ArgumentException exception)
    {
        var message = exception.Message;
        var handled = false;

        if (string.Equals(exception.ParamName, "phone", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_isRegisterMode ? _registerPhoneInput : _loginPhoneInput, PhoneRequiredMessage);
            handled = true;
        }

        if (string.Equals(exception.ParamName, "password", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_isRegisterMode ? _registerPasswordInput : _loginPasswordInput, PasswordRequiredMessage);
            handled = true;
        }

        if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_registerEmailInput, EmailRequiredMessage);
            handled = true;
        }

        if (message.Contains("First name", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Last name", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_registerFullNameInput, FullNameInvalidMessage);
            handled = true;
        }

        SetStatusError(handled
            ? (_isRegisterMode ? "Vui lòng kiểm tra lại thông tin đăng ký." : "Vui lòng kiểm tra lại thông tin đăng nhập.")
            : UiFeedback.GetFriendlyErrorMessage(exception));
    }

    private void SetFieldError(AuthTextInput input, string message)
    {
        input.SetError(message);
        input.FocusInput();
    }

    private static void MarkInvalid(AuthTextInput input, string message, ref AuthTextInput? firstInvalid)
    {
        input.SetError(message);
        firstInvalid ??= input;
    }

    private static void SetStatusInfo(string message) => _ = message;

    private static void SetStatusError(string message) => _ = message;

    private void ClearAuthErrors()
    {
        _loginPhoneInput.ClearError();
        _loginPasswordInput.ClearError();
        _registerFullNameInput.ClearError();
        _registerEmailInput.ClearError();
        _registerPhoneInput.ClearError();
        _registerPasswordInput.ClearError();
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var normalizedName = NormalizeFullName(fullName);
        return TrySplitFullName(normalizedName, out var firstName, out var lastName)
            ? (firstName, lastName)
            : (normalizedName, string.Empty);
    }

    private static bool TrySplitFullName(string fullName, out string firstName, out string lastName)
    {
        firstName = string.Empty;
        lastName = string.Empty;

        var normalizedName = NormalizeFullName(fullName);
        var lastSpaceIndex = normalizedName.LastIndexOf(' ');
        if (lastSpaceIndex <= 0 || lastSpaceIndex >= normalizedName.Length - 1)
        {
            return false;
        }

        firstName = normalizedName[..lastSpaceIndex];
        lastName = normalizedName[(lastSpaceIndex + 1)..];
        return true;
    }

    private static string NormalizeFullName(string value) =>
        WhitespaceRegex.Replace(value.Trim(), " ");

    private sealed class AuthTextInput : UserControl
    {
        private const int LabelHeight = 24;
        private const int LabelInputGap = 6;
        private const int InputTop = LabelHeight + LabelInputGap;
        private const int InputHeight = 40;
        private const int ErrorTop = InputTop + InputHeight + 6;
        private const int ErrorHeight = 28;
        private const int StableHeight = ErrorTop + ErrorHeight;
        private const int IconSize = 20;
        private const int TextLeft = 44;
        private const int TextBoxVerticalPadding = 8;
        private const int PasswordButtonWidth = 40;

        private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
        private static readonly Color BorderColor = Color.FromArgb(209, 209, 209);
        private static readonly Color FocusBorderColor = Color.FromArgb(15, 108, 189);
        private static readonly Color ErrorColor = Color.FromArgb(177, 14, 28);
        private static readonly Color PlaceholderTextColor = Color.FromArgb(115, 115, 115);
        private static readonly Color SurfaceColor = Color.White;

        private readonly Label _label = new();
        private readonly TextBox _textBox = new();
        private readonly Label _placeholderLabel = new();
        private readonly Button _passwordButton = new();
        private readonly Label _errorLabel = new();
        private readonly Image? _leadingIcon;
        private readonly Image? _errorIcon;
        private readonly Image? _showPasswordIcon;
        private readonly Image? _hidePasswordIcon;
        private readonly bool _isPassword;

        public AuthTextInput(
            string label,
            string placeholder,
            string iconFileName,
            bool isPassword = false)
        {
            _isPassword = isPassword;
            _leadingIcon = SidebarIconRenderer.LoadOriginal(iconFileName, IconSize);
            _errorIcon = SidebarIconRenderer.LoadOriginal("new-design/error-message-icon.svg", 14);
            _showPasswordIcon = SidebarIconRenderer.LoadOriginal("new-design/show-password.svg", 20);
            _hidePasswordIcon = SidebarIconRenderer.LoadOriginal("new-design/hide-password.svg", 20);

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            BackColor = SurfaceColor;
            Height = StableHeight;
            MinimumSize = new Size(180, Height);

            _label.Text = label;
            _label.TextAlign = ContentAlignment.MiddleLeft;
            _label.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            _label.ForeColor = TextColor;
            _label.BackColor = SurfaceColor;

            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = new Font("Segoe UI", 12f, FontStyle.Regular);
            _textBox.ForeColor = TextColor;
            _textBox.BackColor = SurfaceColor;
            _textBox.PlaceholderText = string.Empty;
            _textBox.UseSystemPasswordChar = isPassword;
            _textBox.Margin = Padding.Empty;
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
                    ClearError();
                }

                UpdatePlaceholderVisibility();
                TextChanged?.Invoke(this, e);
            };
            _textBox.FontChanged += (_, _) => LayoutChildren();

            _placeholderLabel.Text = placeholder;
            _placeholderLabel.Font = _textBox.Font;
            _placeholderLabel.ForeColor = PlaceholderTextColor;
            _placeholderLabel.BackColor = SurfaceColor;
            _placeholderLabel.TextAlign = ContentAlignment.MiddleLeft;
            _placeholderLabel.AutoEllipsis = true;
            _placeholderLabel.UseMnemonic = false;
            _placeholderLabel.Visible = false;
            _placeholderLabel.Click += (_, _) => _textBox.Focus();

            _passwordButton.FlatStyle = FlatStyle.Flat;
            _passwordButton.FlatAppearance.BorderSize = 0;
            _passwordButton.BackColor = SurfaceColor;
            _passwordButton.Image = _showPasswordIcon;
            _passwordButton.Cursor = Cursors.Hand;
            _passwordButton.TabStop = false;
            _passwordButton.AccessibleDescription = "Hiện mật khẩu";
            _passwordButton.Click += (_, _) => TogglePassword();

            _errorLabel.AutoSize = false;
            _errorLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _errorLabel.ForeColor = ErrorColor;
            _errorLabel.BackColor = SurfaceColor;
            _errorLabel.TextAlign = ContentAlignment.MiddleLeft;
            _errorLabel.AutoEllipsis = true;
            _errorLabel.Visible = false;

            Controls.Add(_label);
            Controls.Add(_textBox);
            Controls.Add(_placeholderLabel);
            if (isPassword)
            {
                Controls.Add(_passwordButton);
            }
            Controls.Add(_errorLabel);

            LayoutChildren();
        }

        public new event EventHandler? TextChanged;

        public string TextValue => _textBox.Text;

        private string ErrorText
        {
            get => _errorLabel.Text;
            set
            {
                _errorLabel.Text = value;
                var hasError = !string.IsNullOrWhiteSpace(value);
                _errorLabel.Visible = hasError;
                Invalidate();
            }
        }

        public void FocusInput() => _textBox.Focus();

        public void Clear() => _textBox.Clear();

        public void SetError(string message) => ErrorText = message;

        public void ClearError() => ErrorText = string.Empty;

        private void TogglePassword()
        {
            if (!_isPassword)
            {
                return;
            }

            _textBox.UseSystemPasswordChar = !_textBox.UseSystemPasswordChar;
            _passwordButton.Image = _textBox.UseSystemPasswordChar ? _showPasswordIcon : _hidePasswordIcon;
            _passwordButton.AccessibleDescription = _textBox.UseSystemPasswordChar ? "Hiện mật khẩu" : "Ẩn mật khẩu";
            _textBox.Focus();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _textBox.Enabled = Enabled;
            _placeholderLabel.Enabled = Enabled;
            _passwordButton.Enabled = Enabled;
            UpdatePlaceholderVisibility();
            Invalidate();
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var hasError = !string.IsNullOrWhiteSpace(ErrorText);
            var borderColor = hasError ? ErrorColor : _textBox.Focused ? FocusBorderColor : BorderColor;
            var inputBounds = GetInputBounds();

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = AppButton.CreateRoundedRectangle(inputBounds, 4);
            using var background = new SolidBrush(SurfaceColor);
            using var pen = new Pen(borderColor, hasError || _textBox.Focused ? 1.5f : 1f);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(pen, path);

            if (_leadingIcon is not null)
            {
                var iconBounds = new Rectangle(13, InputTop + (InputHeight - IconSize) / 2, IconSize, IconSize);
                e.Graphics.DrawImage(_leadingIcon, iconBounds);
            }

            if (hasError && _errorIcon is not null)
            {
                e.Graphics.DrawImage(_errorIcon, new Rectangle(2, ErrorTop + (ErrorHeight - 14) / 2, 14, 14));
            }
        }

        private void LayoutChildren()
        {
            _label.SetBounds(0, 0, Width, LabelHeight);

            var rightPadding = _isPassword ? PasswordButtonWidth + 8 : 12;
            var textWidth = Math.Max(40, Width - TextLeft - rightPadding);
            var textBoxHeight = GetTextBoxHeight();
            var textTop = InputTop + Math.Max(0, (InputHeight - textBoxHeight) / 2);
            _textBox.SetBounds(TextLeft, textTop, textWidth, textBoxHeight);
            _placeholderLabel.SetBounds(TextLeft, InputTop + 1, textWidth, InputHeight - 2);

            if (_isPassword)
            {
                _passwordButton.SetBounds(Width - PasswordButtonWidth - 2, InputTop + 2, PasswordButtonWidth, InputHeight - 4);
            }

            _errorLabel.SetBounds(22, ErrorTop, Math.Max(40, Width - 22), ErrorHeight);
            UpdatePlaceholderVisibility();
            _passwordButton.BringToFront();
            Invalidate();
        }

        private int GetTextBoxHeight()
        {
            var preferredHeight = _textBox.PreferredHeight;
            var fontHeight = _textBox.Font.Height + TextBoxVerticalPadding;
            var maximumHeight = Math.Max(1, InputHeight - 6);
            return Math.Min(maximumHeight, Math.Max(preferredHeight, fontHeight));
        }

        private void UpdatePlaceholderVisibility()
        {
            _placeholderLabel.Visible = Enabled &&
                !_textBox.Focused &&
                string.IsNullOrEmpty(_textBox.Text) &&
                !string.IsNullOrWhiteSpace(_placeholderLabel.Text);

            if (_placeholderLabel.Visible)
            {
                _placeholderLabel.BringToFront();
            }
        }

        private Rectangle GetInputBounds() =>
            new(0, InputTop, Math.Max(1, Width - 1), InputHeight - 1);
    }
}
