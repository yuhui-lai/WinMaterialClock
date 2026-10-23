using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace WinMaterialClock;

public sealed partial class MainWindow : Window
{
    private const int CMD_OPEN_SETTINGS = 2001;
    private const int CMD_EXIT = 2099;
    private const uint TRAY_ICON_ID = 1;

    private readonly nint _hwnd;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly UISettings _uiSettings = new();
    private readonly CancellationTokenSource _appExitCts = new();
    private readonly NativeMethods.SUBCLASSPROC _subclassDelegate;
    private readonly AppSettings _settings;

    private CancellationTokenSource _scheduleWakeCts = new();
    private PixelClockCompositionRenderer? _renderer;
    private SettingsWindow? _settingsWindow;
    private MaterialPalette _currentPalette;
    private bool _trayAdded;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _subclassDelegate = WindowSubclassProc;
        _settings = AppSettings.Load();

        SystemBackdrop = new TransparentBackdrop();
        CompositionHost.PointerPressed += OnCompositionHostPointerPressed;
        CompositionHost.DoubleTapped += (_, _) => OpenSettingsWindow();

        ConfigureFramelessDesktopWindow();
        InitializeCompositionScene();
        AddTrayIcon();

        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;

        // 註冊全域熱鍵：Alt+Shift+L 切換穿透鎖定、Alt+Shift+S 開啟設定畫面
        NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HOTKEY_ID_TOGGLE_LOCK,
            NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            'L');
        NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HOTKEY_ID_OPEN_SETTINGS,
            NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            'S');

        _ = RunAdaptiveSchedulerAsync(_appExitCts.Token);
    }

    private void ConfigureFramelessDesktopWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        nint style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME |
                   NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU);
        style |= NativeMethods.WS_POPUP;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_STYLE, style);

        nint exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~(NativeMethods.WS_EX_APPWINDOW | NativeMethods.WS_EX_TOPMOST);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        if (_settings.IsLockedClickThrough)
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        nint shellWindow = NativeMethods.GetShellWindow();
        if (shellWindow != 0)
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_HWNDPARENT, shellWindow);

        uint disablePolicy = NativeMethods.DWMNCRP_DISABLED;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_NCRENDERING_POLICY, in disablePolicy, sizeof(uint));

        uint doNotRound = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, in doNotRound, sizeof(uint));

        uint borderNone = NativeMethods.DWM_COLOR_NONE;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_BORDER_COLOR, in borderNone, sizeof(uint));

        NativeMethods.MARGINS margins = new() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, in margins);

        nint hRgn = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        NativeMethods.DWM_BLURBEHIND blurBehind = new()
        {
            dwFlags = 0x1 | 0x2,
            fEnable = 1,
            hRgnBlur = hRgn,
            fTransitionOnMaximized = 0
        };
        NativeMethods.DwmEnableBlurBehindWindow(_hwnd, in blurBehind);
        NativeMethods.DeleteObject(hRgn);

        UpdateWindowBoundsAndPinToBottom(_settings.WindowX, _settings.WindowY);
        NativeMethods.SetWindowSubclass(_hwnd, _subclassDelegate, 1, 0);
    }

    private void UpdateWindowBoundsAndPinToBottom(int x, int y)
    {
        float dpiScale = GetWindowDpiScale();
        int widthPx = (int)MathF.Ceiling(PixelClockCompositionRenderer.WidgetWidthDip * _settings.Scale * dpiScale);
        int heightPx = (int)MathF.Ceiling(PixelClockCompositionRenderer.WidgetHeightDip * _settings.Scale * dpiScale);

        NativeMethods.SetWindowPos(
            _hwnd,
            NativeMethods.HWND_BOTTOM,
            x, y,
            widthPx, heightPx,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    private void InitializeCompositionScene()
    {
        Visual hostVisual = ElementCompositionPreview.GetElementVisual(CompositionHost);
        Compositor compositor = hostVisual.Compositor;

        _renderer = new PixelClockCompositionRenderer(compositor, GetWindowDpiScale());
        _renderer.SetVisualStyle(_settings.Style);
        _renderer.SetScale(_settings.Scale);
        _renderer.SetOpacity(_settings.Opacity);
        ElementCompositionPreview.SetElementChildVisual(CompositionHost, _renderer.RootVisual);

        ApplyMaterialPalette(animate: false);
        SyncClockDisplay(animate: false);
        TrimWorkingSet();
    }

    private void TrimWorkingSet()
    {
        _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, static () =>
        {
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            nint proc = NativeMethods.GetCurrentProcess();
            NativeMethods.SetProcessWorkingSetSize(proc, -1, -1);
            NativeMethods.K32EmptyWorkingSet(proc);
        });
    }

    /// <summary>
    /// 智慧雙模省電排程器：
    /// - 數位時鐘模式：精確休眠至下一分鐘 00 秒（59.9 秒執行緒完全靜止）。
    /// - 圓形指針模式：精確休眠至下一秒 000 毫秒（推動秒針平滑跳動）。
    /// </summary>
    private async Task RunAdaptiveSchedulerAsync(CancellationToken appExitToken)
    {
        try
        {
            await Task.Delay(700, appExitToken).ConfigureAwait(false);
            TrimWorkingSet();

            while (!appExitToken.IsCancellationRequested)
            {
                DateTimeOffset now = TimeProvider.System.GetLocalNow();
                bool isAnalog = _settings.Style == ClockVisualStyle.CircularAnalog;

                int remainingMs = isAnalog
                    ? (1000 - now.Millisecond + 10)
                    : ((60 - now.Second) * 1000 - now.Millisecond + 15);

                if (remainingMs <= 0)
                    remainingMs = isAnalog ? 200 : 1000;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appExitToken, _scheduleWakeCts.Token);
                try
                {
                    await Task.Delay(remainingMs, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!appExitToken.IsCancellationRequested)
                {
                    // 樣式切換喚醒排程器
                }

                _dispatcherQueue.TryEnqueue(() => SyncClockDisplay(animate: true));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void WakeScheduler()
    {
        var oldCts = Interlocked.Exchange(ref _scheduleWakeCts, new CancellationTokenSource());
        oldCts.Cancel();
        oldCts.Dispose();
    }

    private void SyncClockDisplay(bool animate)
    {
        if (_renderer is null)
            return;

        DateTimeOffset now = TimeProvider.System.GetLocalNow();
        _renderer.UpdateTime(now.Hour, now.Minute, now.Second, animate);
    }

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() => ApplyMaterialPalette(animate: true));
    }

    private void ApplyMaterialPalette(bool animate)
    {
        if (_renderer is null)
            return;

        _currentPalette = MaterialYouPalette.GenerateFromSystem();
        _renderer.ApplyPalette(_currentPalette, animate);

        NativeMethods.SetPreferredAppMode(_currentPalette.IsDarkTheme ? 2 : 3);
        NativeMethods.FlushMenuThemes();
    }

    public void UpdateStyleFromSettings(ClockVisualStyle newStyle)
    {
        _settings.Style = newStyle;
        _settings.Save();
        _renderer?.SetVisualStyle(newStyle);
        SyncClockDisplay(animate: false);
        WakeScheduler();
    }

    public void UpdateScaleFromSettings(float newScale)
    {
        _settings.Scale = Math.Clamp(newScale, 0.5f, 1.8f);
        _renderer?.SetScale(_settings.Scale);
        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rc))
        {
            _settings.WindowX = rc.Left;
            _settings.WindowY = rc.Top;
        }
        UpdateWindowBoundsAndPinToBottom(_settings.WindowX, _settings.WindowY);
    }

    public void UpdateOpacityFromSettings(float newOpacity)
    {
        _settings.Opacity = Math.Clamp(newOpacity, 0.15f, 1.0f);
        _renderer?.SetOpacity(_settings.Opacity);
    }

    public void ResetClockPosition()
    {
        _settings.WindowX = 120;
        _settings.WindowY = 120;
        _settings.Save();
        UpdateWindowBoundsAndPinToBottom(120, 120);
    }

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(this, _settings, _currentPalette);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            TrimWorkingSet();
        };
        _settingsWindow.Activate();
    }

    public void ExitApplication()
    {
        _settingsWindow?.Close();
        Close();
    }

    private void OnCompositionHostPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_settings.IsLockedClickThrough)
            return;

        PointerPointProperties props = e.GetCurrentPoint(CompositionHost).Properties;
        if (props.IsLeftButtonPressed)
        {
            e.Handled = true;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(_hwnd, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            SaveCurrentWindowPosition();
        }
        else if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            ShowQuickContextMenu();
        }
    }

    private void SaveCurrentWindowPosition()
    {
        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rc))
        {
            _settings.WindowX = rc.Left;
            _settings.WindowY = rc.Top;
            _settings.Save();
        }
    }

    public void ToggleClickThroughLock()
    {
        _settings.IsLockedClickThrough = !_settings.IsLockedClickThrough;
        _settings.Save();

        nint exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        if (_settings.IsLockedClickThrough)
            exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
        else
            exStyle &= ~(NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);

        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        _settingsWindow?.RefreshLockState();
    }

    private void ShowQuickContextMenu()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt))
            return;

        nint hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == 0)
            return;

        try
        {
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_OPEN_SETTINGS, "開啟設定畫面... (Alt+Shift+S)");
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_EXIT, "離開 WinMaterialClock");

            NativeMethods.SetForegroundWindow(_hwnd);
            int cmd = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                pt.X, pt.Y, _hwnd, 0);

            if (cmd == CMD_OPEN_SETTINGS)
                OpenSettingsWindow();
            else if (cmd == CMD_EXIT)
                ExitApplication();
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    private unsafe void AddTrayIcon()
    {
        NativeMethods.NOTIFYICONDATAW nid = default;
        nid.cbSize = (uint)sizeof(NativeMethods.NOTIFYICONDATAW);
        nid.hWnd = _hwnd;
        nid.uID = TRAY_ICON_ID;
        nid.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        nid.uCallbackMessage = NativeMethods.WM_TRAYICON;
        nid.hIcon = NativeMethods.LoadIcon(0, NativeMethods.IDI_APPLICATION);

        ReadOnlySpan<char> tip = "WinMaterialClock 桌面時鐘 (點擊開啟設定)";
        for (int i = 0; i < tip.Length && i < 127; i++)
            nid.szTip[i] = tip[i];

        _trayAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, in nid);
    }

    private unsafe void RemoveTrayIcon()
    {
        if (!_trayAdded)
            return;

        NativeMethods.NOTIFYICONDATAW nid = default;
        nid.cbSize = (uint)sizeof(NativeMethods.NOTIFYICONDATAW);
        nid.hWnd = _hwnd;
        nid.uID = TRAY_ICON_ID;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, in nid);
        _trayAdded = false;
    }

    private float GetWindowDpiScale()
    {
        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return dpi > 0 ? dpi / 96f : 1.0f;
    }

    private nint WindowSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        switch (uMsg)
        {
            case NativeMethods.WM_ERASEBKGND:
                NativeMethods.GetClientRect(hWnd, out NativeMethods.RECT rc);
                NativeMethods.FillRect(wParam, in rc, NativeMethods.GetStockObject(4));
                return 1;

            case NativeMethods.WM_WINDOWPOSCHANGING:
                unsafe
                {
                    var wp = (NativeMethods.WINDOWPOS*)lParam;
                    wp->hwndInsertAfter = NativeMethods.HWND_BOTTOM;
                    wp->flags |= NativeMethods.SWP_NOACTIVATE;
                }
                return 0;

            case NativeMethods.WM_NCHITTEST:
                if (_settings.IsLockedClickThrough)
                    return NativeMethods.HTTRANSPARENT;
                return NativeMethods.HTCLIENT;

            case NativeMethods.WM_EXITSIZEMOVE:
                SaveCurrentWindowPosition();
                return 0;

            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_NCRBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                ShowQuickContextMenu();
                return 0;

            case NativeMethods.WM_TRAYICON:
                uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                if (mouseMsg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_LBUTTONDBLCLK)
                {
                    _dispatcherQueue.TryEnqueue(OpenSettingsWindow);
                    return 0;
                }
                if (mouseMsg is NativeMethods.WM_RBUTTONUP or NativeMethods.WM_CONTEXTMENU)
                {
                    _dispatcherQueue.TryEnqueue(ShowQuickContextMenu);
                    return 0;
                }
                break;

            case NativeMethods.WM_HOTKEY:
                int hotkeyId = (int)wParam;
                if (hotkeyId == NativeMethods.HOTKEY_ID_TOGGLE_LOCK)
                {
                    ToggleClickThroughLock();
                    return 0;
                }
                if (hotkeyId == NativeMethods.HOTKEY_ID_OPEN_SETTINGS)
                {
                    OpenSettingsWindow();
                    return 0;
                }
                break;

            case NativeMethods.WM_DWMCOLORIZATIONCOLORCHANGED:
            case NativeMethods.WM_SETTINGCHANGE:
                _dispatcherQueue.TryEnqueue(() => ApplyMaterialPalette(animate: true));
                break;

            case NativeMethods.WM_POWERBROADCAST:
                _dispatcherQueue.TryEnqueue(() => SyncClockDisplay(animate: false));
                break;

            case NativeMethods.WM_DPICHANGED:
                float newScale = (wParam.ToInt32() & 0xFFFF) / 96f;
                _renderer?.UpdateDpi(newScale);
                UpdateWindowBoundsAndPinToBottom(_settings.WindowX, _settings.WindowY);
                return 0;

            case NativeMethods.WM_DESTROY:
                _appExitCts.Cancel();
                RemoveTrayIcon();
                _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
                NativeMethods.UnregisterHotKey(hWnd, NativeMethods.HOTKEY_ID_TOGGLE_LOCK);
                NativeMethods.UnregisterHotKey(hWnd, NativeMethods.HOTKEY_ID_OPEN_SETTINGS);
                NativeMethods.RemoveWindowSubclass(hWnd, _subclassDelegate, 1);
                _renderer?.Dispose();
                break;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}
