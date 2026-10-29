using System.Windows;
using SpaceIsland.Models;

namespace SpaceIsland.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(IReadOnlyList<CleanItem> items)
    {
        InitializeComponent();

        ItemList.ItemsSource = items;

        long total = items.Sum(i => i.Size);
        int cautionCount = items.Count(i => i.Risk == RiskLevel.Caution);

        SummaryText.Text = $"将删除 {items.Count} 项，合计 {CleanItem.Format(total)}";

        var notes = new List<string>();
        if (items.Any(i => i.Id == "recycle-bin"))
            notes.Add("回收站清空后无法还原。");
        if (cautionCount > 0)
            notes.Add($"其中 {cautionCount} 项（黄色圆点）属于下载缓存，清掉后下次要重新联网下载。");
        if (items.Any(i => i.Risk == RiskLevel.Safe && i.Id != "recycle-bin"))
            notes.Add("其余都是能自动重建的临时文件，删除不会影响已安装的程序。");

        NoteText.Text = string.Join(" ", notes);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
