using System.IO;
using SpaceIsland.Models;

namespace SpaceIsland.Services;

/// <summary>
/// 清理项清单。原则：只收录“删掉会自动重建或系统本来就会丢弃”的内容，
/// 用户的文档、图片、下载文件一律不进这个表。
/// </summary>
public static class CleanCatalog
{
    public static List<CleanItem> Create()
    {
        string temp = Path.GetTempPath();
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string systemDrive = Path.GetPathRoot(windows) ?? "C:\\";

        var items = new List<CleanItem>();

        // ---------------- 可直接清理 ----------------

        items.Add(new CleanItem(
            "user-temp", "用户临时文件",
            "安装包解压残留、程序运行时写下的临时文件，删掉后用到时会自动重建。",
            RiskLevel.Safe, CleanMethod.Files, new[] { temp }));

        items.Add(new CleanItem(
            "win-temp", "Windows 临时文件",
            "系统级临时目录，同样会自动重建。需要管理员权限才能清干净。",
            RiskLevel.Safe, CleanMethod.Files, new[] { Path.Combine(windows, "Temp") }, needsAdmin: true));

        items.Add(new CleanItem(
            "win-update", "Windows 更新缓存",
            "已经装完的更新安装包副本，系统不会再需要它们。",
            RiskLevel.Safe, CleanMethod.Files,
            new[] { Path.Combine(windows, "SoftwareDistribution", "Download") }, needsAdmin: true));

        items.Add(new CleanItem(
            "delivery-opt", "传递优化缓存",
            "Windows 用来给其他电脑分发更新而缓存的分片。",
            RiskLevel.Safe, CleanMethod.Files,
            new[]
            {
                Path.Combine(windows, "ServiceProfiles", "NetworkService",
                    "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization")
            }, needsAdmin: true));

        items.Add(new CleanItem(
            "recycle-bin", "回收站",
            "已删除但还占着磁盘的文件。清空后无法再还原。",
            RiskLevel.Safe, CleanMethod.RecycleBin, new[] { Path.Combine(systemDrive, "$Recycle.Bin") }));

        items.Add(new CleanItem(
            "thumb-cache", "缩略图缓存",
            "资源管理器为图片和视频生成的预览小图，会自动重建。",
            RiskLevel.Safe, CleanMethod.Glob,
            new[] { Path.Combine(local, "Microsoft", "Windows", "Explorer", "thumbcache_*.db") }));

        items.Add(new CleanItem(
            "crash-dump", "崩溃转储文件",
            "蓝屏时写下的内存转储，只在排查故障时有用。",
            RiskLevel.Safe, CleanMethod.Files,
            new[]
            {
                Path.Combine(windows, "Minidump"),
                Path.Combine(windows, "MEMORY.DMP")
            }, needsAdmin: true));

        items.Add(new CleanItem(
            "error-report", "错误报告存档",
            "程序崩溃后打包上报给微软的存档，本机留着没有用途。",
            RiskLevel.Safe, CleanMethod.Files,
            new[]
            {
                Path.Combine(local, "Microsoft", "Windows", "WER"),
                Path.Combine(programData, "Microsoft", "Windows", "WER"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "Temp")
            }, needsAdmin: true));

        items.Add(new CleanItem(
            "cbs-log", "系统组件日志",
            "组件安装过程的 CBS 日志，日积月累能占掉好几个 GB。",
            RiskLevel.Safe, CleanMethod.Files,
            new[] { Path.Combine(windows, "Logs", "CBS") }, needsAdmin: true));

        items.Add(new CleanItem(
            "d3d-cache", "显卡着色器缓存",
            "游戏和图形程序编译好的着色器，删掉会重新生成，首次启动略慢。",
            RiskLevel.Safe, CleanMethod.Files,
            new[]
            {
                Path.Combine(local, "D3DSCache"),
                Path.Combine(local, "NVIDIA", "DXCache"),
                Path.Combine(local, "NVIDIA", "GLCache")
            }));

        var browserCaches = CollectBrowserCaches(local, roaming);
        if (browserCaches.Count > 0)
        {
            items.Add(new CleanItem(
                "browser-cache", "浏览器网页缓存",
                "Edge / Chrome 缓存的网页图片和脚本，清掉后网页首次加载会慢一点。",
                RiskLevel.Safe, CleanMethod.Files, browserCaches));
        }

        // ---------------- 需要确认 ----------------

        items.Add(new CleanItem(
            "pip-cache", "Python 包缓存",
            "pip 下载过的安装包。清掉后装同样的包需要重新下载。",
            RiskLevel.Caution, CleanMethod.Files,
            new[] { Path.Combine(local, "pip", "Cache") }));

        items.Add(new CleanItem(
            "npm-cache", "npm 缓存",
            "Node 依赖的下载缓存。清掉后 npm install 会变慢。",
            RiskLevel.Caution, CleanMethod.Files,
            new[] { Path.Combine(local, "npm-cache") }));

        items.Add(new CleanItem(
            "nuget-cache", "NuGet 包缓存",
            ".NET 项目的依赖包，清掉后还原项目要重新联网下载。",
            RiskLevel.Caution, CleanMethod.Files,
            new[] { Path.Combine(profile, ".nuget", "packages") }));

        // ---------------- 仅展示 ----------------

        items.Add(new CleanItem(
            "winsxs", "WinSxS 组件存储",
            "系统组件仓库，看着很大但大部分是硬链接。要用 DISM 分析后才能清理，不要手动删。",
            RiskLevel.Info, CleanMethod.Files, new[] { Path.Combine(windows, "WinSxS") }));

        items.Add(new CleanItem(
            "windows-old", "旧的 Windows 安装",
            "升级前的系统备份，用于十天内的回滚。要清它请在「设置 → 系统 → 存储」里操作。",
            RiskLevel.Info, CleanMethod.Files, new[] { Path.Combine(systemDrive, "Windows.old") }));

        return items;
    }

    /// <summary>把各浏览器配置目录下的缓存目录收集出来。</summary>
    private static List<string> CollectBrowserCaches(string local, string roaming)
    {
        var roots = new List<string>();
        var userDataDirs = new[]
        {
            Path.Combine(local, "Microsoft", "Edge", "User Data"),
            Path.Combine(local, "Google", "Chrome", "User Data"),
            Path.Combine(roaming, "Opera Software", "Opera Stable")
        };

        string[] cacheNames = { "Cache", "Code Cache", "GPUCache", "Service Worker\\CacheStorage" };

        foreach (string userData in userDataDirs)
        {
            try
            {
                if (!Directory.Exists(userData)) continue;

                foreach (string profile in Directory.EnumerateDirectories(userData))
                {
                    string name = Path.GetFileName(profile);
                    bool isProfile = name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                                     || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                                     || name.Equals("Opera Stable", StringComparison.OrdinalIgnoreCase);
                    if (!isProfile) continue;

                    foreach (string cacheName in cacheNames)
                    {
                        string path = Path.Combine(profile, cacheName);
                        if (Directory.Exists(path)) roots.Add(path);
                    }
                }
            }
            catch
            {
                // 浏览器没装或拒绝访问，跳过
            }
        }

        return roots;
    }
}
