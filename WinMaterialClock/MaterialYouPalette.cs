using Windows.UI;
using Windows.UI.ViewManagement;

namespace WinMaterialClock;

public enum ClockVisualStyle
{
    PixelStackedCard = 0,   // 經典 Pixel 2x2 + M3 圓角卡片底板
    TonalPills = 1,         // 雙色膠囊背景 + 2x2 數字
    PureFloatingText = 2,   // 無底板純懸浮 2x2 粗體數字
    CircularAnalog = 3,     // 圓形錶盤（含刻度與時、分、秒三根指針）
    FluentDesign = 4        // Windows 11 Fluent Design 分層毛玻璃高光邊框卡片
}

public readonly record struct MaterialPalette(
    Color HourDigits,
    Color MinuteDigits,
    Color SecondHandAccent,
    Color CardSurface,
    Color PillAccent,
    Color FluentInnerSurface,
    Color FluentBorderTop,
    Color FluentBorderBottom,
    bool IsDarkTheme);

internal static class MaterialYouPalette
{
    private static readonly UISettings s_uiSettings = new();

    public static MaterialPalette GenerateFromSystem()
    {
        Color bg = s_uiSettings.GetColorValue(UIColorType.Background);
        bool isDark = (bg.R * 299 + bg.G * 587 + bg.B * 114) < 128000;

        Color accent = s_uiSettings.GetColorValue(UIColorType.Accent);
        if (accent.A == 0 && NativeMethods.DwmGetColorizationColor(out uint dwmArgb, out _) == 0)
        {
            accent = Color.FromArgb(
                255,
                (byte)((dwmArgb >> 16) & 0xFF),
                (byte)((dwmArgb >> 8) & 0xFF),
                (byte)(dwmArgb & 0xFF));
        }

        RgbToHsl(accent, out float h, out float s, out _);
        float clampedSat = Math.Clamp(s, 0.48f, 0.90f);

        if (isDark)
        {
            Color hourColor = HslToRgb(h, clampedSat, 0.84f, 255);
            Color minuteColor = HslToRgb((h + 14f) % 360f, clampedSat * 0.76f, 0.66f, 255);
            Color secondAccent = HslToRgb((h + 38f) % 360f, Math.Min(1f, clampedSat * 1.15f), 0.72f, 255);
            Color surfaceColor = HslToRgb(h, clampedSat * 0.28f, 0.13f, 225);
            Color pillColor = HslToRgb(h, clampedSat * 0.40f, 0.22f, 235);
            Color fluentInner = HslToRgb(h, clampedSat * 0.22f, 0.20f, 210);
            Color fluentBorderTop = Color.FromArgb(140, 255, 255, 255);
            Color fluentBorderBottom = Color.FromArgb(30, 255, 255, 255);

            return new MaterialPalette(
                hourColor, minuteColor, secondAccent,
                surfaceColor, pillColor, fluentInner,
                fluentBorderTop, fluentBorderBottom, true);
        }
        else
        {
            Color hourColor = HslToRgb(h, clampedSat, 0.25f, 255);
            Color minuteColor = HslToRgb((h + 14f) % 360f, clampedSat * 0.78f, 0.42f, 255);
            Color secondAccent = HslToRgb((h + 38f) % 360f, Math.Min(1f, clampedSat * 1.1f), 0.50f, 255);
            Color surfaceColor = HslToRgb(h, clampedSat * 0.35f, 0.93f, 232);
            Color pillColor = HslToRgb(h, clampedSat * 0.45f, 0.84f, 238);
            Color fluentInner = Color.FromArgb(215, 255, 255, 255);
            Color fluentBorderTop = Color.FromArgb(210, 255, 255, 255);
            Color fluentBorderBottom = Color.FromArgb(65, 0, 0, 0);

            return new MaterialPalette(
                hourColor, minuteColor, secondAccent,
                surfaceColor, pillColor, fluentInner,
                fluentBorderTop, fluentBorderBottom, false);
        }
    }

    private static void RgbToHsl(Color c, out float h, out float s, out float l)
    {
        float r = c.R / 255f;
        float g = c.G / 255f;
        float b = c.B / 255f;

        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        l = (max + min) * 0.5f;

        if (MathF.Abs(max - min) < 1e-5f)
        {
            h = 0f;
            s = 0f;
            return;
        }

        float d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

        if (max == r)
            h = ((g - b) / d + (g < b ? 6f : 0f)) * 60f;
        else if (max == g)
            h = ((b - r) / d + 2f) * 60f;
        else
            h = ((r - g) / d + 4f) * 60f;
    }

    private static Color HslToRgb(float h, float s, float l, byte alpha)
    {
        if (s <= 1e-5f)
        {
            byte v = (byte)Math.Clamp((int)(l * 255f), 0, 255);
            return Color.FromArgb(alpha, v, v, v);
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        float hk = h / 360f;

        byte r = (byte)Math.Clamp((int)(HueToRgb(p, q, hk + 1f / 3f) * 255f), 0, 255);
        byte g = (byte)Math.Clamp((int)(HueToRgb(p, q, hk) * 255f), 0, 255);
        byte b = (byte)Math.Clamp((int)(HueToRgb(p, q, hk - 1f / 3f) * 255f), 0, 255);
        return Color.FromArgb(alpha, r, g, b);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }
}

