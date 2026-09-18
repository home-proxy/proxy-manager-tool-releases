using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.App.Controls;

public enum ApplicationSelectionMode
{
    AddMultiple,
    UpdateSingle
}

public sealed class RunningApplicationSelectionControl : UserControl
{
    private const string ManualExecutableKeyPrefix = "manual:path:";
    private const int DropZoneHeight = 82;
    private const int FilterRowHeight = 42;
    private const int StatusRowHeight = 32;
    private const int CompactGridHeaderHeight = 36;
    private const int CompactGridRowHeight = 28;
    private const int ApplicationIconSize = 18;
    private const int DropZoneIconSize = 24;
    private const int RefreshButtonSize = 32;
    private const int StatusBadgeRadius = 8;

    private static readonly Color AccentBlue = Color.FromArgb(15, 108, 189);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color MutedTextColor = Color.FromArgb(115, 115, 115);

    private readonly IEmulatorProcessScanner _emulatorProcessScanner;
    private readonly IAppProcessScanner _processScanner;
    private readonly ApplicationSelectionMode _mode;
    private readonly AppTextInput _runningApplicationSearchTextBox = new() { Width = 320 };
    private readonly ComboBox _runningSelectionFilterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 183 };
    private readonly DataGridView _runningApplicationGrid = new AppDataGridView();
    private readonly Label _runningApplicationStatusLabel = new() { AutoSize = true };
    private readonly TableLayoutPanel _runningApplicationActionsLayout = new();
    private readonly ToolTip _toolTip = new();
    private readonly Dictionary<string, Image> _runningApplicationIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _manualExecutablePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _importedExecutablePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pickedExecutablePaths = new(StringComparer.OrdinalIgnoreCase);
    private List<RunningApplicationRow> _runningApplicationRows = [];
    private IReadOnlyList<ApplicationRule> _existingRules = [];
    private ApplicationRule? _currentRule;
    private Control? _runningApplicationRefreshButton;
    private bool _isRunningApplicationScanInProgress;
    private bool _hasLoadedRunningApplications;
    private bool _isBinding;

    public RunningApplicationSelectionControl(
        IEmulatorProcessScanner emulatorProcessScanner,
        IAppProcessScanner processScanner,
        ApplicationSelectionMode mode)
    {
        _emulatorProcessScanner = emulatorProcessScanner;
        _processScanner = processScanner;
        _mode = mode;

        Dock = DockStyle.Fill;
        Controls.Add(BuildApplicationSelectionSurface());
        LightTheme.Apply(this);
        RestoreCustomButtonStyles(this);
        _runningApplicationGrid.CellPainting += PaintRunningApplicationHeaderCell;
        ApplyRunningApplicationGridMetrics();
    }

    public async Task EnsureLoadedAsync()
    {
        if (!_hasLoadedRunningApplications)
        {
            await RefreshRunningApplicationsAsync(markNewRows: false);
        }
    }

    public void LoadAddContext(IReadOnlyList<ApplicationRule> rules)
    {
        _currentRule = null;
        _existingRules = rules;
        SeedImportedExecutablePaths();
        foreach (var row in _runningApplicationRows)
        {
            row.IsImported = IsImportedCandidate(row.Candidate);
            row.IsSelected = row.IsImported;
        }

        var existingKeys = _runningApplicationRows
            .Select(row => row.Candidate.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        AddMissingManualExecutableRows(markNewRows: false, existingKeys);
        BindRunningApplicationGrid();
    }

    public void LoadUpdateContext(ApplicationRule currentRule, IReadOnlyList<ApplicationRule> existingRules)
    {
        _currentRule = currentRule;
        _existingRules = existingRules
            .Where(rule => rule.Id != currentRule.Id)
            .ToList();
        SeedImportedExecutablePaths();
        AddCurrentRuleManualPath();
        foreach (var row in _runningApplicationRows)
        {
            row.IsImported = IsImportedCandidate(row.Candidate);
            row.IsSelected = !row.IsImported && IsCurrentCandidate(row.Candidate);
        }

        var existingKeys = _runningApplicationRows
            .Select(row => row.Candidate.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        AddMissingManualExecutableRows(markNewRows: false, existingKeys);
        BindRunningApplicationGrid();
    }

    public bool TryCreateSelectedRules(IWin32Window owner, out IReadOnlyList<ApplicationRule> rules)
    {
        _runningApplicationGrid.EndEdit();
        rules = [];
        var selected = _runningApplicationRows
            .Where(row => row.IsSelected && !row.IsImported)
            .Select(row => row.Candidate)
            .ToList();

        if (_mode == ApplicationSelectionMode.UpdateSingle && selected.Count != 1)
        {
            MessageBox.Show(owner, "Hãy chọn đúng một ứng dụng để cập nhật.", "Thiếu lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (_mode == ApplicationSelectionMode.AddMultiple && selected.Count == 0)
        {
            MessageBox.Show(owner, "Hãy chọn ít nhất một ứng dụng mới.", "Thiếu lựa chọn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        var result = RunningApplicationRuleFactory.CreateRules(selected);
        if (result.Rules.Count == 0)
        {
            MessageBox.Show(owner, "Không thể thêm ứng dụng đã chọn vì chưa lấy được đường dẫn file .exe.", "Thiếu đường dẫn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_mode == ApplicationSelectionMode.UpdateSingle && result.Rules.Count != 1)
        {
            MessageBox.Show(owner, "Ứng dụng đã chọn tạo ra nhiều target. Hãy chọn một process hoặc file .exe cụ thể hơn.", "Không thể cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (result.SkippedCount > 0)
        {
            MessageBox.Show(owner, $"Đã bỏ qua {result.SkippedCount} ứng dụng chưa lấy được đường dẫn file .exe.", "Một số ứng dụng bị bỏ qua", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        rules = result.Rules;
        return true;
    }

    private Control BuildApplicationSelectionSurface()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DropZoneHeight + 12));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, FilterRowHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusRowHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(BuildExecutableDropZone(), 0, 0);
        layout.Controls.Add(BuildRunningApplicationActions(), 0, 1);

        _runningApplicationStatusLabel.Text = "Đang chuẩn bị quét ứng dụng đang chạy...";
        _runningApplicationStatusLabel.ForeColor = TextColor;
        _runningApplicationStatusLabel.Margin = Padding.Empty;
        layout.Controls.Add(BuildRunningApplicationStatusRow(), 0, 2);

        ConfigureRunningApplicationGrid();
        layout.Controls.Add(_runningApplicationGrid, 0, 3);

        return layout;
    }

    private Control BuildExecutableDropZone()
    {
        var panel = new DashedDropZonePanel
        {
            Dock = DockStyle.Top,
            Height = DropZoneHeight,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.White,
            AllowDrop = true,
            Padding = new Padding(12, 12, 9, 12),
            Margin = new Padding(0, 0, 0, 12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var icon = new PictureBox
        {
            Image = UiIcons.NewUploadApplication,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = DropZoneIconSize + 8,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0)
        };

        var textBlock = new DropZoneTextBlock
        {
            Dock = DockStyle.Fill,
            Subtitle = _mode == ApplicationSelectionMode.UpdateSingle
                ? "File được chọn sẽ thay thế ứng dụng hiện tại và tự động tick chọn."
                : "File được chọn sẽ được thêm vào danh sách bên dưới và tự động tick chọn.",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = TextColor,
            Margin = Padding.Empty
        };

        var browseButton = CreatePlainButton("Chọn file...", (_, _) => BrowseExecutables(), new Size(130, 32));
        browseButton.Anchor = AnchorStyles.Right;
        browseButton.Margin = new Padding(16, 0, 0, 0);

        panel.Controls.Add(icon, 0, 0);
        panel.Controls.Add(textBlock, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        EnableExecutableDrop(panel);
        return panel;
    }

    private Control BuildRunningApplicationActions()
    {
        _runningApplicationActionsLayout.Dock = DockStyle.Fill;
        _runningApplicationActionsLayout.AutoSize = false;
        _runningApplicationActionsLayout.ColumnCount = 2;
        _runningApplicationActionsLayout.RowCount = 1;
        _runningApplicationActionsLayout.Padding = Padding.Empty;
        _runningApplicationActionsLayout.Margin = Padding.Empty;
        _runningApplicationActionsLayout.BackColor = Color.White;
        _runningApplicationActionsLayout.ColumnStyles.Clear();
        _runningApplicationActionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _runningApplicationActionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _runningApplicationActionsLayout.RowStyles.Clear();
        _runningApplicationActionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _runningApplicationSearchTextBox.PlaceholderText = "Tìm theo tên ứng dụng";
        _runningApplicationSearchTextBox.TextChanged += (_, _) => BindRunningApplicationGrid();
        _runningSelectionFilterCombo.Items.AddRange(["Tất cả", "Đã chọn", "Chưa chọn"]);
        _runningSelectionFilterCombo.SelectedIndex = 0;
        _runningSelectionFilterCombo.SelectedIndexChanged += (_, _) => BindRunningApplicationGrid();
        var statusLabel = new Label
        {
            Text = "Trạng thái",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = TextColor,
            Margin = new Padding(0, 7, 10, 0)
        };
        _runningApplicationSearchTextBox.Anchor = AnchorStyles.Left;
        _runningApplicationSearchTextBox.Margin = Padding.Empty;
        _runningApplicationSearchTextBox.MinimumSize = new Size(320, _runningApplicationSearchTextBox.MinimumSize.Height);
        _runningSelectionFilterCombo.Anchor = AnchorStyles.Left;
        _runningSelectionFilterCombo.Margin = Padding.Empty;

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty
        };
        filters.Controls.Add(statusLabel);
        filters.Controls.Add(_runningSelectionFilterCombo);

        _runningApplicationActionsLayout.Controls.Add(_runningApplicationSearchTextBox, 0, 0);
        _runningApplicationActionsLayout.Controls.Add(filters, 1, 0);
        return _runningApplicationActionsLayout;
    }

    private Control BuildRunningApplicationStatusRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _runningApplicationStatusLabel.Anchor = AnchorStyles.Left;
        _runningApplicationStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _runningApplicationStatusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        var refreshButton = CreateIconButton(
            UiIcons.NewRefreshApplication,
            async (_, _) => await RefreshRunningApplicationsAsync(markNewRows: true));
        refreshButton.Anchor = AnchorStyles.Left;
        refreshButton.Margin = new Padding(8, 0, 0, 4);
        _toolTip.SetToolTip(refreshButton, "Làm mới");
        _runningApplicationRefreshButton = refreshButton;

        row.Controls.Add(_runningApplicationStatusLabel, 0, 0);
        row.Controls.Add(refreshButton, 1, 0);
        return row;
    }

    private void ConfigureRunningApplicationGrid()
    {
        _runningApplicationGrid.Dock = DockStyle.Fill;
        _runningApplicationGrid.ReadOnly = false;
        _runningApplicationGrid.AllowUserToAddRows = false;
        _runningApplicationGrid.AllowUserToDeleteRows = false;
        _runningApplicationGrid.MultiSelect = false;
        _runningApplicationGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _runningApplicationGrid.AutoGenerateColumns = false;
        _runningApplicationGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _runningApplicationGrid.RowTemplate.Height = CompactGridRowHeight;
        _runningApplicationGrid.ColumnHeaderMouseClick += HandleRunningApplicationColumnHeaderMouseClick;
        _runningApplicationGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_runningApplicationGrid.IsCurrentCellDirty)
            {
                _runningApplicationGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _runningApplicationGrid.CellValueChanged += (_, e) => HandleRunningApplicationCellValueChanged(e);
        _runningApplicationGrid.CellPainting += PaintRunningApplicationNameCell;
        _runningApplicationGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = string.Empty,
            DataPropertyName = nameof(RunningApplicationRow.IsSelected),
            MinimumWidth = 48,
            Width = 48,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _runningApplicationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = string.Empty,
            DataPropertyName = nameof(RunningApplicationRow.DisplayName),
            MinimumWidth = 320,
            FillWeight = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true
        });
        ApplyRunningApplicationGridMetrics();
    }

    private void ApplyRunningApplicationGridMetrics()
    {
        _runningApplicationGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _runningApplicationGrid.ColumnHeadersHeight = CompactGridHeaderHeight;
        _runningApplicationGrid.RowTemplate.Height = CompactGridRowHeight;
        foreach (DataGridViewRow row in _runningApplicationGrid.Rows)
        {
            row.Height = CompactGridRowHeight;
            row.MinimumHeight = CompactGridRowHeight;
        }
    }

    private void HandleRunningApplicationCellValueChanged(DataGridViewCellEventArgs e)
    {
        if (_isBinding ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _runningApplicationGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(RunningApplicationRow.IsSelected) ||
            _runningApplicationGrid.Rows[e.RowIndex].DataBoundItem is not RunningApplicationRow changedRow)
        {
            return;
        }

        if (changedRow.IsImported)
        {
            changedRow.IsSelected = false;
            _runningApplicationGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = false;
            return;
        }

        if (_mode == ApplicationSelectionMode.UpdateSingle && changedRow.IsSelected)
        {
            foreach (var row in _runningApplicationRows.Where(row => !ReferenceEquals(row, changedRow)))
            {
                row.IsSelected = false;
            }

            RefreshVisibleSelectionCells();
        }

        if (_runningSelectionFilterCombo.SelectedIndex > 0)
        {
            BeginInvoke(new Action(BindRunningApplicationGrid));
        }

        _runningApplicationGrid.Invalidate();
    }

    private void HandleRunningApplicationColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_mode != ApplicationSelectionMode.AddMultiple ||
            e.ColumnIndex != 0)
        {
            return;
        }

        ToggleVisibleRunningApplicationSelection();
    }

    private void ToggleVisibleRunningApplicationSelection()
    {
        _runningApplicationGrid.EndEdit();
        var rows = GetVisibleSelectableRunningApplicationRows().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var shouldSelect = rows.Any(row => !row.IsSelected);
        foreach (var row in rows)
        {
            row.IsSelected = shouldSelect;
        }

        RefreshVisibleSelectionCells();
        if (_runningSelectionFilterCombo.SelectedIndex > 0)
        {
            BeginInvoke(new Action(BindRunningApplicationGrid));
        }
        else
        {
            _runningApplicationGrid.Invalidate();
        }
    }

    private IEnumerable<RunningApplicationRow> GetVisibleSelectableRunningApplicationRows()
    {
        foreach (DataGridViewRow gridRow in _runningApplicationGrid.Rows)
        {
            if (gridRow.DataBoundItem is RunningApplicationRow row && !row.IsImported)
            {
                yield return row;
            }
        }
    }

    private void BrowseExecutables()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Ứng dụng Windows (*.exe)|*.exe",
            Title = "Chọn file .exe",
            Multiselect = _mode == ApplicationSelectionMode.AddMultiple
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddExecutablePaths(dialog.FileNames);
        }
    }

    private void HandleExecutableDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = ContainsSupportedDroppedFile(e.Data)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void HandleExecutableDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddExecutablePaths(paths);
        }
    }

    private void EnableExecutableDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter -= HandleExecutableDragEnter;
        control.DragEnter += HandleExecutableDragEnter;
        control.DragDrop -= HandleExecutableDragDrop;
        control.DragDrop += HandleExecutableDragDrop;

        foreach (Control child in control.Controls)
        {
            EnableExecutableDrop(child);
        }
    }

    private static bool ContainsSupportedDroppedFile(IDataObject? data)
    {
        if (data?.GetDataPresent(DataFormats.FileDrop) != true ||
            data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return false;
        }

        return paths.Any(path =>
            string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase));
    }

    private void AddExecutablePaths(IEnumerable<string> paths)
    {
        var added = 0;
        var selected = 0;
        var duplicate = 0;
        var invalid = 0;
        string? firstSelectedPath = null;

        _runningApplicationGrid.EndEdit();
        foreach (var path in _mode == ApplicationSelectionMode.UpdateSingle ? paths.Take(1) : paths)
        {
            if (!TryResolveExecutablePath(path, out var executablePath))
            {
                invalid++;
                continue;
            }

            firstSelectedPath ??= executablePath;
            _pickedExecutablePaths.Add(executablePath);
            var wasManual = !_manualExecutablePaths.Add(executablePath);
            var row = FindRunningApplicationRowByExecutablePath(executablePath);
            if (row is null)
            {
                row = CreateManualExecutableRow(executablePath, isNew: !IsImportedExecutablePath(executablePath));
                _runningApplicationRows.Add(row);
                added++;
            }
            else if (row.IsSelected && wasManual)
            {
                duplicate++;
            }
            else
            {
                selected++;
            }

            row.IsImported = IsImportedCandidate(row.Candidate);
            if (!row.IsImported)
            {
                SelectOnlyIfNeeded(row);
            }
        }

        BindRunningApplicationGrid();
        if (firstSelectedPath is not null)
        {
            SelectRunningApplicationRow(firstSelectedPath);
        }

        UpdateManualExecutableStatus(added, selected, duplicate, invalid);

        if (invalid > 0)
        {
            MessageBox.Show(
                this,
                "Một số file không hợp lệ. Chỉ chấp nhận file .exe hoặc shortcut .lnk trỏ tới file .exe.",
                "File không hợp lệ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool TryResolveExecutablePath(string path, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            executablePath = Path.GetFullPath(path);
            return true;
        }

        if (string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase) &&
            TryResolveShortcutTarget(path, out var targetPath) &&
            File.Exists(targetPath) &&
            string.Equals(Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            executablePath = Path.GetFullPath(targetPath);
            return true;
        }

        return false;
    }

    private static bool TryResolveShortcutTarget(string shortcutPath, out string targetPath)
    {
        targetPath = string.Empty;
        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return false;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            var value = shortcut?.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null);
            targetPath = value as string ?? string.Empty;
            return !string.IsNullOrWhiteSpace(targetPath);
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private void SeedImportedExecutablePaths()
    {
        _importedExecutablePaths.Clear();
        _manualExecutablePaths.Clear();
        foreach (var rule in _existingRules)
        {
            if (!TryGetExecutableRulePath(rule, out var executablePath))
            {
                continue;
            }

            _importedExecutablePaths.Add(executablePath);
            _manualExecutablePaths.Add(executablePath);
        }
    }

    private void AddCurrentRuleManualPath()
    {
        if (_currentRule is not null && TryGetExecutableRulePath(_currentRule, out var executablePath))
        {
            _manualExecutablePaths.Add(executablePath);
        }
    }

    private static bool TryGetExecutableRulePath(ApplicationRule rule, out string executablePath)
    {
        executablePath = string.Empty;
        if (rule.TargetType != ApplicationTargetType.Executable ||
            !IsUsableExecutableRulePath(rule.ExecutableName))
        {
            return false;
        }

        executablePath = rule.ExecutableName.Trim().Trim('"');
        return true;
    }

    private static bool IsUsableExecutableRulePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path.Trim().Trim('"')), ".exe", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshRunningApplicationsAsync(bool markNewRows)
    {
        if (_isRunningApplicationScanInProgress || IsDisposed)
        {
            return;
        }

        _isRunningApplicationScanInProgress = true;
        try
        {
            _runningApplicationGrid.EndEdit();
            var selectedKeys = _runningApplicationRows
                .Where(row => row.IsSelected)
                .Select(row => row.Candidate.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedIdentityPaths = _runningApplicationRows
                .Where(row => row.IsSelected)
                .SelectMany(row => GetCandidateIdentityExecutablePaths(row.Candidate))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingKeys = _runningApplicationRows
                .Select(row => row.Candidate.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _hasLoadedRunningApplications = true;

            _runningApplicationStatusLabel.Text = "Đang quét ứng dụng đang chạy...";
            var emulatorKinds = Enum.GetValues<EmulatorKind>();
            var emulatorTask = _emulatorProcessScanner.ScanAsync(emulatorKinds, CancellationToken.None);
            var processTask = _processScanner.GetRunningProcessesAsync(CancellationToken.None);
            await Task.WhenAll(emulatorTask, processTask);

            var candidates = RunningApplicationRuleFactory.BuildCandidates(
                emulatorTask.Result,
                processTask.Result);
            _runningApplicationRows = candidates
                .Select(candidate =>
                {
                    var isNew = markNewRows && existingKeys.Count > 0 && !existingKeys.Contains(candidate.Key);
                    var isImported = IsImportedCandidate(candidate);
                    var isSelected = !isImported && (IsCurrentCandidate(candidate) ||
                        selectedKeys.Contains(candidate.Key) ||
                        GetCandidateIdentityExecutablePaths(candidate).Any(path =>
                            selectedIdentityPaths.Contains(path) ||
                            (_mode == ApplicationSelectionMode.AddMultiple && _manualExecutablePaths.Contains(path))));
                    if (_mode == ApplicationSelectionMode.AddMultiple)
                    {
                        isSelected = isSelected || isImported;
                    }

                    return new RunningApplicationRow(
                        candidate,
                        GetRunningApplicationIcon(candidate),
                        isSelected,
                        isImported,
                        isNew);
                })
                .ToList();
            AddMissingManualExecutableRows(markNewRows, existingKeys);
            NormalizeSingleSelection();
            BindRunningApplicationGrid();

            var newCount = markNewRows && existingKeys.Count > 0
                ? _runningApplicationRows.Count(row => row.IsNew)
                : 0;
            _runningApplicationStatusLabel.Text =
                $"Tìm thấy {_runningApplicationRows.Count} ứng dụng đang chạy.{FormatManualSummary()}{FormatNewSummary(newCount)}";
        }
        catch (Exception ex)
        {
            _runningApplicationStatusLabel.Text = $"Không thể quét ứng dụng đang chạy: {ex.Message}";
        }
        finally
        {
            _isRunningApplicationScanInProgress = false;
        }
    }

    private void AddMissingManualExecutableRows(bool markNewRows, HashSet<string> existingKeys)
    {
        var representedPaths = _runningApplicationRows
            .SelectMany(row => GetCandidateIdentityExecutablePaths(row.Candidate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _manualExecutablePaths.Where(path => !representedPaths.Contains(path)).ToList())
        {
            var candidate = CreateManualExecutableCandidate(path);
            var isImported = IsImportedCandidate(candidate);
            var isCurrent = IsCurrentCandidate(candidate);
            var isNew = markNewRows && existingKeys.Count > 0 && !existingKeys.Contains(candidate.Key) && !isImported;
            _runningApplicationRows.Add(new RunningApplicationRow(
                candidate,
                GetRunningApplicationIcon(candidate),
                isSelected: isCurrent || (_mode == ApplicationSelectionMode.AddMultiple && isImported),
                isImported,
                isNew));
        }

        NormalizeSingleSelection();
    }

    private void BindRunningApplicationGrid()
    {
        _isBinding = true;
        var query = _runningApplicationRows.Where(MatchesRunningApplicationFilters);
        var filtered = _mode == ApplicationSelectionMode.AddMultiple
            ? query
                .OrderByDescending(row => row.IsNew)
                .ThenByDescending(row => row.IsImported)
                .ThenByDescending(row => row.IsSelected)
                .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : query.ToList();
        _runningApplicationGrid.DataSource = null;
        _runningApplicationGrid.DataSource = filtered;
        ConfigureRunningApplicationRows();
        _isBinding = false;
    }

    private void RefreshVisibleSelectionCells()
    {
        foreach (DataGridViewRow gridRow in _runningApplicationGrid.Rows)
        {
            if (gridRow.DataBoundItem is not RunningApplicationRow row)
            {
                continue;
            }

            gridRow.Cells[0].Value = row.IsSelected;
        }

        _runningApplicationGrid.Refresh();
    }

    private bool MatchesRunningApplicationFilters(RunningApplicationRow row)
    {
        if (!LocalFuzzySearch.IsMatch(row.Candidate.EffectiveSearchText, _runningApplicationSearchTextBox.Text))
        {
            return false;
        }

        return _runningSelectionFilterCombo.SelectedIndex switch
        {
            1 => row.IsSelected,
            2 => !row.IsSelected,
            _ => true
        };
    }

    private void ConfigureRunningApplicationRows()
    {
        ApplyRunningApplicationGridMetrics();
        foreach (DataGridViewRow gridRow in _runningApplicationGrid.Rows)
        {
            if (gridRow.DataBoundItem is not RunningApplicationRow row)
            {
                continue;
            }

            gridRow.Cells[0].ReadOnly = row.IsImported;
            if (row.IsImported)
            {
                gridRow.DefaultCellStyle.ForeColor = LightTheme.Muted;
                gridRow.DefaultCellStyle.SelectionForeColor = LightTheme.Muted;
            }
            else
            {
                gridRow.DefaultCellStyle.ForeColor = _runningApplicationGrid.DefaultCellStyle.ForeColor;
                gridRow.DefaultCellStyle.SelectionForeColor = _runningApplicationGrid.DefaultCellStyle.SelectionForeColor;
            }
        }
    }

    private static string FormatNewSummary(int newCount) =>
        newCount > 0 ? $" Có {newCount} ứng dụng mới." : string.Empty;

    private string FormatManualSummary() =>
        GetPickedNewExecutableCount() > 0 ? $" Đã chọn thêm {GetPickedNewExecutableCount()} file." : string.Empty;

    private int GetPickedNewExecutableCount() =>
        _pickedExecutablePaths.Count(path => !_importedExecutablePaths.Contains(path));

    private void UpdateManualExecutableStatus(int added, int selected, int duplicate, int invalid)
    {
        var parts = new List<string>();
        if (added > 0)
        {
            parts.Add($"thêm mới {added}");
        }

        if (selected > 0)
        {
            parts.Add($"đã tick {selected}");
        }

        if (duplicate > 0)
        {
            parts.Add($"bỏ qua trùng {duplicate}");
        }

        if (invalid > 0)
        {
            parts.Add($"không hợp lệ {invalid}");
        }

        _runningApplicationStatusLabel.Text = parts.Count == 0
            ? $"Tìm thấy {_runningApplicationRows.Count} ứng dụng đang chạy.{FormatManualSummary()}"
            : $"Đã xử lý file: {string.Join("; ", parts)}. Tổng {_runningApplicationRows.Count} ứng dụng đang chạy.{FormatManualSummary()}";
    }

    private void PaintRunningApplicationHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            e.Graphics is null)
        {
            return;
        }

        var propertyName = _runningApplicationGrid.Columns[e.ColumnIndex].DataPropertyName;
        if (propertyName == nameof(RunningApplicationRow.IsSelected))
        {
            e.Handled = true;
            DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true);

            var state = GetSelectAllHeaderCheckBoxState();
            var checkBoxSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            var checkBoxLocation = new Point(
                e.CellBounds.Left + (e.CellBounds.Width - checkBoxSize.Width) / 2,
                e.CellBounds.Top + (e.CellBounds.Height - checkBoxSize.Height) / 2);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxLocation, state);
            return;
        }

        if (propertyName == nameof(RunningApplicationRow.DisplayName))
        {
            e.Handled = true;
            DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true);
            var textBounds = new Rectangle(
                e.CellBounds.Left + 10,
                e.CellBounds.Top,
                Math.Max(1, e.CellBounds.Width - 20),
                e.CellBounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                "Tên ứng dụng",
                _runningApplicationGrid.ColumnHeadersDefaultCellStyle.Font ?? _runningApplicationGrid.Font,
                textBounds,
                TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private System.Windows.Forms.VisualStyles.CheckBoxState GetSelectAllHeaderCheckBoxState()
    {
        if (_mode != ApplicationSelectionMode.AddMultiple)
        {
            return System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled;
        }

        var rows = GetVisibleSelectableRunningApplicationRows().ToList();
        if (rows.Count == 0)
        {
            return System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled;
        }

        var selectedCount = rows.Count(row => row.IsSelected);
        if (selectedCount == 0)
        {
            return System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
        }

        return selectedCount == rows.Count
            ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
            : System.Windows.Forms.VisualStyles.CheckBoxState.MixedNormal;
    }

    private void PaintRunningApplicationNameCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _runningApplicationGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(RunningApplicationRow.DisplayName) ||
            _runningApplicationGrid.Rows[e.RowIndex].DataBoundItem is not RunningApplicationRow row)
        {
            return;
        }

        var graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        e.Handled = true;
        DesignedGridTheme.PaintCellBackgroundAndBorder(graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));

        var cellStyle = e.CellStyle ?? _runningApplicationGrid.DefaultCellStyle;
        var cellFont = cellStyle.Font ?? _runningApplicationGrid.Font;
        var selected = (e.State & DataGridViewElementStates.Selected) != 0;
        var textColor = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        var hasBadge = TryGetRunningApplicationBadge(row, out var badgeText, out var badgeBackColor, out var badgeTextColor);
        var badgeBounds = Rectangle.Empty;
        if (hasBadge)
        {
            var badgeSize = TextRenderer.MeasureText(badgeText, cellFont, Size.Empty, TextFormatFlags.NoPadding);
            var badgeWidth = Math.Min(Math.Max(1, e.CellBounds.Width / 3), badgeSize.Width + 14);
            var badgeHeight = Math.Min(e.CellBounds.Height - 8, badgeSize.Height + 6);
            badgeBounds = new Rectangle(
                e.CellBounds.Right - badgeWidth - 8,
                e.CellBounds.Top + Math.Max(4, (e.CellBounds.Height - badgeHeight) / 2),
                Math.Max(1, badgeWidth),
                Math.Max(1, badgeHeight));
        }

        var iconBounds = new Rectangle(
            e.CellBounds.Left + 10,
            e.CellBounds.Top + (e.CellBounds.Height - ApplicationIconSize) / 2,
            ApplicationIconSize,
            ApplicationIconSize);
        var textBounds = new Rectangle(
            iconBounds.Right + 8,
            e.CellBounds.Top,
            Math.Max(16, (hasBadge ? badgeBounds.Left : e.CellBounds.Right) - iconBounds.Right - 14),
            e.CellBounds.Height);

        graphics.DrawImage(row.Icon, iconBounds);
        TextRenderer.DrawText(
            graphics,
            row.DisplayName,
            cellFont,
            textBounds,
            textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (hasBadge)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = AppButton.CreateRoundedRectangle(badgeBounds, StatusBadgeRadius);
            using var background = new SolidBrush(badgeBackColor);
            graphics.FillPath(background, path);
            TextRenderer.DrawText(
                graphics,
                badgeText,
                cellFont,
                badgeBounds,
                badgeTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private bool TryGetRunningApplicationBadge(RunningApplicationRow row, out string text, out Color backColor, out Color textColor)
    {
        if (row.IsNew)
        {
            text = "New";
            backColor = Color.FromArgb(253, 232, 232);
            textColor = LightTheme.Danger;
            return true;
        }

        if (_mode == ApplicationSelectionMode.UpdateSingle && IsCurrentCandidate(row.Candidate))
        {
            text = "Hiện tại";
            backColor = LightTheme.AccentSoft;
            textColor = AccentBlue;
            return true;
        }

        text = string.Empty;
        backColor = Color.Empty;
        textColor = Color.Empty;
        return false;
    }

    private Image GetRunningApplicationIcon(RunningApplicationCandidate candidate)
    {
        var path = candidate.Emulator?.ExecutablePath ??
            candidate.Process?.ExecutablePath ??
            candidate.TargetExecutablePaths.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return GetDefaultRunningApplicationIcon();
        }

        if (_runningApplicationIconCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            var image = icon?.ToBitmap() ?? GetDefaultRunningApplicationIcon();
            _runningApplicationIconCache[path] = image;
            return image;
        }
        catch
        {
            return GetDefaultRunningApplicationIcon();
        }
    }

    private Image GetDefaultRunningApplicationIcon()
    {
        const string key = "__default__";
        if (_runningApplicationIconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = SystemIcons.Application.ToBitmap();
        _runningApplicationIconCache[key] = image;
        return image;
    }

    private RunningApplicationRow CreateManualExecutableRow(string executablePath, bool isNew)
    {
        var candidate = CreateManualExecutableCandidate(executablePath);
        var isImported = IsImportedCandidate(candidate);
        return new RunningApplicationRow(
            candidate,
            GetRunningApplicationIcon(candidate),
            isSelected: !isImported,
            isImported,
            isNew && !isImported);
    }

    private static RunningApplicationCandidate CreateManualExecutableCandidate(string executablePath)
    {
        var displayName = ApplicationRule.GetExecutableDisplayName(executablePath);
        return new RunningApplicationCandidate(
            $"{ManualExecutableKeyPrefix}{executablePath}",
            displayName,
            Process: null,
            Emulator: null,
            [executablePath],
            SearchText: $"{displayName} {executablePath} file exe");
    }

    private RunningApplicationRow? FindRunningApplicationRowByExecutablePath(string executablePath) =>
        _runningApplicationRows.FirstOrDefault(row =>
            GetCandidateIdentityExecutablePaths(row.Candidate)
                .Any(path => string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase)));

    private void SelectRunningApplicationRow(string executablePath)
    {
        _runningApplicationGrid.ClearSelection();
        foreach (DataGridViewRow gridRow in _runningApplicationGrid.Rows)
        {
            if (gridRow.DataBoundItem is not RunningApplicationRow row ||
                !GetCandidateIdentityExecutablePaths(row.Candidate)
                    .Any(path => string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            gridRow.Selected = true;
            _runningApplicationGrid.CurrentCell = gridRow.Cells[Math.Min(1, gridRow.Cells.Count - 1)];
            return;
        }
    }

    private bool IsImportedCandidate(RunningApplicationCandidate candidate)
    {
        if (candidate.Emulator is { } emulator)
        {
            return _existingRules.Any(rule =>
                rule.TargetType == ApplicationTargetType.Emulator &&
                rule.EmulatorKind == emulator.EmulatorKind &&
                string.Equals(rule.EmulatorInstanceKey, emulator.EmulatorInstanceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rule.RuntimeProcessName, emulator.ProcessName, StringComparison.OrdinalIgnoreCase));
        }

        return GetCandidateIdentityExecutablePaths(candidate)
            .Any(IsImportedExecutablePath);
    }

    private bool IsCurrentCandidate(RunningApplicationCandidate candidate)
    {
        if (_currentRule is null)
        {
            return false;
        }

        if (candidate.Emulator is { } emulator)
        {
            return _currentRule.TargetType == ApplicationTargetType.Emulator &&
                _currentRule.EmulatorKind == emulator.EmulatorKind &&
                string.Equals(_currentRule.EmulatorInstanceKey, emulator.EmulatorInstanceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_currentRule.RuntimeProcessName, emulator.ProcessName, StringComparison.OrdinalIgnoreCase);
        }

        return _currentRule.TargetType == ApplicationTargetType.Executable &&
            TryGetExecutableRulePath(_currentRule, out var executablePath) &&
            GetCandidateIdentityExecutablePaths(candidate)
                .Any(path => string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsImportedExecutablePath(string executablePath) =>
        _importedExecutablePaths.Contains(executablePath);

    private static IReadOnlyList<string> GetCandidateIdentityExecutablePaths(RunningApplicationCandidate candidate)
    {
        var paths = new List<string>();
        if (candidate.Process?.ExecutablePath is { } processPath &&
            !string.IsNullOrWhiteSpace(processPath))
        {
            paths.Add(processPath);
        }
        else if (!candidate.IsEmulator)
        {
            paths.AddRange(candidate.TargetExecutablePaths
                .Where(path => !string.IsNullOrWhiteSpace(path)));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SelectOnlyIfNeeded(RunningApplicationRow selectedRow)
    {
        if (_mode == ApplicationSelectionMode.UpdateSingle)
        {
            foreach (var row in _runningApplicationRows)
            {
                row.IsSelected = ReferenceEquals(row, selectedRow);
            }

            return;
        }

        selectedRow.IsSelected = true;
    }

    private void NormalizeSingleSelection()
    {
        if (_mode != ApplicationSelectionMode.UpdateSingle)
        {
            return;
        }

        var selectedRows = _runningApplicationRows
            .Where(row => row.IsSelected && !row.IsImported)
            .ToList();
        var keep = selectedRows.FirstOrDefault(row => IsCurrentCandidate(row.Candidate)) ??
            selectedRows.FirstOrDefault();
        foreach (var row in _runningApplicationRows)
        {
            row.IsSelected = ReferenceEquals(row, keep);
        }
    }

    private static Button CreatePlainButton(string text, EventHandler onClick, Size size)
    {
        var button = new OutlineButton
        {
            Text = text,
            Name = "RunningApplicationBrowseButton",
            Size = size,
            MinimumSize = size,
            AutoSize = false,
            BackColor = Color.White,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Tag = LightTheme.SkipButtonThemeTag
        };
        button.Click += onClick;
        return button;
    }

    private static Button CreateIconButton(Image icon, EventHandler onClick)
    {
        var button = new Button
        {
            Size = new Size(RefreshButtonSize, RefreshButtonSize),
            Name = "RunningApplicationRefreshButton",
            MinimumSize = new Size(RefreshButtonSize, RefreshButtonSize),
            AutoSize = false,
            Image = icon,
            ImageAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(243, 243, 243),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            TabStop = false,
            Tag = LightTheme.SkipButtonThemeTag
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 235, 235);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 224, 224);
        button.Click += onClick;
        return button;
    }

    private static void RestoreCustomButtonStyles(Control control)
    {
        if (control is Button button)
        {
            switch (button.Name)
            {
                case "RunningApplicationBrowseButton":
                    button.BackColor = Color.White;
                    button.ForeColor = TextColor;
                    break;
                case "RunningApplicationRefreshButton":
                    button.BackColor = Color.FromArgb(243, 243, 243);
                    button.ForeColor = TextColor;
                    button.FlatAppearance.BorderColor = button.BackColor;
                    break;
            }
        }

        foreach (Control child in control.Controls)
        {
            RestoreCustomButtonStyles(child);
        }
    }

    private sealed class DropZoneTextBlock : Control
    {
        private const int MinimumLineHeight = 20;
        private const int LineGap = 2;
        private static readonly string[] TitleSegments = ["Kéo thả file ", ".exe", " hoặc ", "Shortcut", " vào đây"];

        public DropZoneTextBlock()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public string Subtitle { get; set; } = string.Empty;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            using var boldFont = new Font(Font, FontStyle.Bold);
            var lineHeight = GetLineHeight(Font, boldFont);
            var totalHeight = (lineHeight * 2) + LineGap;
            var titleY = ClientRectangle.Top + Math.Max(0, (ClientRectangle.Height - totalHeight) / 2);
            var subtitleY = titleY + lineHeight + LineGap;
            DrawTitle(e.Graphics, boldFont, titleY, lineHeight);
            TextRenderer.DrawText(
                e.Graphics,
                Subtitle,
                Font,
                new Rectangle(ClientRectangle.Left, subtitleY, Math.Max(1, ClientRectangle.Width), lineHeight),
                MutedTextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static int GetLineHeight(Font regularFont, Font boldFont) =>
            Math.Max(
                MinimumLineHeight,
                Math.Max(regularFont.Height, boldFont.Height) + 3);

        private void DrawTitle(Graphics graphics, Font boldFont, int y, int lineHeight)
        {
            var x = ClientRectangle.Left;
            for (var i = 0; i < TitleSegments.Length; i++)
            {
                var font = i is 1 or 3 ? boldFont : Font;
                var segment = TitleSegments[i];
                var size = TextRenderer.MeasureText(
                    graphics,
                    segment,
                    font,
                    Size.Empty,
                    TextFormatFlags.NoPadding);
                TextRenderer.DrawText(
                    graphics,
                    segment,
                    font,
                    new Rectangle(x, y, size.Width, lineHeight),
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                x += size.Width;
            }
        }
    }

    private sealed class OutlineButton : Button
    {
        private const int ButtonHeight = 32;
        private static readonly Color BorderColor = Color.FromArgb(23, 23, 23);
        private static readonly Color ButtonTextColor = Color.FromArgb(36, 36, 36);

        private bool _isHovering;
        private bool _isPressed;

        public OutlineButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Padding = new Padding(10, 2, 10, 2);
            TextAlign = ContentAlignment.MiddleCenter;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(
                Math.Max(MinimumSize.Width, Padding.Horizontal + textSize.Width + 12),
                Math.Max(ButtonHeight, MinimumSize.Height));
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            var preferred = GetPreferredSize(Size.Empty);
            base.SetBoundsCore(x, y, Math.Max(width, preferred.Width), Math.Max(height, preferred.Height), specified);
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
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var backColor = _isPressed
                ? Color.FromArgb(238, 238, 238)
                : _isHovering
                    ? Color.FromArgb(245, 245, 245)
                    : Color.White;
            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var background = new SolidBrush(backColor);
            using var border = new Pen(BorderColor, 1);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
            var textBounds = new Rectangle(
                bounds.Left + Padding.Left,
                bounds.Top + Padding.Top,
                Math.Max(1, bounds.Width - Padding.Horizontal),
                Math.Max(1, bounds.Height - Padding.Vertical));
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ButtonTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class DashedDropZonePanel : TableLayoutPanel
    {
        public DashedDropZonePanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var pen = new Pen(AccentBlue, 1)
            {
                DashStyle = DashStyle.Dot
            };
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class RunningApplicationRow
    {
        public RunningApplicationRow(
            RunningApplicationCandidate candidate,
            Image icon,
            bool isSelected,
            bool isImported,
            bool isNew)
        {
            Candidate = candidate;
            Icon = icon;
            IsSelected = isSelected;
            IsImported = isImported;
            IsNew = isNew;
        }

        public RunningApplicationCandidate Candidate { get; }
        public Image Icon { get; }
        public bool IsSelected { get; set; }
        public bool IsImported { get; set; }
        public bool IsNew { get; }
        public string DisplayName => Candidate.DisplayName;
    }
}
