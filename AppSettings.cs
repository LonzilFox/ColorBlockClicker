using System.Text.Json;

namespace ColorBlockClicker;

internal sealed class AppSettings
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasRegion { get; set; }
    public int MaxGrid { get; set; } = 40;
    public bool UltraFast { get; set; } = true;

    public Rectangle Region => new(Left, Top, Width, Height);

    private static string SettingsPath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorBlockClicker");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath))
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Settings failure must never block the click engine.
        }
    }
}
