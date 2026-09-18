using NetAgent.ProxyManager.App.Controls.Primitives;

namespace NetAgent.ProxyManager.App.Theme;

internal static class UiIcons
{
    public static Bitmap Add => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 2);
        g.DrawLine(pen, 8, 3, 8, 13);
        g.DrawLine(pen, 3, 8, 13, 8);
    }, LightTheme.Success);

    public static Bitmap Import => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 2);
        g.DrawLine(pen, 8, 3, 8, 11);
        g.DrawLine(pen, 5, 8, 8, 11);
        g.DrawLine(pen, 11, 8, 8, 11);
        g.DrawLine(pen, 3, 13, 13, 13);
    }, LightTheme.Accent);

    public static Bitmap Key =>
        SidebarIconRenderer.LoadOriginal("Key.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawEllipse(pen, 2, 4, 6, 6);
            g.DrawLine(pen, 8, 7, 14, 7);
            g.DrawLine(pen, 11, 7, 11, 10);
            g.DrawLine(pen, 13, 7, 13, 9);
        }, LightTheme.Warning);

    public static Bitmap Coin =>
        SidebarIconRenderer.LoadOriginal("Coin.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 1.6f);
            using var brush = new SolidBrush(Color.White);
            g.DrawEllipse(pen, 3, 3, 10, 10);
            g.DrawEllipse(pen, 5, 5, 6, 6);
            g.FillEllipse(brush, 7, 7, 2, 2);
        }, LightTheme.Warning);

    public static Bitmap Refresh =>
        SidebarIconRenderer.LoadOriginal("proxy-xoay.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawArc(pen, 3, 3, 10, 10, 35, 270);
            g.DrawLine(pen, 11, 3, 14, 4);
            g.DrawLine(pen, 14, 4, 12, 7);
        }, LightTheme.Accent);

    public static Bitmap Edit =>
        SidebarIconRenderer.LoadOriginal("System update.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawLine(pen, 4, 12, 12, 4);
            g.DrawLine(pen, 10, 4, 12, 6);
            g.DrawLine(pen, 3, 13, 6, 13);
        }, LightTheme.Accent);

    public static Bitmap Delete =>
        SidebarIconRenderer.LoadOriginal("Remove.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawRectangle(pen, 5, 5, 6, 8);
            g.DrawLine(pen, 4, 4, 12, 4);
            g.DrawLine(pen, 6, 3, 10, 3);
        }, LightTheme.Danger);

    public static Bitmap Check =>
        SidebarIconRenderer.LoadOriginal("Check.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawLine(pen, 3, 8, 7, 12);
            g.DrawLine(pen, 7, 12, 13, 4);
        }, LightTheme.Success);

    public static Bitmap Copy => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 1.7f);
        g.DrawRectangle(pen, 5, 4, 8, 9);
        g.DrawRectangle(pen, 3, 2, 8, 9);
    }, LightTheme.Accent);

    public static Bitmap CopyGlyph =>
        SidebarIconRenderer.Load("new-design/Copy.svg", LightTheme.Accent) ??
        SidebarIconRenderer.Load("Copy.svg", LightTheme.Accent) ?? DrawTransparent(g =>
        {
            using var pen = CreatePen(LightTheme.Accent, 1.7f);
            g.DrawRectangle(pen, 5, 4, 8, 9);
            g.DrawRectangle(pen, 3, 2, 8, 9);
        });

    public static Bitmap SearchGlyph =>
        SidebarIconRenderer.Load("Search.svg", LightTheme.Accent) ?? DrawTransparent(g =>
        {
            using var pen = CreatePen(LightTheme.Accent, 2);
            g.DrawEllipse(pen, 3, 3, 7, 7);
            g.DrawLine(pen, 9, 9, 13, 13);
        });

    public static Bitmap UploadGlyph =>
        SidebarIconRenderer.Load("Upload.svg", LightTheme.Accent) ?? DrawTransparent(g =>
        {
            using var pen = CreatePen(LightTheme.Accent, 2);
            g.DrawLine(pen, 8, 3, 8, 11);
            g.DrawLine(pen, 5, 6, 8, 3);
            g.DrawLine(pen, 11, 6, 8, 3);
            g.DrawLine(pen, 3, 13, 13, 13);
        });

    public static Bitmap PenGlyph =>
        SidebarIconRenderer.Load("Pen.svg", LightTheme.Accent) ?? Edit;

    public static Bitmap TrashGlyph =>
        SidebarIconRenderer.Load("Trash.svg", LightTheme.Danger) ?? Delete;

    public static Bitmap PlayGlyph =>
        SidebarIconRenderer.LoadOriginal("Play.svg") ?? Play;

    public static Bitmap StopGlyph =>
        SidebarIconRenderer.LoadOriginal("Stop.svg") ?? Stop;

    public static Bitmap RotateProxyGlyph =>
        SidebarIconRenderer.LoadOriginal("proxy-xoay.svg") ?? Swap;

    public static Bitmap ChangeGlyph =>
        SidebarIconRenderer.LoadOriginal("Change.svg") ?? Swap;

    public static Bitmap ChangeInfoGlyph =>
        SidebarIconRenderer.LoadOriginal("System update.svg") ?? Edit;

    public static Bitmap RemoveGlyph =>
        SidebarIconRenderer.LoadOriginal("Remove.svg") ?? Delete;

    public static Bitmap NewAttachProxy =>
        SidebarIconRenderer.LoadOriginal("new-design/attach-proxy.svg", 16) ?? ChangeGlyph;

    public static Bitmap NewEditPath =>
        SidebarIconRenderer.LoadOriginal("new-design/edit-path.svg", 16) ?? Folder;

    public static Bitmap NewEdit =>
        SidebarIconRenderer.LoadOriginal("new-design/edit.svg", 16) ?? PenGlyph;

    public static Bitmap NewRemove =>
        SidebarIconRenderer.LoadOriginal("new-design/remove.svg", 16) ?? TrashGlyph;

    public static Bitmap NewStart =>
        SidebarIconRenderer.LoadOriginal("new-design/start.svg", 16) ?? PlayGlyph;

    public static Bitmap NewStop =>
        SidebarIconRenderer.LoadOriginal("new-design/stop.svg", 16) ?? StopGlyph;

    public static Bitmap NewUnlinkProxy =>
        SidebarIconRenderer.LoadOriginal("new-design/unlink-proxy.svg", 20) ?? RemoveGlyph;

    public static Bitmap NewViewProxy =>
        SidebarIconRenderer.LoadOriginal("new-design/view-proxy.svg", 20) ?? Show;

    public static Bitmap NewViewProxyHover =>
        SidebarIconRenderer.Load("new-design/view-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? NewViewProxy;

    public static Bitmap NewWallet =>
        SidebarIconRenderer.LoadOriginal("new-design/Wallet.svg", 16) ?? Coin;

    public static Bitmap NewWalletToolbar =>
        SidebarIconRenderer.Load("new-design/Wallet.svg", DesignedGridTheme.TextColor, 20) ?? NewWallet;

    public static Bitmap NewWalletToolbarHover =>
        SidebarIconRenderer.Load("new-design/Wallet.svg", DesignedGridTheme.AccentColor, 20) ?? NewWallet;

    public static Bitmap NewWalletToolbarDisabled =>
        SidebarIconRenderer.Load("new-design/Wallet.svg", Color.FromArgb(189, 189, 189), 20) ?? NewWallet;

    public static Bitmap NewLock =>
        SidebarIconRenderer.Load("new-design/lock.svg", Color.FromArgb(64, 64, 64), 16) ?? Key;

    public static Bitmap EditLoggedUserInfo =>
        SidebarIconRenderer.Load("new-design/edit-logger-user-infor.svg", Color.FromArgb(64, 64, 64), 20) ?? PenGlyph;

    public static Bitmap NewAddApplication =>
        SidebarIconRenderer.Load("new-design/add-new-data.svg", DesignedGridTheme.TextColor, 20) ?? Add;

    public static Bitmap NewAddApplicationHover =>
        SidebarIconRenderer.Load("new-design/add-new-data.svg", DesignedGridTheme.AccentColor, 20) ?? Add;

    public static Bitmap NewAddData =>
        SidebarIconRenderer.Load("new-design/add-new-data.svg", DesignedGridTheme.TextColor, 20) ?? Add;

    public static Bitmap NewAddDataHover =>
        SidebarIconRenderer.Load("new-design/add-new-data.svg", DesignedGridTheme.AccentColor, 20) ?? Add;

    public static Bitmap NewAddDataDisabled =>
        SidebarIconRenderer.Load("new-design/add-new-data.svg", Color.FromArgb(189, 189, 189), 20) ?? Add;

    public static Bitmap NewAddMyProxies =>
        SidebarIconRenderer.Load("new-design/add-my-proxies.svg", Color.White, 20) ?? Add;

    public static Bitmap NewAddMyProxiesHover =>
        SidebarIconRenderer.Load("new-design/add-my-proxies.svg", Color.White, 20) ?? Add;

    public static Bitmap NewAddMyProxiesDisabled =>
        SidebarIconRenderer.Load("new-design/add-my-proxies.svg", Color.FromArgb(189, 189, 189), 20) ?? Add;

    public static Bitmap NewAutoAssignProxy =>
        SidebarIconRenderer.Load("new-design/auto-assign-proxy.svg", DesignedGridTheme.TextColor, 20) ?? Swap;

    public static Bitmap NewAutoAssignProxyHover =>
        SidebarIconRenderer.Load("new-design/auto-assign-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? Swap;

    public static Bitmap NewReset =>
        SidebarIconRenderer.Load("new-design/reset.svg", DesignedGridTheme.TextColor, 20) ?? Refresh;

    public static Bitmap NewResetHover =>
        SidebarIconRenderer.Load("new-design/reset.svg", DesignedGridTheme.AccentColor, 20) ?? Refresh;

    public static Bitmap NewResetDisabled =>
        SidebarIconRenderer.Load("new-design/reset.svg", Color.FromArgb(189, 189, 189), 20) ?? Refresh;

    public static Bitmap NewRemoveAll =>
        SidebarIconRenderer.Load("new-design/remove-all.svg", DesignedGridTheme.TextColor, 20) ?? TrashGlyph;

    public static Bitmap NewRemoveAllHover =>
        SidebarIconRenderer.Load("new-design/remove-all.svg", DesignedGridTheme.AccentColor, 20) ?? TrashGlyph;

    public static Bitmap NewRemoveAllDisabled =>
        SidebarIconRenderer.Load("new-design/remove-all.svg", Color.FromArgb(189, 189, 189), 20) ?? TrashGlyph;

    public static Bitmap NewFunnelClear =>
        SidebarIconRenderer.Load("new-design/funnel-x.svg", Color.FromArgb(189, 189, 189), 20) ?? Clear;

    public static Bitmap NewFunnelClearHover =>
        SidebarIconRenderer.Load("new-design/funnel-x.svg", DesignedGridTheme.AccentColor, 20) ?? Clear;

    public static Bitmap NewUploadApplication =>
        SidebarIconRenderer.LoadOriginal("new-design/upload-application.svg", 24) ?? UploadGlyph;

    public static Bitmap NewRefreshApplication =>
        SidebarIconRenderer.LoadOriginal("new-design/refresh-application.svg", 20) ?? Refresh;

    public static Bitmap NewCopy =>
        SidebarIconRenderer.Load("new-design/Copy.svg", DesignedGridTheme.TextColor, 20) ?? CopyGlyph;

    public static Bitmap NewCopyHover =>
        SidebarIconRenderer.Load("new-design/Copy.svg", DesignedGridTheme.AccentColor, 20) ?? CopyGlyph;

    public static Bitmap NewCopyDisabled =>
        SidebarIconRenderer.Load("new-design/Copy.svg", Color.FromArgb(189, 189, 189), 20) ?? CopyGlyph;

    public static Bitmap NewInlineEdit =>
        SidebarIconRenderer.LoadOriginal("new-design/inline-edit-icon.svg", 18) ?? NewEdit;

    public static Bitmap NewInlineEditAction =>
        SidebarIconRenderer.Load("new-design/inline-edit-icon.svg", DesignedGridTheme.TextColor, 20) ?? NewInlineEdit;

    public static Bitmap NewInlineEditActionHover =>
        SidebarIconRenderer.Load("new-design/inline-edit-icon.svg", DesignedGridTheme.AccentColor, 20) ?? NewInlineEdit;

    public static Bitmap NewGetRotateLink =>
        SidebarIconRenderer.Load("new-design/get-rotate-link.svg", DesignedGridTheme.TextColor, 20) ?? Import;

    public static Bitmap NewGetRotateLinkHover =>
        SidebarIconRenderer.Load("new-design/get-rotate-link.svg", DesignedGridTheme.AccentColor, 20) ?? Import;

    public static Bitmap NewGetRotateLinkDisabled =>
        SidebarIconRenderer.Load("new-design/get-rotate-link.svg", Color.FromArgb(189, 189, 189), 20) ?? Import;

    public static Bitmap NewRefreshProxy =>
        SidebarIconRenderer.Load("new-design/refresh-proxy.svg", DesignedGridTheme.TextColor, 20) ?? NewReset;

    public static Bitmap NewRefreshProxyHover =>
        SidebarIconRenderer.Load("new-design/refresh-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? NewResetHover;

    public static Bitmap NewRefreshProxyDisabled =>
        SidebarIconRenderer.Load("new-design/refresh-proxy.svg", Color.FromArgb(189, 189, 189), 20) ?? NewReset;

    public static Bitmap NewCheckProxy =>
        SidebarIconRenderer.Load("new-design/check-proxy.svg", DesignedGridTheme.TextColor, 20) ?? Check;

    public static Bitmap NewCheckProxyHover =>
        SidebarIconRenderer.Load("new-design/check-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? Check;

    public static Bitmap NewCheckProxyDisabled =>
        SidebarIconRenderer.Load("new-design/check-proxy.svg", Color.FromArgb(189, 189, 189), 20) ?? Check;

    public static Bitmap NewChangeProxyInfo =>
        SidebarIconRenderer.Load("new-design/change-proxy-info.svg", DesignedGridTheme.TextColor, 20) ?? NewEdit;

    public static Bitmap NewChangeProxyInfoHover =>
        SidebarIconRenderer.Load("new-design/change-proxy-info.svg", DesignedGridTheme.AccentColor, 20) ?? NewEdit;

    public static Bitmap NewChangeProxyInfoDisabled =>
        SidebarIconRenderer.Load("new-design/change-proxy-info.svg", Color.FromArgb(189, 189, 189), 20) ?? NewEdit;

    public static Bitmap NewRenewProxy =>
        SidebarIconRenderer.Load("new-design/renew-proxy.svg", DesignedGridTheme.TextColor, 20) ?? Key;

    public static Bitmap NewRenewProxyHover =>
        SidebarIconRenderer.Load("new-design/renew-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? Key;

    public static Bitmap NewRenewProxyDisabled =>
        SidebarIconRenderer.Load("new-design/renew-proxy.svg", Color.FromArgb(189, 189, 189), 20) ?? Key;

    public static Bitmap NewRotateProxy =>
        SidebarIconRenderer.Load("new-design/rotate-proxy.svg", DesignedGridTheme.TextColor, 20) ?? RotateProxyGlyph;

    public static Bitmap NewRotateProxyHover =>
        SidebarIconRenderer.Load("new-design/rotate-proxy.svg", DesignedGridTheme.AccentColor, 20) ?? RotateProxyGlyph;

    public static Bitmap NewRotateProxyDisabled =>
        SidebarIconRenderer.Load("new-design/rotate-proxy.svg", Color.FromArgb(189, 189, 189), 20) ?? RotateProxyGlyph;

    public static Bitmap NewLoadingWarning =>
        SidebarIconRenderer.Load("new-design/Loading.svg", Color.FromArgb(236, 113, 0), 18) ?? Refresh;

    public static Bitmap Swap =>
        SidebarIconRenderer.LoadOriginal("Change.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawLine(pen, 3, 5, 12, 5);
            g.DrawLine(pen, 10, 3, 12, 5);
            g.DrawLine(pen, 10, 7, 12, 5);
            g.DrawLine(pen, 13, 11, 4, 11);
            g.DrawLine(pen, 6, 9, 4, 11);
            g.DrawLine(pen, 6, 13, 4, 11);
        }, LightTheme.Warning);

    public static Bitmap Clear =>
        SidebarIconRenderer.LoadOriginal("Remove.svg") ?? Draw(g =>
        {
            using var pen = CreatePen(Color.White, 2);
            g.DrawLine(pen, 4, 4, 12, 12);
            g.DrawLine(pen, 12, 4, 4, 12);
        }, LightTheme.Muted);

    public static Bitmap Play => Draw(g =>
    {
        using var brush = new SolidBrush(Color.White);
        g.FillPolygon(brush, new[] { new Point(5, 3), new Point(13, 8), new Point(5, 13) });
    }, LightTheme.Success);

    public static Bitmap Stop => Draw(g =>
    {
        using var brush = new SolidBrush(Color.White);
        g.FillRectangle(brush, 4, 4, 8, 8);
    }, LightTheme.Danger);

    public static Bitmap Folder => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 2);
        g.DrawRectangle(pen, 2, 5, 12, 8);
        g.DrawLine(pen, 2, 5, 6, 5);
        g.DrawLine(pen, 6, 5, 7, 3);
        g.DrawLine(pen, 7, 3, 14, 3);
    }, LightTheme.Warning);

    public static Bitmap Search => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 2);
        g.DrawEllipse(pen, 3, 3, 7, 7);
        g.DrawLine(pen, 9, 9, 13, 13);
    }, LightTheme.Accent);

    public static Bitmap Eye => DrawTransparent(g =>
    {
        using var pen = CreatePen(LightTheme.Foreground, 1.6f);
        g.DrawArc(pen, 2, 4, 12, 8, 0, 180);
        g.DrawArc(pen, 2, 4, 12, 8, 180, 180);
        using var brush = new SolidBrush(LightTheme.Foreground);
        g.FillEllipse(brush, 6, 6, 4, 4);
    });

    public static Bitmap Show => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 1.6f);
        g.DrawArc(pen, 2, 4, 12, 8, 0, 180);
        g.DrawArc(pen, 2, 4, 12, 8, 180, 180);
        using var brush = new SolidBrush(Color.White);
        g.FillEllipse(brush, 6, 6, 4, 4);
    }, LightTheme.Accent);

    public static Bitmap Hide => Draw(g =>
    {
        using var pen = CreatePen(Color.White, 1.6f);
        g.DrawArc(pen, 2, 4, 12, 8, 0, 180);
        g.DrawArc(pen, 2, 4, 12, 8, 180, 180);
        g.DrawLine(pen, 3, 13, 13, 3);
    }, LightTheme.Muted);

    public static Bitmap EyeOff => DrawTransparent(g =>
    {
        using var pen = CreatePen(LightTheme.Muted, 1.6f);
        g.DrawArc(pen, 2, 4, 12, 8, 0, 180);
        g.DrawArc(pen, 2, 4, 12, 8, 180, 180);
        g.DrawLine(pen, 3, 13, 13, 3);
    });

    public static Bitmap PasswordView =>
        SidebarIconRenderer.LoadOriginal("new-design/show-password.svg", 20) ??
        SidebarIconRenderer.Load("View.svg", LightTheme.Foreground, 20) ??
        Eye;

    public static Bitmap PasswordHide =>
        SidebarIconRenderer.LoadOriginal("new-design/hide-password.svg", 20) ??
        SidebarIconRenderer.Load("Hide.svg", LightTheme.Foreground, 20) ??
        EyeOff;

    private static Bitmap Draw(Action<Graphics> draw, Color background)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(background);
        graphics.FillEllipse(brush, 0, 0, 15, 15);
        draw(graphics);
        return bitmap;
    }

    private static Bitmap DrawTransparent(Action<Graphics> draw)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        draw(graphics);
        return bitmap;
    }

    private static Pen CreatePen(Color color, float width) => new(color, width)
    {
        StartCap = System.Drawing.Drawing2D.LineCap.Round,
        EndCap = System.Drawing.Drawing2D.LineCap.Round
    };
}
