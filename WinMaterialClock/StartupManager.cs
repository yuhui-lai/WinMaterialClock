namespace WinMaterialClock;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "WinMaterialClock";

    public static bool IsStartupEnabled()
    {
        if (NativeMethods.RegOpenKeyEx(NativeMethods.HKEY_CURRENT_USER, RunKeyPath, 0, NativeMethods.KEY_READ, out nint hKey) != 0)
            return false;

        try
        {
            uint size = 0;
            int result = NativeMethods.RegQueryValueEx(hKey, AppValueName, 0, out _, 0, ref size);
            return result == 0 && size > 0;
        }
        finally
        {
            NativeMethods.RegCloseKey(hKey);
        }
    }

    public static void SetStartup(bool enable)
    {
        if (NativeMethods.RegOpenKeyEx(NativeMethods.HKEY_CURRENT_USER, RunKeyPath, 0, NativeMethods.KEY_WRITE, out nint hKey) != 0)
            return;

        try
        {
            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    string quoted = $"\"{exePath}\"";
                    uint byteCount = (uint)((quoted.Length + 1) * sizeof(char));
                    NativeMethods.RegSetValueEx(hKey, AppValueName, 0, NativeMethods.REG_SZ, quoted, byteCount);
                }
            }
            else
            {
                NativeMethods.RegDeleteValue(hKey, AppValueName);
            }
        }
        finally
        {
            NativeMethods.RegCloseKey(hKey);
        }
    }
}
