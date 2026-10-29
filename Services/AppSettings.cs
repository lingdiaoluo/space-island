using System.IO;
using System.Text.Json;

namespace SpaceIsland.Services;

/// <summary>只存几项界面偏好，不记录任何扫描内容。</summary>
public sealed class AppSettings
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceIsland");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    public bool DarkTheme { get; set; }
    public double WindowWidth { get; set; } = 880;
    public double WindowHeight { get; set; } = 800;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                AppSettings? parsed = JsonSerializer.Deserialize<AppSettings>(json);
                if (parsed != null) return parsed;
            }
        }
        catch
        {
            // 配置坏了就用默认值，不打断启动
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 存不下就算了，不影响主流程
        }
    }
}
