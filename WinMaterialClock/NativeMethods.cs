using System.Runtime.InteropServices;

namespace WinMaterialClock;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenu(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(nint hmenu, uint fuFlags, int x, int y, nint hwnd, nint lptpm);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint hMenu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial nint GetShellWindow();

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RemoveWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass);

    [LibraryImport("comctl32.dll")]
    public static partial nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmExtendFrameIntoClientArea(nint hWnd, in MARGINS pMarInset);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmEnableBlurBehindWindow(nint hWnd, in DWM_BLURBEHIND pBlurBehind);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateRectRgn(int x1, int y1, int x2, int y2);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32.dll")]
    public static partial nint GetStockObject(int i);

    [LibraryImport("user32.dll")]
    public static partial int FillRect(nint hDC, in RECT lprc, nint hbr);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    public static partial nint LoadIcon(nint hInstance, nint lpIconName);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint dwMessage, in NOTIFYICONDATAW lpData);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, in uint pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    public static partial int SetPreferredAppMode(int preferredAppMode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    public static partial void FlushMenuThemes();

    [LibraryImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegOpenKeyEx(nint hKey, string lpSubKey, uint ulOptions, uint samDesired, out nint phkResult);

    [LibraryImport("advapi32.dll", EntryPoint = "RegQueryValueExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegQueryValueEx(nint hKey, string lpValueName, nint lpReserved, out uint lpType, nint lpData, ref uint lpcbData);

    [LibraryImport("advapi32.dll", EntryPoint = "RegSetValueExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegSetValueEx(nint hKey, string lpValueName, uint Reserved, uint dwType, string lpData, uint cbData);

    [LibraryImport("advapi32.dll", EntryPoint = "RegDeleteValueW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegDeleteValue(nint hKey, string lpValueName);

    [LibraryImport("advapi32.dll")]
    public static partial int RegCloseKey(nint hKey);

    [LibraryImport("d2d1.dll")]
    public static partial int D2D1CreateFactory(int factoryType, in Guid riid, nint pFactoryOptions, out nint ppIFactory);

    [LibraryImport("d3d11.dll")]
    public static partial int D3D11CreateDevice(
        nint pAdapter,
        int DriverType,
        nint Software,
        uint Flags,
        nint pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out nint ppDevice,
        out int pFeatureLevel,
        out nint ppImmediateContext);

    [LibraryImport("dwrite.dll")]
    public static partial int DWriteCreateFactory(int factoryType, in Guid iid, out nint factory);

    [LibraryImport("kernel32.dll")]
    public static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool K32EmptyWorkingSet(nint hProcess);

    [LibraryImport("CoreMessaging.dll")]
    public static partial int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint dispatcherQueueController);
}
