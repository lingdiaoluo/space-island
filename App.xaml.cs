using System.IO;
using System.Windows;
using SpaceIsland.Services;
using SpaceIsland.Views;

namespace SpaceIsland;

public partial class App : Application
{
    private Mutex? _mutex;

    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "SpaceIsland.SingleInstance.v1", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) => Log(args.Exception.ToString());

        Settings = AppSettings.Load();
        ThemeManager.Apply(Settings.DarkTheme);

        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Settings.Save();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    internal static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Folder);
            File.AppendAllText(Path.Combine(AppSettings.Folder, "app.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}");
        }
        catch
        {
            // 日志写不了也不能影响主流程
        }
    }
}

public static class ThemeManager
{
    public static void Apply(bool dark)
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative)
        };

        ResourceDictionary resources = Application.Current.Resources;
        if (resources.MergedDictionaries.Count > 0)
            resources.MergedDictionaries[0] = dictionary;
        else
            resources.MergedDictionaries.Add(dictionary);
    }
}
