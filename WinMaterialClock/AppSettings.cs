using System.Globalization;

namespace WinMaterialClock;

/// <summary>
/// 零反射輕量級設定持久化管理類別，以 INI 純文字格式讀寫於 %LOCALAPPDATA%\WinMaterialClock\settings.ini，
/// 確保 100% Native AOT 相容性與零序列化額外記憶體開銷。
/// </summary>
public sealed class AppSettings
{
    private static readonly string s_settingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinMaterialClock");
    private static readonly string s_settingsFile =
        Path.Combine(s_settingsDir, "settings.ini");

    public ClockVisualStyle Style { get; set; } = ClockVisualStyle.PixelStackedCard;
    public float Scale { get; set; } = 1.0f;
    public float Opacity { get; set; } = 0.95f;
    public int WindowX { get; set; } = 120;
    public int WindowY { get; set; } = 120;
    public bool IsLockedClickThrough { get; set; }

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            if (!File.Exists(s_settingsFile))
                return settings;

            foreach (string rawLine in File.ReadAllLines(s_settingsFile))
            {
                ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
                if (line.IsEmpty || line[0] == '#')
                    continue;

                int eqIdx = line.IndexOf('=');
                if (eqIdx <= 0)
                    continue;

                ReadOnlySpan<char> key = line[..eqIdx].Trim();
                ReadOnlySpan<char> val = line[(eqIdx + 1)..].Trim();

                if (key.Equals("Style", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int styleInt) &&
                    styleInt is >= 0 and <= 4)
                {
                    settings.Style = (ClockVisualStyle)styleInt;
                }
                else if (key.Equals("Scale", StringComparison.OrdinalIgnoreCase) &&
                         float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                {
                    settings.Scale = Math.Clamp(scale, 0.5f, 1.8f);
                }
                else if (key.Equals("Opacity", StringComparison.OrdinalIgnoreCase) &&
                         float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float opacity))
                {
                    settings.Opacity = Math.Clamp(opacity, 0.15f, 1.0f);
                }
                else if (key.Equals("WindowX", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wx))
                {
                    settings.WindowX = wx;
                }
                else if (key.Equals("WindowY", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wy))
                {
                    settings.WindowY = wy;
                }
                else if (key.Equals("IsLockedClickThrough", StringComparison.OrdinalIgnoreCase) &&
                         bool.TryParse(val, out bool locked))
                {
                    settings.IsLockedClickThrough = locked;
                }
            }
        }
        catch
        {
            // Ignore corrupted file and use defaults
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(s_settingsDir);
            string content = string.Create(
                CultureInfo.InvariantCulture,
                $"""
                Style={(int)Style}
                Scale={Scale:F2}
                Opacity={Opacity:F2}
                WindowX={WindowX}
                WindowY={WindowY}
                IsLockedClickThrough={IsLockedClickThrough}
                """);
            File.WriteAllText(s_settingsFile, content);
        }
        catch
        {
            // Ignore write errors
        }
    }
}
