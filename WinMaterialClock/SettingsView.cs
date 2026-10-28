using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using WinMaterialClock.Reactor;

namespace WinMaterialClock;

/// <summary>
/// 定義設定中心 (<see cref="SettingsView"/>) 的不可變 MVU 狀態記錄。
/// </summary>
/// <param name="IsPixel2x2Open">Pixel 2x2 數位時鐘是否已開啟於桌面。</param>
/// <param name="IsScallopedOpen">Material You 扇形指針時鐘是否已開啟於桌面。</param>
/// <param name="IsClassicOpen">經典圓形刻度時鐘是否已開啟於桌面。</param>
/// <param name="IsStartupEnabled">是否已啟用 Windows 開機自動啟動。</param>
/// <param name="IsClickThrough">是否已啟用滑鼠穿透點擊模式 (WS_EX_TRANSPARENT)。</param>
/// <param name="Scale">時鐘縮放大小倍率 (0.5f ~ 1.8f)。</param>
/// <param name="Opacity">時鐘整體不透明度 (0.15f ~ 1.0f)。</param>
public sealed record SettingsViewState(
    bool IsPixel2x2Open,
    bool IsScallopedOpen,
    bool IsClassicOpen,
    bool IsStartupEnabled,
    bool IsClickThrough,
    float Scale,
    float Opacity)
{
    public SettingsViewState() : this(
        WidgetManager.IsWidgetOpen(ClockStyle.Pixel2x2),
        WidgetManager.IsWidgetOpen(ClockStyle.AnalogScalloped),
        WidgetManager.IsWidgetOpen(ClockStyle.AnalogClassic),
        StartupManager.IsStartupEnabled(),
        WidgetManager.IsClickThroughEnabled,
        WidgetManager.Settings.Scale,
        WidgetManager.Settings.Opacity)
    {
    }
}

/// <summary>
/// 基於 WinUIReactor 實作之 Fluent Design 設定中心視圖元件。
/// 提供所有時鐘樣式之預覽卡片、單一實例動態新增/移除控制，以及全域系統控制區。
/// </summary>
public sealed class SettingsView : RxComponent<SettingsViewState>
{
    /// <summary>當元件掛載時，訂閱 <see cref="WidgetManager.WidgetsStateChanged"/> 以即時響應小工具開關狀態。</summary>
    protected override void OnMounted()
    {
        WidgetManager.WidgetsStateChanged += OnExternalWidgetsStateChanged;
        RefreshStateFromManager();
    }

    /// <summary>當元件卸載時，解除事件訂閱以避免記憶體洩漏。</summary>
    protected override void OnWillUnmount()
    {
        WidgetManager.WidgetsStateChanged -= OnExternalWidgetsStateChanged;
    }

    private void OnExternalWidgetsStateChanged()
    {
        RefreshStateFromManager();
    }

    /// <summary>從 <see cref="WidgetManager"/> 與 <see cref="StartupManager"/> 同步最新狀態並觸發 MVU 重新渲染。</summary>
    private void RefreshStateFromManager()
    {
        SetState(s => s with
        {
            IsPixel2x2Open = WidgetManager.IsWidgetOpen(ClockStyle.Pixel2x2),
            IsScallopedOpen = WidgetManager.IsWidgetOpen(ClockStyle.AnalogScalloped),
            IsClassicOpen = WidgetManager.IsWidgetOpen(ClockStyle.AnalogClassic),
            IsStartupEnabled = StartupManager.IsStartupEnabled(),
            IsClickThrough = WidgetManager.IsClickThroughEnabled,
            Scale = WidgetManager.Settings.Scale,
            Opacity = WidgetManager.Settings.Opacity
        });
    }

    /// <summary>
    /// 宣告式渲染遵循 Windows 11 Fluent Design 規範之設定中心介面。
    /// </summary>
    public override RxNode Render()
    {
        MaterialPalette palette = MaterialYouPalette.GenerateFromSystem();
        bool isDark = palette.IsDarkTheme;

        Color cardBg = isDark ? Color.FromArgb(135, 42, 42, 48) : Color.FromArgb(190, 255, 255, 255);
        Color cardBorder = isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0);
        Color textPrimary = isDark ? Colors.White : Color.FromArgb(255, 22, 22, 26);
        Color textSecondary = isDark ? Color.FromArgb(180, 210, 210, 220) : Color.FromArgb(175, 85, 85, 100);
        Color accentColor = palette.HourDigits;

        return RxUI.ScrollableVStack(
            spacing: 10,
            padding: new Thickness(24, 34, 24, 24),
            RxUI.VStack(
                spacing: 4,
                padding: new Thickness(0, 0, 0, 2),
                RxUI.Label("WinMaterialClock 設定中心", 22, 700, textPrimary),
                RxUI.Label("Material You 桌面小工具管理 • 每種樣式嚴格限制單一實例並固定於桌面底層", 12.5, 400, textSecondary)
            ),
            RxUI.Label("時鐘小工具樣式庫", 13.5, 600, accentColor),
            BuildStyleCard(
                ClockStyle.Pixel2x2, "2×2", "Pixel 2x2 粗體數位時鐘",
                "經典 Google Pixel 上下兩列大字排版，採用零輪詢分鐘對齊休眠與 300ms Material 位移動畫。",
                State.IsPixel2x2Open, cardBg, cardBorder, textPrimary, textSecondary, accentColor),
            BuildStyleCard(
                ClockStyle.AnalogScalloped, "❁", "Material You 扇形指針時鐘",
                "12 瓣幾何扇形波浪色塊錶盤，搭配膠囊粗指針與 Compositor 純 GPU 無限循環秒針。",
                State.IsScallopedOpen, cardBg, cardBorder, textPrimary, textSecondary, accentColor),
            BuildStyleCard(
                ClockStyle.AnalogClassic, "◎", "經典圓形刻度指針時鐘",
                "雙層圓形錶盤內含 60 格精細分刻、12 時刻與時、分、秒三針，0% CPU 硬體加速旋轉。",
                State.IsClassicOpen, cardBg, cardBorder, textPrimary, textSecondary, accentColor),
            BuildGlobalControlsSection(cardBg, cardBorder, textPrimary, textSecondary, accentColor)
        );
    }

    private RxNode BuildGlobalControlsSection(
        Color cardBg,
        Color cardBorder,
        Color textPrimary,
        Color textSecondary,
        Color accentColor)
    {
        return RxUI.VStack(
            spacing: 10,
            padding: new Thickness(0),
            RxUI.Label("尺寸、透明度與全域控制", 13.5, 600, accentColor),
            RxUI.SettingsCard(
                cardBg, cardBorder, cornerRadius: 10, padding: new Thickness(16, 10, 16, 12),
                RxUI.VStack(
                    spacing: 8,
                    padding: new Thickness(0),
                    RxUI.SliderRow(
                        "時鐘縮放大小 (50% ~ 180%)",
                        0.50f, 1.80f, State.Scale,
                        textPrimary, accentColor, cardBorder,
                        onValueChanged: val => WidgetManager.SetScaleAll(val),
                        onCommit: () => WidgetManager.Settings.Save()),
                    RxUI.SliderRow(
                        "整體不透明度 (15% ~ 100%)",
                        0.15f, 1.00f, State.Opacity,
                        textPrimary, accentColor, cardBorder,
                        onValueChanged: val => WidgetManager.SetOpacityAll(val),
                        onCommit: () => WidgetManager.Settings.Save())
                )),
            RxUI.SettingsCard(
                cardBg, cardBorder, cornerRadius: 10, padding: new Thickness(16, 10, 16, 10),
                RxUI.SettingsRow(
                    previewBadge: null,
                    title: "開機自動啟動",
                    description: "登入 Windows 時自動於桌面還原時鐘小工具 (讀寫註冊表 CurrentVersion\\Run)",
                    textPrimary, textSecondary,
                    rightAction: BuildToggleButton(
                        State.IsStartupEnabled, accentColor, cardBorder, textPrimary,
                        onToggle: () =>
                        {
                            StartupManager.SetStartup(!StartupManager.IsStartupEnabled());
                            RefreshStateFromManager();
                        }))),
            RxUI.SettingsCard(
                cardBg, cardBorder, cornerRadius: 10, padding: new Thickness(16, 10, 16, 10),
                RxUI.SettingsRow(
                    previewBadge: null,
                    title: "穿透點擊模式 (鎖定位置 WS_EX_TRANSPARENT)",
                    description: "啟用後滑鼠點擊直接穿透至後方桌面，隨時可按 Alt + Shift + L 切換鎖定",
                    textPrimary, textSecondary,
                    rightAction: BuildToggleButton(
                        State.IsClickThrough, accentColor, cardBorder, textPrimary,
                        onToggle: () =>
                        {
                            WidgetManager.SetClickThroughAll(!State.IsClickThrough);
                            RefreshStateFromManager();
                        })))
        );
    }

    /// <summary>
    /// 建構單一時鐘樣式的 Fluent SettingsCard：
    /// 若未開啟則顯示「新增至桌面」；若已開啟則按鈕轉為「已新增」，並提供「移至前景」與「移除小工具」按鈕。
    /// </summary>
    private RxNode BuildStyleCard(
        ClockStyle style,
        string iconGlyph,
        string title,
        string description,
        bool isOpen,
        Color cardBg,
        Color cardBorder,
        Color textPrimary,
        Color textSecondary,
        Color accentColor)
    {
        var badgeNode = new RxElementNode(() => new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = iconGlyph,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        RxNode actionButtons = !isOpen
            ? RxUI.Button(
                text: "＋ 新增至桌面",
                background: Color.FromArgb(65, accentColor.R, accentColor.G, accentColor.B),
                border: accentColor,
                foreground: textPrimary,
                isEnabled: true,
                onClick: () =>
                {
                    WidgetManager.AddWidget(style);
                    RefreshStateFromManager();
                })
            : RxUI.HStack(
                spacing: 8,
                RxUI.Button(
                    text: "✓ 已新增",
                    background: Color.FromArgb(25, accentColor.R, accentColor.G, accentColor.B),
                    border: cardBorder,
                    foreground: accentColor,
                    isEnabled: false,
                    onClick: static () => { }),
                RxUI.Button(
                    text: "移至前景",
                    background: Colors.Transparent,
                    border: cardBorder,
                    foreground: textPrimary,
                    isEnabled: true,
                    onClick: () => WidgetManager.BringToFront(style)),
                RxUI.Button(
                    text: "移除小工具",
                    background: Color.FromArgb(45, 235, 87, 87),
                    border: Color.FromArgb(140, 235, 87, 87),
                    foreground: Color.FromArgb(255, 255, 115, 115),
                    isEnabled: true,
                    onClick: () =>
                    {
                        WidgetManager.RemoveWidget(style);
                        RefreshStateFromManager();
                    }));

        return RxUI.SettingsCard(
            cardBg, cardBorder,
            cornerRadius: 10,
            padding: new Thickness(16, 12, 16, 12),
            RxUI.SettingsRow(badgeNode, title, description, textPrimary, textSecondary, actionButtons));
    }

    private static RxNode BuildToggleButton(
        bool isOn,
        Color accentColor,
        Color borderColor,
        Color textPrimary,
        Action onToggle)
    {
        return RxUI.Button(
            text: isOn ? "● 已開啟" : "○ 已關閉",
            background: isOn ? Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B) : Colors.Transparent,
            border: isOn ? accentColor : borderColor,
            foreground: isOn ? accentColor : textPrimary,
            isEnabled: true,
            onClick: onToggle);
    }
}

/// <summary>
/// 承載 <see cref="SettingsView"/> 的 Windows 11 Fluent Design (<see cref="MicaBackdrop"/>) 設定中心視窗。
/// </summary>
public sealed partial class SettingsCenterWindow : Window
{
    private static SettingsCenterWindow? s_instance;

    /// <summary>開啟或喚醒唯一的設定中心視窗實例。</summary>
    public static void ShowInstance()
    {
        if (s_instance is not null)
        {
            s_instance.Activate();
            return;
        }

        s_instance = new SettingsCenterWindow();
        s_instance.Closed += (_, _) =>
        {
            s_instance = null;
            WidgetManager.TrimProcessMemory();
        };
        s_instance.Activate();
    }

    public SettingsCenterWindow()
    {
        Title = "WinMaterialClock 設定中心";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        float dpiScale = dpi > 0 ? dpi / 96f : 1.0f;

        int targetWidthPx = (int)MathF.Ceiling(680f * dpiScale);
        int targetHeightPx = (int)MathF.Ceiling(760f * dpiScale);
        AppWindow.Resize(new SizeInt32(targetWidthPx, targetHeightPx));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsResizable = true;
        }

        Content = new SettingsView().Build();
    }
}
