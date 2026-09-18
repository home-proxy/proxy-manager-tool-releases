using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Models;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ProxyRestartConfirmationForm : Form
{
    private readonly CheckBox _doNotAskAgainCheckBox = new()
    {
        Text = "Không hỏi lại lần sau",
        AutoSize = true
    };

    public ProxyRestartConfirmationForm(IReadOnlyList<ApplicationRestartCandidate> applications)
    {
        Text = "Restart ứng dụng để gắn proxy";
        Width = 1080;
        Height = 820;
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(BuildLayout(applications));
        LightTheme.Apply(this);
    }

    public bool DoNotAskAgain => _doNotAskAgainCheckBox.Checked;

    private Control BuildLayout(IReadOnlyList<ApplicationRestartCandidate> applications)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Cần restart ứng dụng để gắn proxy",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = $"Profile mới đã được load vào Proxifier. Có {applications.Count} ứng dụng đang chạy cần restart để nhận cấu hình proxy mới.\n\nNếu tiếp tục, app chỉ đóng và mở lại các ứng dụng trong danh sách bên dưới. Các ứng dụng khác không bị ảnh hưởng.\n\nNếu bấm Hủy, profile vẫn giữ trong Proxifier nhưng các ứng dụng này có thể chưa dùng đúng IP mới.",
            AutoSize = true,
            MaximumSize = new Size(1080, 0),
            ForeColor = LightTheme.Foreground,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        root.Controls.Add(BuildApplicationGrid(applications), 0, 2);

        _doNotAskAgainCheckBox.Margin = new Padding(0, 12, 0, 0);
        root.Controls.Add(_doNotAskAgainCheckBox, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0)
        };
        var restartButton = LightTheme.CreateToolbarButton(
            "Restart ứng dụng",
            (_, _) => DialogResult = DialogResult.OK,
            LightTheme.Success,
            UiIcons.Play);
        var skipButton = LightTheme.CreateToolbarButton(
            "Hủy",
            (_, _) => DialogResult = DialogResult.Cancel,
            LightTheme.Muted);
        buttons.Controls.Add(restartButton);
        buttons.Controls.Add(skipButton);
        root.Controls.Add(buttons, 0, 4);

        AcceptButton = restartButton;
        CancelButton = skipButton;
        return root;
    }

    private static Control BuildApplicationGrid(IReadOnlyList<ApplicationRestartCandidate> applications)
    {
        var grid = new AppDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            DataSource = applications
                .Select(candidate => new ApplicationRestartRow(candidate.Icon, candidate.DisplayName))
                .ToList()
        };
        grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = string.Empty,
            DataPropertyName = nameof(ApplicationRestartRow.Icon),
            MinimumWidth = 46,
            Width = 46,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ImageLayout = DataGridViewImageCellLayout.Zoom
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Tên ứng dụng",
            DataPropertyName = nameof(ApplicationRestartRow.ApplicationName),
            MinimumWidth = 320,
            FillWeight = 100
        });
        return grid;
    }

    private sealed record ApplicationRestartRow(Image Icon, string ApplicationName);
}
