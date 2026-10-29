using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using SpaceIsland.Models;
using SpaceIsland.Services;

namespace SpaceIsland.Views;

public partial class MainWindow : Window
{
    private readonly List<CleanItem> _items = CleanCatalog.Create();
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();

        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null) VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

        RestoreWindowPlacement();

        SafeList.ItemsSource = _items.Where(i => i.Risk == RiskLevel.Safe).ToList();
        CautionList.ItemsSource = _items.Where(i => i.Risk == RiskLevel.Caution).ToList();
        InfoList.ItemsSource = _items.Where(i => i.Risk == RiskLevel.Info).ToList();

        foreach (CleanItem item in _items) item.PropertyChanged += (_, _) => UpdateTotals();

        if (!Cleaner.IsElevated)
            AdminHint.Text = "非管理员：Windows 目录会跳过";

        RefreshDisk();
        UpdateTotals();

        Loaded += async (_, _) =>
        {
            if (App.Settings.WindowWidth > 600) Width = App.Settings.WindowWidth;
            if (App.Settings.WindowHeight > 500) Height = App.Settings.WindowHeight;
            await ScanAsync();
        };
    }

    // ------------------------------------------------------------------ 扫描

    private async Task ScanAsync()
    {
        if (_busy) return;
        _busy = true;
        CleanButton.IsEnabled = false;

        foreach (CleanItem item in _items)
        {
            item.Size = 0;
            item.FileCount = 0;
            item.Scanned = false;
        }

        UpdateTotals();

        try
        {
            foreach (CleanItem item in _items)
            {
                if (item.IsInfo)
                {
                    item.Scanned = true;
                    continue;
                }

                ScanStateText.Text = $"正在扫描 {item.Title}…";
                CleanItem captured = item;
                ScanResult result = await Task.Run(() => Scanner.Measure(captured, CancellationToken.None));

                item.Size = result.Size;
                item.FileCount = result.Files;
                item.Exists = result.Exists;
                item.Scanned = true;
                UpdateTotals();
            }

            ScanStateText.Text = $"上次扫描 {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            App.Log("scan: " + ex);
            ScanStateText.Text = "扫描中断";
        }
        finally
        {
            _busy = false;
            RefreshDisk();
            UpdateTotals();
        }
    }

    // ------------------------------------------------------------------ 清理

    private async void OnClean(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        List<CleanItem> selected = _items
            .Where(i => i.Selected && !i.IsInfo && i.Size > 0)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "没有勾选需要清理的内容。", "空间岛",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = new ConfirmWindow(selected) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        _busy = true;
        CleanButton.IsEnabled = false;
        ScanStateText.Text = "正在清理…";

        long freed = 0;
        int deleted = 0;
        int skipped = 0;
        var failures = new List<string>();

        foreach (CleanItem item in selected)
        {
            ScanStateText.Text = $"正在清理 {item.Title}…";

            CleanItem captured = item;
            var progress = new Progress<string>(text => ScanStateText.Text = text);

            CleanOutcome outcome = await Task.Run(() =>
                Cleaner.Clean(captured, progress, CancellationToken.None));

            freed += outcome.FreedBytes;
            deleted += outcome.DeletedFiles;
            skipped += outcome.Skipped;
            failures.AddRange(outcome.Failures);
        }

        _busy = false;

        // 清理完重新扫一遍，用真实结果刷新界面
        await ScanAsync();

        string message = $"已释放 {CleanItem.Format(freed)}，删除 {deleted} 个文件。";
        if (skipped > 0) message += $"\n{skipped} 个文件正在使用中，已跳过（关掉对应程序后再试一次即可）。";
        if (failures.Count > 0)
        {
            message += $"\n\n{failures.Count} 项没能完成：";
            foreach (string failure in failures.Take(4)) message += $"\n· {failure}";
        }

        App.Log($"clean: freed={freed} deleted={deleted} skipped={skipped} failures={failures.Count}");

        MessageBox.Show(this, message, "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnRescan(object sender, RoutedEventArgs e) => _ = ScanAsync();

    // ------------------------------------------------------------------ 其它

    private void RefreshDisk()
    {
        DiskStat disk = DiskInfo.ReadSystemDrive();

        DiskHeadline.Text = disk.Headline;
        DiskFreeText.Text = disk.FreeHint;
        DiskPercentText.Text = $"已用 {disk.UsedPercent:0}%";

        DiskBar.Value = disk.BarPercent;
        DiskBar.Foreground = disk.UsedPercent >= 90
            ? (System.Windows.Media.Brush)FindResource("Brush.Danger")
            : disk.UsedPercent >= 75
                ? (System.Windows.Media.Brush)FindResource("Brush.Warn")
                : (System.Windows.Media.Brush)FindResource("Brush.Accent");
    }

    private void UpdateTotals()
    {
        long selected = _items.Where(i => i.Selected && !i.IsInfo).Sum(i => i.Size);
        long safe = _items.Where(i => i.Risk == RiskLevel.Safe).Sum(i => i.Size);
        long caution = _items.Where(i => i.Risk == RiskLevel.Caution && i.HasContent).Sum(i => i.Size);

        TotalText.Text = CleanItem.Format(selected);
        SafeSubtotal.Text = safe > 0 ? CleanItem.Format(safe) : string.Empty;
        CautionSubtotal.Text = caution > 0 ? CleanItem.Format(caution) : string.Empty;

        CleanButton.Content = selected > 0
            ? (IsAdminNeeded() ? $"清理 {CleanItem.Format(selected)}" : $"清理 {CleanItem.Format(selected)}")
            : "开始清理";
        CleanButton.IsEnabled = selected > 0 && !_busy;
    }

    private bool IsAdminNeeded() => !Cleaner.IsElevated &&
        _items.Any(i => i.Selected && i.NeedsAdmin && i.Size > 0);

    private void OnOpenItemFolder(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CleanItem item) return;

        string? path = item.Roots.FirstOrDefault(Directory.Exists)
                       ?? item.Roots.FirstOrDefault(File.Exists);

        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show(this, "这个目录在本机不存在。", "空间岛",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.Log("open folder: " + ex);
        }
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        App.Settings.DarkTheme = !App.Settings.DarkTheme;
        ThemeManager.Apply(App.Settings.DarkTheme);
        App.Settings.Save();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { /* 忽略 */ }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void RestoreWindowPlacement()
    {
        AppSettings s = App.Settings;
        if (s.WindowLeft is double left && s.WindowTop is double top
            && left > -2000 && top > -2000 && left < 8000 && top < 8000)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        AppSettings s = App.Settings;
        s.WindowWidth = Width;
        s.WindowHeight = Height;
        s.WindowLeft = Left;
        s.WindowTop = Top;
        s.Save();
        base.OnClosed(e);
    }
}
