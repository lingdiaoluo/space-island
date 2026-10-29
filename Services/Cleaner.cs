using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SpaceIsland.Models;

namespace SpaceIsland.Services;

public sealed record CleanOutcome(long FreedBytes, int DeletedFiles, int Skipped, IReadOnlyList<string> Failures)
{
    public string Summary
    {
        get
        {
            string text = $"释放 {CleanItem.Format(FreedBytes)}，删除 {DeletedFiles} 个文件";
            if (Skipped > 0) text += $"，{Skipped} 个被占用已跳过";
            return text;
        }
    }
}

/// <summary>
/// 真正动文件的地方。每条路径都要过 <see cref="PathGuard"/>，
/// 遇到联接点、系统保护目录、root 之外的东西一律不动。
/// </summary>
public static class Cleaner
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public static CleanOutcome Clean(CleanItem item, IProgress<string>? progress, CancellationToken token)
    {
        // 只展示的项永远不执行清理，这是双保险
        if (item.Risk == RiskLevel.Info)
            return new CleanOutcome(0, 0, 0, new[] { "该项仅供查看" });

        return item.Method switch
        {
            CleanMethod.RecycleBin => EmptyRecycleBin(),
            CleanMethod.Glob => CleanGlobs(item, progress, token),
            _ => CleanRoots(item, progress, token)
        };
    }

    private static CleanOutcome CleanRoots(CleanItem item, IProgress<string>? progress, CancellationToken token)
    {
        long freed = 0;
        int deleted = 0;
        int skipped = 0;
        var failures = new List<string>();

        foreach (string root in item.Roots)
        {
            token.ThrowIfCancellationRequested();

            if (PathGuard.LooksSuspicious(root))
            {
                failures.Add($"{root}：路径不可信，已跳过");
                continue;
            }

            // 清理根目录本身不能是关键系统目录
            if (PathGuard.IsProtectedDirectory(root))
            {
                failures.Add($"{root}：属于受保护目录，已跳过");
                continue;
            }

            try
            {
                if (File.Exists(root) && !Directory.Exists(root))
                {
                    if (DeleteFile(root, ref freed, ref skipped)) deleted++;
                    continue;
                }

                if (!Directory.Exists(root)) continue;

                progress?.Report($"正在清理 {item.Title}");
                DeleteDirectoryContents(root, root, ref freed, ref deleted, ref skipped, failures, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{root}：{ex.Message}");
            }
        }

        return new CleanOutcome(freed, deleted, skipped, failures);
    }

    private static void DeleteDirectoryContents(string directory, string root,
        ref long freed, ref int deleted, ref int skipped, List<string> failures, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            skipped++;
            return;
        }

        foreach (string file in files)
        {
            token.ThrowIfCancellationRequested();

            // 只删 root 之内的东西
            if (!PathGuard.IsUnder(file, root))
            {
                skipped++;
                continue;
            }

            if (DeleteFile(file, ref freed, ref skipped)) deleted++;
        }

        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(directory);
        }
        catch
        {
            return;
        }

        foreach (string sub in subdirs)
        {
            token.ThrowIfCancellationRequested();

            if (!PathGuard.IsUnder(sub, root)) continue;
            if (PathGuard.HasReparsePoint(sub)) continue;   // 不跨链接删东西

            DeleteDirectoryContents(sub, root, ref freed, ref deleted, ref skipped, failures, token);

            try
            {
                // 只删已经空了的目录
                if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub, false);
            }
            catch
            {
                // 还被占用就留着
            }
        }
    }

    private static bool DeleteFile(string path, ref long freed, ref int skipped)
    {
        try
        {
            long length = 0;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch
            {
                // 拿不到大小也照样删
            }

            var info = new FileInfo(path);
            if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                skipped++;
                return false;   // 符号链接本身也不动，避免歧义
            }

            if (info.Exists && (info.Attributes & FileAttributes.ReadOnly) != 0)
            {
                info.Attributes &= ~FileAttributes.ReadOnly;
            }

            File.Delete(path);
            freed += length;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            skipped++;
            return false;
        }
        catch (IOException)
        {
            skipped++;    // 文件被占用，跳过
            return false;
        }
        catch
        {
            skipped++;
            return false;
        }
    }

    private static CleanOutcome CleanGlobs(CleanItem item, IProgress<string>? progress, CancellationToken token)
    {
        long freed = 0;
        int deleted = 0;
        int skipped = 0;

        foreach (string pattern in item.Roots)
        {
            token.ThrowIfCancellationRequested();

            string? dir = Path.GetDirectoryName(pattern);
            string filePattern = Path.GetFileName(pattern);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

            IEnumerable<string> matches;
            try
            {
                matches = Directory.EnumerateFiles(dir, filePattern).ToList();
            }
            catch
            {
                continue;
            }

            foreach (string file in matches)
            {
                if (!PathGuard.IsUnder(file, dir)) continue;
                if (DeleteFile(file, ref freed, ref skipped)) deleted++;
            }
        }

        progress?.Report($"正在清理 {item.Title}");
        return new CleanOutcome(freed, deleted, skipped, Array.Empty<string>());
    }

    private static CleanOutcome EmptyRecycleBin()
    {
        // 先量一下清空前的体积，用于报告释放量
        long before = 0;
        foreach (string root in DriveInfo.GetDrives().Select(d => d.RootDirectory.FullName))
        {
            string bin = Path.Combine(root, "$Recycle.Bin");
            if (!Directory.Exists(bin)) continue;
            try
            {
                before += MeasureNoThrow(bin);
            }
            catch
            {
                // 忽略
            }
        }

        int hr;
        try
        {
            hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }
        catch
        {
            return new CleanOutcome(0, 0, 0, new[] { "清空回收站失败" });
        }

        if (hr != 0)
            return new CleanOutcome(0, 0, 0, new[] { $"清空回收站失败（0x{hr:X8}）" });

        return new CleanOutcome(before, 0, 0, Array.Empty<string>());
    }

    private static long MeasureNoThrow(string root)
    {
        long total = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string current = stack.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }

            try
            {
                foreach (string sub in Directory.EnumerateDirectories(current))
                {
                    try
                    {
                        if (new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    }
                    catch { continue; }
                    stack.Push(sub);
                }
            }
            catch { }
        }

        return total;
    }
}
