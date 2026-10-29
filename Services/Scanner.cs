using System.IO;
using SpaceIsland.Models;

namespace SpaceIsland.Services;

public readonly record struct ScanResult(long Size, int Files, bool Exists);

/// <summary>只统计大小，不做任何改动。所有异常都当成“读不到”处理，不让个别目录把整次扫描拖垮。</summary>
public static class Scanner
{
    public static ScanResult Measure(CleanItem item, CancellationToken token)
    {
        long size = 0;
        int files = 0;
        bool exists = false;

        try
        {
            foreach (string root in item.Roots)
            {
                token.ThrowIfCancellationRequested();

                if (item.Method == CleanMethod.Glob)
                {
                    MeasureGlob(root, ref size, ref files, ref exists);
                    continue;
                }

                if (File.Exists(root) && !Directory.Exists(root))
                {
                    try
                    {
                        size += new FileInfo(root).Length;
                        files++;
                        exists = true;
                    }
                    catch
                    {
                        // 单个文件读不到就算了
                    }
                    continue;
                }

                if (!Directory.Exists(root)) continue;
                exists = true;
                MeasureDirectory(root, ref size, ref files, token);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 整体失败时保留已统计到的部分
        }

        return new ScanResult(size, files, exists);
    }

    private static void MeasureGlob(string pattern, ref long size, ref int files, ref bool exists)
    {
        try
        {
            string? dir = Path.GetDirectoryName(pattern);
            string filePattern = Path.GetFileName(pattern);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            foreach (string file in Directory.EnumerateFiles(dir, filePattern))
            {
                try
                {
                    size += new FileInfo(file).Length;
                    files++;
                    exists = true;
                }
                catch
                {
                    // 忽略被占用的条目
                }
            }
        }
        catch
        {
            // 目录不可读，跳过
        }
    }

    private static void MeasureDirectory(string root, ref long size, ref int files, CancellationToken token)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string current = stack.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (string file in entries)
            {
                try
                {
                    size += new FileInfo(file).Length;
                    files++;
                }
                catch
                {
                    // 被占用或权限不足
                }
            }

            try
            {
                foreach (string sub in Directory.EnumerateDirectories(current))
                {
                    // 联接点/符号链接不跟进去，避免把别处的文件算进来
                    try
                    {
                        var info = new DirectoryInfo(sub);
                        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    }
                    catch
                    {
                        continue;
                    }

                    stack.Push(sub);
                }
            }
            catch
            {
                // 子目录枚举失败，跳过
            }
        }
    }
}
