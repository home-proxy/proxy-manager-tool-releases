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
    private const int ContentWidth = 480;
    private const int FooterHeight = 72;
    private const int TitleHeight = 36;
    private const int SubtitleHeight = 28;
    private const int SwitchHeight = 28;
    private const int LogoHeight = 44;
    private const int SidePadding = 40;
    private const int SendButtonWidth = 96;
    private const int ContentTopPadding = 16;
    private const int ContentBottomPadding = 8;
    private const int LogoGap = 12;
    private const int TitleGap = 6;
    private const int SubtitleGap = 2;
    private const int SwitchGap = 16;
    private const int StatusGap = 4;
    private const int StatusHeight = 24;

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
    private const string ResetCodeRequiredMessage = "Vui lòng nhập mã nhận được từ email.";
    private const string ResetCodeInvalidMessage = "Mã không tồn tại hoặc đã hết hạn.";
    private const string EmailNotFoundMessage = "Email không tồn tại.";

    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAuthService _authService;
    private readonly Panel _bodyPanel = new();
    private readonly Panel _content = new();
    private readonly Panel _fieldsHost = new();
    private readonly Label _titleLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly AppPrimaryButton _submitButton = new();
    private readonly ProgressBar _busyBar = new();
    private readonly Label _statusLabel = new();
    private readonly FlowLayoutPanel _switchPanel = new();
    private readonly LinkLabel _switchModeLink = new();
    private readonly Label _switchPromptLabel = new();
    private int _nextFieldTop;
    private bool _isLayingOut;
    private bool _isBusy;
    private string _idleButtonText = "Đăng nhập";

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
    private readonly AuthTextInput _forgotEmailInput = new(
        "Email",
        "Nhập email đã đăng ký",
        "new-design/email.svg");
    private readonly AuthTextInput _forgotCodeInput = new(
        "Mã xác nhận",
        "Nhập mã nhận được từ email",
        "new-design/lock.svg");
    private readonly AuthTextInput _forgotPasswordInput = new(
        "Mật khẩu mới",
        "Nhập mật khẩu mới",
        "new-design/lock.svg",
        isPassword: true);
    private readonly AppButton _sendCodeButton = new();

    private AuthMode _mode = AuthMode.Login;

    public LoginForm(IAuthService authService)
    {
        _authService = authService;

        Text = AppBranding.AppDisplayName;
        ClientSize = new Size(FormWidth, 560);
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
        _bodyPanel.Padding = Padding.Empty;

        _content.Width = ContentWidth;
        _content.BackColor = LightTheme.Surface;

        var logoBox = BuildLogoBox();
        _content.Controls.Add(logoBox);

        _titleLabel.AutoSize = false;
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _titleLabel.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
        _titleLabel.ForeColor = Color.FromArgb(36, 36, 36);
        _content.Controls.Add(_titleLabel);

        _subtitleLabel.AutoSize = false;
        _subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _subtitleLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _subtitleLabel.ForeColor = Color.FromArgb(115, 115, 115);
        _content.Controls.Add(_subtitleLabel);

        _switchPanel.AutoSize = false;
        _switchPanel.FlowDirection = FlowDirection.LeftToRight;
        _switchPanel.WrapContents = false;
        _switchPanel.Resize += (_, _) => CenterSwitchPanel(_switchPanel);

        _switchPromptLabel.AutoSize = true;
        _switchPromptLabel.Font = new Font("Segoe UI", 12.5f, FontStyle.Regular);
        _switchPromptLabel.ForeColor = Color.FromArgb(115, 115, 115);
        _switchPromptLabel.Margin = new Padding(0, 1, 4, 0);

        _switchModeLink.AutoSize = true;
        _switchModeLink.Font = new Font("Segoe UI", 12.5f, FontStyle.Regular);
        _switchModeLink.LinkColor = LightTheme.Accent;
        _switchModeLink.ActiveLinkColor = LightTheme.AccentPressed;
        _switchModeLink.VisitedLinkColor = LightTheme.Accent;
        _switchModeLink.Margin = new Padding(0, 1, 0, 0);
        _switchModeLink.LinkBehavior = LinkBehavior.AlwaysUnderline;
        _switchModeLink.Click += (_, _) =>
        {
            _mode = _mode == AuthMode.Login ? AuthMode.Register : AuthMode.Login;
            RenderMode();
        };

        _sendCodeButton.Text = "Gửi";
        _sendCodeButton.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _sendCodeButton.Size = new Size(SendButtonWidth, AuthTextInput.InputBoxHeight);
        _sendCodeButton.Margin = Padding.Empty;
        _sendCodeButton.Click += async (_, _) => await SendResetCodeAsync();

        _switchPanel.Controls.Add(_switchPromptLabel);
        _switchPanel.Controls.Add(_switchModeLink);
        _content.Controls.Add(_switchPanel);

        _fieldsHost.BackColor = LightTheme.Surface;
        _content.Controls.Add(_fieldsHost);

        _statusLabel.AutoSize = false;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _statusLabel.ForeColor = LightTheme.Accent;
        _statusLabel.BackColor = LightTheme.Surface;
        _content.Controls.Add(_statusLabel);

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
            Padding = new Padding(SidePadding, 12, SidePadding, 12)
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(224, 224, 224));
            e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
        };

        _submitButton.Text = "Đăng nhập";
        _submitButton.Dock = DockStyle.Fill;
        _submitButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _submitButton.Margin = new Padding(0, 8, 0, 0);
        _submitButton.MinimumSize = new Size(0, 44);
        _submitButton.Click += async (_, _) => await SubmitAsync();

        _busyBar.Dock = DockStyle.Top;
        _busyBar.Height = 4;
        _busyBar.Style = ProgressBarStyle.Marquee;
        _busyBar.MarqueeAnimationSpeed = 24;
        _busyBar.Visible = false;

        footer.Controls.Add(_submitButton);
        footer.Controls.Add(_busyBar);
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

    private int DesiredClientHeight =>
        ContentTopPadding + MeasureStack(_fieldsHost.Height) + ContentBottomPadding + FooterHeight;

    private static int MeasureStack(int fieldsHeight) =>
        LogoHeight + LogoGap +
        TitleHeight + TitleGap +
        SubtitleHeight + SubtitleGap +
        SwitchHeight + SwitchGap +
        fieldsHeight + StatusGap +
        StatusHeight;

    private void RenderMode()
    {
        _fieldsHost.Controls.Clear();
        SetStatusInfo(string.Empty);

        switch (_mode)
        {
            case AuthMode.Register:
                Text = "Đăng ký ProxyManager";
                _titleLabel.Text = "Tạo tài khoản mới";
                _subtitleLabel.Text = "Điền thông tin bên dưới để tạo tài khoản.";
                _switchPromptLabel.Text = "Bạn đã có tài khoản?";
                _switchModeLink.Text = "Đăng nhập";
                _idleButtonText = "Đăng ký";
                BuildRegisterFields();
                break;
            case AuthMode.ForgotPassword:
                Text = "Đặt lại mật khẩu";
                _titleLabel.Text = "Đặt lại mật khẩu";
                _subtitleLabel.Text = "Nhập email đã đăng ký. Mã xác nhận sẽ được gửi tới email đó.";
                _switchPromptLabel.Text = "Bạn đã nhớ mật khẩu?";
                _switchModeLink.Text = "Đăng nhập";
                _idleButtonText = "Xác nhận";
                BuildForgotFields();
                break;
            default:
                Text = "Đăng nhập ProxyManager";
                _titleLabel.Text = "Đăng nhập";
                _subtitleLabel.Text = "Nhập số điện thoại và mật khẩu để tiếp tục.";
                _switchPromptLabel.Text = "Bạn chưa có tài khoản?";
                _switchModeLink.Text = "Đăng ký";
                _idleButtonText = "Đăng nhập";
                BuildLoginFields();
                break;
        }

        if (!_isBusy)
        {
            _submitButton.Text = _idleButtonText;
        }

        CenterSwitchPanel(_switchPanel);
        LayoutContent();
    }

    private void BuildLoginFields()
    {
        _nextFieldTop = 0;
        AddFullWidthField(_loginPhoneInput);
        AddFullWidthField(_loginPasswordInput);

        var forgotPassword = new LinkLabel
        {
            Text = "Quên mật khẩu?",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            LinkColor = LightTheme.Accent,
            ActiveLinkColor = LightTheme.AccentPressed,
            VisitedLinkColor = LightTheme.Accent,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = Padding.Empty
        };
        forgotPassword.LinkClicked += (_, _) =>
        {
            _mode = AuthMode.ForgotPassword;
            RenderMode();
        };
        forgotPassword.Location = new Point(
            Math.Max(0, ContentWidth - forgotPassword.PreferredSize.Width),
            _nextFieldTop + 8);
        _fieldsHost.Controls.Add(forgotPassword);
        _fieldsHost.Height = forgotPassword.Bottom + 4;
    }

    private void BuildForgotFields()
    {
        _nextFieldTop = 0;
        AddFullWidthField(_forgotEmailInput);
        AddFullWidthField(_forgotCodeInput);
        _sendCodeButton.Location = new Point(
            ContentWidth - SendButtonWidth,
            _forgotCodeInput.Top + AuthTextInput.InputBoxTop);
        _fieldsHost.Controls.Add(_sendCodeButton);
        _forgotCodeInput.Width = Math.Max(120, ContentWidth - SendButtonWidth - 8);
        AddFullWidthField(_forgotPasswordInput);
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
        _nextFieldTop += field.Height;
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
        if (_isLayingOut || _bodyPanel.ClientSize.Width <= 0)
        {
            return;
        }

        _isLayingOut = true;
        try
        {
            if (ClientSize.Width != FormWidth || ClientSize.Height != DesiredClientHeight)
            {
                MinimumSize = Size.Empty;
                ClientSize = new Size(FormWidth, DesiredClientHeight);
                MinimumSize = new Size(Width, Height);
            }

            var width = Math.Min(ContentWidth, Math.Max(280, _bodyPanel.ClientSize.Width - (SidePadding * 2)));
            var left = Math.Max(SidePadding, (_bodyPanel.ClientSize.Width - width) / 2);
            var y = ContentTopPadding;

            if (_content.Controls.Count > 0)
            {
                var logo = _content.Controls[0];
                logo.Location = new Point(left + Math.Max(0, (width - logo.Width) / 2), y);
                y += LogoHeight + LogoGap;
            }

            _titleLabel.SetBounds(left, y, width, TitleHeight);
            y += TitleHeight + TitleGap;

            _subtitleLabel.SetBounds(left, y, width, SubtitleHeight);
            y += SubtitleHeight + SubtitleGap;

            _switchPanel.SetBounds(left, y, width, SwitchHeight);
            CenterSwitchPanel(_switchPanel);
            y += SwitchHeight + SwitchGap;

            foreach (Control field in _fieldsHost.Controls)
            {
                if (ReferenceEquals(field, _sendCodeButton))
                {
                    continue;
                }

                if (ReferenceEquals(field, _forgotCodeInput) && _mode == AuthMode.ForgotPassword)
                {
                    field.Left = 0;
                    field.Width = Math.Max(120, width - SendButtonWidth - 8);
                }
                else if (field is AuthTextInput)
                {
                    field.Left = 0;
                    field.Width = width;
                }
                else
                {
                    field.Left = Math.Max(0, width - field.PreferredSize.Width);
                }
            }

            if (_mode == AuthMode.ForgotPassword && _fieldsHost.Controls.Contains(_sendCodeButton))
            {
                _sendCodeButton.SetBounds(
                    width - SendButtonWidth,
                    _forgotCodeInput.Top + AuthTextInput.InputBoxTop,
                    SendButtonWidth,
                    AuthTextInput.InputBoxHeight);
                _sendCodeButton.BringToFront();
            }

            _fieldsHost.SetBounds(left, y, width, Math.Max(1, _fieldsHost.Height));
            y += _fieldsHost.Height + StatusGap;

            _statusLabel.SetBounds(left, y, width, StatusHeight);
            _content.SetBounds(0, 0, _bodyPanel.ClientSize.Width, _bodyPanel.ClientSize.Height);
        }
        finally
        {
            _isLayingOut = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (_isBusy)
        {
            return;
        }

        switch (_mode)
        {
            case AuthMode.Register:
                await RegisterAsync();
                return;
            case AuthMode.ForgotPassword:
                await ResetPasswordAsync();
                return;
            default:
                await LoginAsync();
                return;
        }
    }

    private async Task SendResetCodeAsync()
    {
        if (_isBusy)
        {
            return;
        }

        ClearAuthErrors();
        AuthTextInput? firstInvalid = null;
        if (!ValidateEmail(_forgotEmailInput, ref firstInvalid))
        {
            SetStatusError("Vui lòng kiểm tra lại email.");
            firstInvalid?.FocusInput();
            return;
        }

        try
        {
            SetBusy(true, "Đang gửi mã...");
            await Task.Yield();
            await _authService.ForgotPasswordAsync(_forgotEmailInput.TextValue.Trim(), CancellationToken.None);
            SetStatusInfo("Đã gửi mã tới email của bạn. Hãy nhập mã và mật khẩu mới.");
            _forgotCodeInput.FocusInput();
        }
        catch (AuthApiException ex)
        {
            HandleForgotAuthError(ex);
        }
        catch (Exception ex)
        {
            SetStatusError(FriendlyAuthMessage(ex, "Không thể gửi mã đặt lại mật khẩu. Vui lòng thử lại sau."));
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private async Task ResetPasswordAsync()
    {
        ClearAuthErrors();
        if (!ValidateForgotInputs())
        {
            return;
        }

        try
        {
            SetBusy(true, "Đang đặt lại mật khẩu...");
            await Task.Yield();
            await _authService.ResetPasswordAsync(
                _forgotEmailInput.TextValue.Trim(),
                _forgotCodeInput.TextValue.Trim(),
                _forgotPasswordInput.TextValue,
                CancellationToken.None);
            _forgotPasswordInput.Clear();
            _forgotCodeInput.Clear();
            _mode = AuthMode.Login;
            RenderMode();
            SetStatusInfo("Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.");
        }
        catch (AuthApiException ex)
        {
            HandleForgotAuthError(ex);
        }
        catch (Exception ex)
        {
            SetStatusError(FriendlyAuthMessage(ex, "Không thể đặt lại mật khẩu. Vui lòng thử lại sau."));
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
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
            SetBusy(true, busyText);
            await Task.Yield();
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
            SetStatusError(FriendlyAuthMessage(
                ex,
                _mode == AuthMode.Register
                    ? "Không thể đăng ký tài khoản. Vui lòng thử lại sau."
                    : "Không thể xác thực tài khoản. Vui lòng thử lại sau."));
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
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

    private void HandleForgotAuthError(AuthApiException exception)
    {
        if (exception.HasError("email", "emailNotExists"))
        {
            SetFieldError(_forgotEmailInput, EmailNotFoundMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đặt lại mật khẩu.");
            return;
        }

        if (exception.HasError("email", "invalidEmail"))
        {
            SetFieldError(_forgotEmailInput, EmailInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại thông tin đặt lại mật khẩu.");
            return;
        }

        if (exception.HasError("hash", "invalidHash"))
        {
            SetFieldError(_forgotCodeInput, ResetCodeInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại mã đặt lại mật khẩu.");
            return;
        }

        if (exception.HasError("password", "weakPassword") ||
            exception.HasError("password", "invalidPassword"))
        {
            SetFieldError(_forgotPasswordInput, RegisterPasswordInvalidMessage);
            SetStatusError("Vui lòng kiểm tra lại mật khẩu mới.");
            return;
        }

        SetStatusError("Không thể đặt lại mật khẩu với thông tin này.");
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

    private bool ValidateForgotInputs()
    {
        AuthTextInput? firstInvalid = null;
        var isValid = true;

        isValid &= ValidateEmail(_forgotEmailInput, ref firstInvalid);
        isValid &= ValidateRequired(_forgotCodeInput, ResetCodeRequiredMessage, ref firstInvalid);
        isValid &= ValidateRegisterPassword(_forgotPasswordInput, ref firstInvalid);

        if (!isValid)
        {
            SetStatusError("Vui lòng kiểm tra lại thông tin đặt lại mật khẩu.");
            firstInvalid?.FocusInput();
        }
        else
        {
            SetStatusInfo(string.Empty);
        }

        return isValid;
    }

    private string FriendlyAuthMessage(Exception exception, string fallback)
    {
        var message = UiFeedback.GetFriendlyErrorMessage(exception);
        return string.Equals(message, exception.Message, StringComparison.Ordinal) ? fallback : message;
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
            SetFieldError(_mode == AuthMode.Register ? _registerPhoneInput : _loginPhoneInput, PhoneRequiredMessage);
            handled = true;
        }

        if (string.Equals(exception.ParamName, "password", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(PasswordInputForMode(), PasswordRequiredMessage);
            handled = true;
        }

        if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_mode == AuthMode.ForgotPassword ? _forgotEmailInput : _registerEmailInput, EmailRequiredMessage);
            handled = true;
        }

        if (message.Contains("Reset code", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_forgotCodeInput, ResetCodeRequiredMessage);
            handled = true;
        }

        if (message.Contains("First name", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Last name", StringComparison.OrdinalIgnoreCase))
        {
            SetFieldError(_registerFullNameInput, FullNameInvalidMessage);
            handled = true;
        }

        SetStatusError(handled
            ? _mode switch
            {
                AuthMode.Register => "Vui lòng kiểm tra lại thông tin đăng ký.",
                AuthMode.ForgotPassword => "Vui lòng kiểm tra lại thông tin đặt lại mật khẩu.",
                _ => "Vui lòng kiểm tra lại thông tin đăng nhập."
            }
            : UiFeedback.GetFriendlyErrorMessage(exception));
    }

    private AuthTextInput PasswordInputForMode() =>
        _mode switch
        {
            AuthMode.Register => _registerPasswordInput,
            AuthMode.ForgotPassword => _forgotPasswordInput,
            _ => _loginPasswordInput
        };

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

    private void SetBusy(bool busy, string? message = null)
    {
        _isBusy = busy;
        UseWaitCursor = busy;
        _submitButton.Enabled = !busy;
        _submitButton.Text = busy ? message ?? "Đang xử lý..." : _idleButtonText;
        _switchModeLink.Enabled = !busy;
        _loginPhoneInput.Enabled = !busy;
        _loginPasswordInput.Enabled = !busy;
        _registerFullNameInput.Enabled = !busy;
        _registerEmailInput.Enabled = !busy;
        _registerPhoneInput.Enabled = !busy;
        _registerPasswordInput.Enabled = !busy;
        _forgotEmailInput.Enabled = !busy;
        _forgotCodeInput.Enabled = !busy;
        _forgotPasswordInput.Enabled = !busy;
        _sendCodeButton.Enabled = !busy;
        _busyBar.Visible = busy;
        _busyBar.MarqueeAnimationSpeed = busy ? 24 : 0;

        if (busy && !string.IsNullOrWhiteSpace(message))
        {
            SetStatusInfo(message);
        }

        _submitButton.Refresh();
        _busyBar.Refresh();
    }

    private void SetStatusInfo(string message)
    {
        _statusLabel.ForeColor = LightTheme.Accent;
        _statusLabel.Text = message;
    }

    private void SetStatusError(string message)
    {
        _statusLabel.ForeColor = Color.FromArgb(177, 14, 28);
        _statusLabel.Text = message;
    }

    private void ClearAuthErrors()
    {
        _loginPhoneInput.ClearError();
        _loginPasswordInput.ClearError();
        _registerFullNameInput.ClearError();
        _registerEmailInput.ClearError();
        _registerPhoneInput.ClearError();
        _registerPasswordInput.ClearError();
        _forgotEmailInput.ClearError();
        _forgotCodeInput.ClearError();
        _forgotPasswordInput.ClearError();
    }

    private enum AuthMode
    {
        Login,
        Register,
        ForgotPassword
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
        private const int LabelInputGap = 12;
        public const int InputBoxTop = LabelHeight + LabelInputGap;
        public const int InputBoxHeight = 40;
        private const int InputTop = InputBoxTop;
        private const int InputHeight = InputBoxHeight;
        private const int ErrorTop = InputTop + InputHeight + 2;
        private const int ErrorHeight = 16;
        public const int ControlHeight = ErrorTop + ErrorHeight;
        private const int StableHeight = ControlHeight;
        private const int IconSize = 20;
        private const int TextLeft = 44;
        private const int TextBoxVerticalPadding = 8;
        private const int PasswordButtonWidth = 40;

        private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
        private static readonly Color BorderColor = Color.FromArgb(209, 209, 209);
        private static readonly Color FocusBorderColor = LightTheme.Accent;
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
