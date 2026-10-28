namespace WinMaterialClock;

/// <summary>
/// 定義桌面時鐘小工具支援的視覺樣式枚舉。
/// </summary>
public enum ClockStyle
{
    /// <summary>經典 Google Pixel 2x2 雙排粗體大字數位時鐘。</summary>
    Pixel2x2 = 0,

    /// <summary>Material You 12 瓣扇形波浪錶盤 (Scalloped Dial) 搭配幾何指針。</summary>
    AnalogScalloped = 1,

    /// <summary>經典圓形 60 刻度錶盤搭配時、分、秒三根指針。</summary>
    AnalogClassic = 2
}

/// <summary>
/// 小工具實例管理器：負責集中維護所有桌面時鐘視窗生命週期，
/// 並強制執行「每種 <see cref="ClockStyle"/> 嚴格限制僅能開啟 1 個實例」的單例約束。
/// </summary>
public static class WidgetManager
{
    /// <summary>
    /// 內部維護各樣式對應之唯一視窗實例字典。
    /// Key 為 <see cref="ClockStyle"/>，Value 為開啟中的小工具視窗。
    /// </summary>
    private static readonly Dictionary<ClockStyle, ClockWidgetWindow> s_activeWidgets = new();

    /// <summary>取得全域持久化設定實例。</summary>
    public static AppSettings Settings { get; } = AppSettings.Load();

    /// <summary>當小工具被新增、移除或全域設定變更時觸發之事件，供 SettingsView 即時重新渲染。</summary>
    public static event Action? WidgetsStateChanged;

    /// <summary>取得目前是否啟用全域滑鼠穿透點擊模式 (WS_EX_TRANSPARENT)。</summary>
    public static bool IsClickThroughEnabled => Settings.IsLockedClickThrough;

    /// <summary>
    /// 檢查指定的時鐘樣式目前是否已在桌面上開啟。
    /// </summary>
    /// <param name="style">欲檢查的時鐘樣式。</param>
    /// <returns>若該樣式已有開啟實例則回傳 true，否則回傳 false。</returns>
    public static bool IsWidgetOpen(ClockStyle style)
    {
        return s_activeWidgets.ContainsKey(style);
    }

    /// <summary>
    /// 新增指定樣式的時鐘小工具至桌面。
    /// 若該樣式已開啟，則嚴格禁止重複建立，並直接將現有實例移至前景。
    /// </summary>
    /// <param name="style">欲新增的時鐘樣式。</param>
    /// <returns>是否成功新建小工具實例。</returns>
    public static bool AddWidget(ClockStyle style)
    {
        if (s_activeWidgets.ContainsKey(style))
        {
            BringToFront(style);
            return false;
        }

        int offsetIndex = (int)style;
        int initialX = Settings.WindowX + (offsetIndex * 280);
        int initialY = Settings.WindowY;

        var widgetWindow = new ClockWidgetWindow(style, initialX, initialY);
        s_activeWidgets[style] = widgetWindow;

        widgetWindow.Closed += (_, _) =>
        {
            if (s_activeWidgets.Remove(style))
            {
                WidgetsStateChanged?.Invoke();
                TrimProcessMemory();
            }
        };

        widgetWindow.Activate();
        WidgetsStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 從桌面上關閉並移除指定樣式的時鐘小工具。
    /// </summary>
    /// <param name="style">欲移除的時鐘樣式。</param>
    /// <returns>若成功移除則回傳 true。</returns>
    public static bool RemoveWidget(ClockStyle style)
    {
        if (!s_activeWidgets.Remove(style, out ClockWidgetWindow? window))
            return false;

        window.Close();
        WidgetsStateChanged?.Invoke();
        TrimProcessMemory();
        return true;
    }

    /// <summary>
    /// 將指定樣式的小工具視窗移至前景。
    /// </summary>
    /// <param name="style">目標時鐘樣式。</param>
    public static void BringToFront(ClockStyle style)
    {
        if (s_activeWidgets.TryGetValue(style, out ClockWidgetWindow? window))
        {
            window.BringWidgetToFront();
        }
    }

    /// <summary>
    /// 切換全域穿透點擊模式 (WS_EX_TRANSPARENT)，並同步套用至所有開啟中的小工具視窗。
    /// </summary>
    /// <param name="enable">true 表示鎖定位置並啟用穿透點擊；false 表示恢復可拖曳狀態。</param>
    public static void SetClickThroughAll(bool enable)
    {
        Settings.IsLockedClickThrough = enable;
        Settings.Save();

        foreach (var window in s_activeWidgets.Values)
        {
            window.SetClickThrough(enable);
        }

        WidgetsStateChanged?.Invoke();
    }

    /// <summary>
    /// 同步調整所有開啟中小工具的縮放大小 (50% ~ 180%)。
    /// </summary>
    /// <param name="scale">縮放倍率 (0.5f ~ 1.8f)。</param>
    public static void SetScaleAll(float scale)
    {
        Settings.Scale = Math.Clamp(scale, 0.5f, 1.8f);
        foreach (var window in s_activeWidgets.Values)
        {
            window.ApplyScaleAndOpacity(Settings.Scale, Settings.Opacity);
        }
    }

    /// <summary>
    /// 同步調整所有開啟中小工具的整體不透明度 (15% ~ 100%)。
    /// </summary>
    /// <param name="opacity">不透明度 (0.15f ~ 1.0f)。</param>
    public static void SetOpacityAll(float opacity)
    {
        Settings.Opacity = Math.Clamp(opacity, 0.15f, 1.0f);
        foreach (var window in s_activeWidgets.Values)
        {
            window.ApplyScaleAndOpacity(Settings.Scale, Settings.Opacity);
        }
    }

    /// <summary>
    /// 關閉所有開啟中的小工具視窗並結束程式。
    /// </summary>
    public static void CloseAllWidgets()
    {
        foreach (var window in s_activeWidgets.Values.ToArray())
        {
            window.Close();
        }
        s_activeWidgets.Clear();
    }

    /// <summary>
    /// 呼叫 Win32 API 壓縮並釋放閒置工作集記憶體，維持極致低記憶體佔用。
    /// </summary>
    public static void TrimProcessMemory()
    {
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        nint proc = NativeMethods.GetCurrentProcess();
        NativeMethods.SetProcessWorkingSetSize(proc, -1, -1);
        NativeMethods.K32EmptyWorkingSet(proc);
    }
}
