using System.IO;
using SpaceIsland.Models;

namespace SpaceIsland.Services;

public sealed class DiskStat
{
    public DiskStat(string name, long total, long free)
    {
        Name = name;
        Total = total;
        Free = free;
    }

    public string Name { get; }
    public long Total { get; }
    public long Free { get; }
    public long Used => Math.Max(0, Total - Free);

    public double UsedPercent => Total <= 0 ? 0 : Used * 100.0 / Total;

    /// <summary>进度条宽度百分比（0-100，留一点余量避免满格时圆角被切）。</summary>
    public double BarPercent => Math.Clamp(UsedPercent, 0, 100);

    public string UsedText => CleanItem.Format(Used);
    public string TotalText => CleanItem.Format(Total);
    public string FreeText => CleanItem.Format(Free);

    public string Headline => $"{UsedText} / {TotalText}";
    public string FreeHint => $"剩余 {FreeText}";
}

public static class DiskInfo
{
    public static DiskStat ReadSystemDrive()
    {
        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Read(Path.GetPathRoot(systemRoot) ?? "C:\\");
    }

    public static DiskStat Read(string root)
    {
        try
        {
            var drive = new DriveInfo(root);
            return new DiskStat(drive.Name.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch
        {
            return new DiskStat("C:", 0, 0);
        }
    }
}
