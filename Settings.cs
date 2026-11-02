using System.Text.Json;

namespace TopAwake;

internal sealed class Settings
{
    public bool HotkeyEnabled { get; set; } = true;
    public string AwakeMode { get; set; } = "Off";
    public int TimedMinutes { get; set; } = 30;
    public bool KeepScreenOn { get; set; } = true;

    private static string DirPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopAwake");
    private static string FilePath => Path.Combine(DirPath, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
        }
        catch
        {
            // 配置损坏时回退到默认设置
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 设置保存失败不影响主功能
        }
    }
}
