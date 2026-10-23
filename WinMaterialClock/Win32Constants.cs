using System.Runtime.InteropServices;

namespace WinMaterialClock;

internal static partial class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int GWLP_HWNDPARENT = -8;

    public static readonly nint WS_POPUP = unchecked((nint)0x80000000);
    public static readonly nint WS_VISIBLE = 0x10000000;
    public static readonly nint WS_CAPTION = 0x00C00000;
    public static readonly nint WS_THICKFRAME = 0x00040000;
    public static readonly nint WS_MINIMIZEBOX = 0x00020000;
    public static readonly nint WS_MAXIMIZEBOX = 0x00010000;
    public static readonly nint WS_SYSMENU = 0x00080000;

    public static readonly nint WS_EX_TOOLWINDOW = 0x00000080;
    public static readonly nint WS_EX_LAYERED = 0x00080000;
    public static readonly nint WS_EX_TRANSPARENT = 0x00000020;
    public static readonly nint WS_EX_TOPMOST = 0x00000008;
    public static readonly nint WS_EX_APPWINDOW = 0x00040000;

    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_NOTOPMOST = -2;
    public static readonly nint HWND_BOTTOM = 1;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const uint LWA_ALPHA = 0x00000002;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_WINDOWPOSCHANGING = 0x0046;
    public const uint WM_NCHITTEST = 0x0084;
    public const uint WM_NCLBUTTONDOWN = 0x00A1;
    public const uint WM_NCRBUTTONUP = 0x00A5;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_POWERBROADCAST = 0x0218;
    public const uint WM_EXITSIZEMOVE = 0x0232;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_TRAYICON = 0x8001;

    public const nint HTCAPTION = 2;
    public const nint HTCLIENT = 1;
    public const nint HTTRANSPARENT = -1;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int HOTKEY_ID_TOGGLE_LOCK = 0x9001;
    public const int HOTKEY_ID_OPEN_SETTINGS = 0x9002;

    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const nint IDI_APPLICATION = 32512;

    public const uint MF_STRING = 0x0000;
    public const uint MF_CHECKED = 0x0008;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_NONOTIFY = 0x0080;

    public const int DWMWA_NCRENDERING_POLICY = 2;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const uint DWMNCRP_DISABLED = 1;
    public const uint DWMWCP_DONOTROUND = 1;
    public const uint DWM_COLOR_NONE = 0xFFFFFFFE;

    public static readonly nint HKEY_CURRENT_USER = unchecked((nint)0x80000001);
    public const uint KEY_READ = 0x20019;
    public const uint KEY_WRITE = 0x20006;
    public const uint REG_SZ = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public int fEnable;
        public nint hRgnBlur;
        public int fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public nint hwnd;
        public nint hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState;
        public uint dwStateMask;
        public fixed char szInfo[256];
        public uint uVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    public delegate nint SUBCLASSPROC(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);
}
