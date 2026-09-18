using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>
/// Per-window taskbar identity. Windows groups taskbar buttons by
/// AppUserModelID: giving each game window its own id yields one button
/// per client. The store comes from SHGetPropertyStoreForWindow — no
/// manual QueryInterface (the .lnk PropertyStore cast proved fragile).
/// Best effort: failures never break the launch.
/// </summary>
public static class TaskbarIdentity
{
    private static readonly Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF74");

    // PKEY_AppUserModel_ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, pid 5.
    private static readonly Guid PKEY_AppUserModelID_FmtId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private const uint PKEY_AppUserModelID_Pid = 5;
    private const ushort VT_LPWSTR = 31;
    private const int S_OK = 0;

    public static bool TrySetAppId(IntPtr windowHandle, string appId)
    {
        if (windowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(appId))
            return false;
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            Guid iid = IID_IPropertyStore;
            int hr = NativeMethods.SHGetPropertyStoreForWindow(windowHandle, ref iid, out object? raw);
            if (hr != S_OK || raw is not IPropertyStore store)
                return false;
            try
            {
                var key = new PROPERTYKEY { fmtid = PKEY_AppUserModelID_FmtId, pid = PKEY_AppUserModelID_Pid };
                IntPtr pwsz = Marshal.StringToCoTaskMemUni(appId);
                try
                {
                    var value = new PROPVARIANT { vt = VT_LPWSTR, pwszVal = pwsz };
                    store.SetValue(ref key, ref value);
                    store.Commit();
                    return true;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pwsz);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        catch (Exception)
        {
            return false;
        }
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
