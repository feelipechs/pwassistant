using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>
/// Per-account shortcut creation (taskbar separation). Each game account
/// launches through its own .lnk carrying a distinct AppUserModelID, so
/// Windows shows one taskbar button per client instead of grouping them.
/// All COM interop lives here (project law #2).
/// </summary>
public static class ShortcutCreator
{
    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");

    // PKEY_AppUserModel_ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, pid 5.
    private static readonly Guid PKEY_AppUserModelID_FmtId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint PKEY_AppUserModelID_Pid = 5;
    private const ushort VT_LPWSTR = 31;

    public static void EnsureShortcut(
        string shortcutPath, string targetPath, string arguments,
        string workingDirectory, string appUserModelId, string iconLocation)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Shell shortcuts require Windows.");

        string? directory = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var link = (IShellLinkW)Activator.CreateInstance(
            Type.GetTypeFromCLSID(CLSID_ShellLink)!)!;
        try
        {
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetIconLocation(iconLocation, 0);

            var propertyStore = (IPropertyStore)link;
            var key = new PROPERTYKEY { fmtid = PKEY_AppUserModelID_FmtId, pid = PKEY_AppUserModelID_Pid };
            IntPtr pwsz = Marshal.StringToCoTaskMemUni(appUserModelId);
            try
            {
                var value = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = pwsz };
                propertyStore.SetValue(ref key, ref value);
                propertyStore.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(pwsz);
            }

            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, out IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF74")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        uint GetCount();
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        void Commit();
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }
}
