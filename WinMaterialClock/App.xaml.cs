using Microsoft.UI.Xaml;

namespace WinMaterialClock;

/// <summary>
/// 應用程式進入點：負責初始化 WinUI 3 環境並透過 <see cref="WidgetManager"/> 啟動桌面小工具。
/// </summary>
public sealed partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 當應用程式啟動時觸發，開啟預設桌面時鐘小工具。
    /// 使用者可隨時於小工具上按右鍵或雙擊開啟 <see cref="SettingsCenterWindow"/> 管理所有樣式實例。
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MaterialPalette palette = MaterialYouPalette.GenerateFromSystem();
        NativeMethods.SetPreferredAppMode(palette.IsDarkTheme ? 2 : 3);
        NativeMethods.FlushMenuThemes();

        WidgetManager.AddWidget(ClockStyle.Pixel2x2);
    }
}

