using System.IO;

namespace SpaceIsland.Services;

/// <summary>
/// 删除前的最后一道闸门。任何进入 <see cref="Cleaner"/> 的路径都要先过这里，
/// 防止配置写错或路径拼接出错时误删系统目录。
/// </summary>
public static class PathGuard
{
    private static readonly HashSet<string> ProtectedDirectories = BuildProtected();

    private static HashSet<string> BuildProtected()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string normalized = Normalize(path);
            if (normalized.Length > 0) set.Add(normalized);
        }

        // 所有固定磁盘的根
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is DriveType.Fixed or DriveType.Removable) Add(drive.RootDirectory.FullName);
            }
            catch
            {
                // 光驱未就绪等情况忽略
            }
        }

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Add(windows);
        Add(Path.Combine(windows, "System32"));
        Add(Path.Combine(windows, "SysWOW64"));
        Add(Path.Combine(windows, "WinSxS"));
        Add(Path.Combine(windows, "Fonts"));
        Add(Path.Combine(windows, "assembly"));
        Add(programFiles);
        Add(programFilesX86);
        Add(programData);
        Add(userProfile);
        Add(Path.GetDirectoryName(userProfile));

        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
                     Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Favorites,
                     Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.Programs
                 })
        {
            Add(Environment.GetFolderPath(folder));
        }

        return set;
    }

    /// <summary>统一成不带结尾分隔符的绝对路径；失败返回空串。</summary>
    public static string Normalize(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            return full.Length > 3 ? full.TrimEnd(Path.DirectorySeparatorChar) : full;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>是不是绝对不该被当成清理根目录的位置（盘符根、Windows、用户目录本身等）。</summary>
    public static bool IsProtectedDirectory(string path)
    {
        string normalized = Normalize(path);
        return normalized.Length == 0 || ProtectedDirectories.Contains(normalized);
    }

    /// <summary>candidate 是否落在 root 之内（含 root 本身）。</summary>
    public static bool IsUnder(string candidate, string root)
    {
        string c = Normalize(candidate);
        string r = Normalize(root);
        if (c.Length == 0 || r.Length == 0) return false;
        if (c.Equals(r, StringComparison.OrdinalIgnoreCase)) return true;
        return c.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>路径本身或任意一级父目录是符号链接/联接点时，不再往里走。</summary>
    public static bool HasReparsePoint(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;

            var dir = new DirectoryInfo(path);
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
        }
        catch
        {
            // 权限不足按“不安全”处理
            return true;
        }

        return false;
    }

    /// <summary>路径是否看起来就不可信（过长、含 ..、非绝对路径）。</summary>
    public static bool LooksSuspicious(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (!Path.IsPathFullyQualified(path)) return true;
        if (path.Contains("..", StringComparison.Ordinal)) return true;
        return path.Length > 240;
    }
}
