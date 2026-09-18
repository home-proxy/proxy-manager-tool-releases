using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppUnderlineTabs : UserControl
{
    private static readonly Color ActiveColor = Color.FromArgb(15, 108, 189);
    private static readonly Color ActiveBackColor = Color.FromArgb(235, 243, 252);

    private readonly FlowLayoutPanel _tabsPanel = new();
    private readonly List<TabButton> _buttons = [];
    private string _selectedKey = string.Empty;

    public AppUnderlineTabs()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 44;
        MinimumSize = new Size(120, 44);
        BackColor = LightTheme.Background;

        _tabsPanel.Dock = DockStyle.Fill;
        _tabsPanel.FlowDirection = FlowDirection.LeftToRight;
        _tabsPanel.WrapContents = false;
        _tabsPanel.BackColor = LightTheme.Background;
        _tabsPanel.Margin = Padding.Empty;
        _tabsPanel.Padding = new Padding(0, 0, 0, 1);
        Controls.Add(_tabsPanel);
    }

    public event EventHandler<string>? SelectedTabChanged;

    public string SelectedKey
    {
        get => _selectedKey;
        set => SelectTab(value, raiseEvent: false);
    }

    public void SetTabs(IEnumerable<AppUnderlineTab> tabs)
    {
        _tabsPanel.SuspendLayout();
        try
        {
            _tabsPanel.Controls.Clear();
            _buttons.Clear();

            foreach (var tab in tabs)
            {
                var button = new TabButton(tab.Key, tab.Text)
                {
                    Height = 42,
                    Width = Math.Max(96, TextRenderer.MeasureText(tab.Text, new Font("Segoe UI", 10f)).Width + 28),
                    Margin = new Padding(0, 0, 20, 0)
                };
                button.Click += (_, _) => SelectTab(button.Key, raiseEvent: true);
                _buttons.Add(button);
                _tabsPanel.Controls.Add(button);
            }
        }
        finally
        {
            _tabsPanel.ResumeLayout();
        }

        if (_buttons.Count == 0)
        {
            _selectedKey = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedKey) || _buttons.All(button => button.Key != _selectedKey))
        {
            _selectedKey = _buttons[0].Key;
        }

        RefreshSelection();
    }

    private void SelectTab(string key, bool raiseEvent)
    {
        if (_buttons.All(button => button.Key != key) || string.Equals(_selectedKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _selectedKey = key;
        RefreshSelection();
        if (raiseEvent)
        {
            SelectedTabChanged?.Invoke(this, key);
        }
    }

    private void RefreshSelection()
    {
        foreach (var button in _buttons)
        {
            button.IsSelected = string.Equals(button.Key, _selectedKey, StringComparison.Ordinal);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(LightTheme.Border, 1);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }

    private sealed class TabButton : Button
    {
        private bool _isHovering;
        private bool _isSelected;

        public TabButton(string key, string text)
        {
            Key = key;
            Text = text;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            Padding = new Padding(10, 0, 10, 0);
            MinimumSize = new Size(72, 42);
            Tag = LightTheme.SkipButtonThemeTag;
        }

        public string Key { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovering = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

            var backColor = _isSelected
                ? ActiveBackColor
                : _isHovering
                    ? Color.FromArgb(245, 245, 245)
                    : Parent?.BackColor ?? LightTheme.Background;
            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            var textColor = _isSelected ? LightTheme.Foreground : LightTheme.Muted;
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (_isSelected)
            {
                using var pen = new Pen(ActiveColor, 3);
                e.Graphics.DrawLine(pen, 10, Height - 3, Width - 10, Height - 3);
            }
        }
    }
}

internal sealed record AppUnderlineTab(string Key, string Text);
