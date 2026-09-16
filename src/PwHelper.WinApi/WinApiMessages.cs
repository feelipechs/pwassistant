namespace PwHelper.WinApi;

internal static class WinApiMessages
{
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_ACTIVATEAPP = 0x001C;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_NCHITTEST = 0x0084;

    public const int WA_ACTIVE = 1;
    public const int WA_INACTIVE = 0;
    public const int HTCLIENT = 1;
    public const int MK_LBUTTON = 0x0001;

    public const uint MAPVK_VK_TO_VSC = 0;

    public static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

    public static IntPtr MakeKeyDownLParam(uint scanCode) => (IntPtr)(1 | (scanCode << 16));

    public static IntPtr MakeKeyUpLParam(uint scanCode) =>
        (IntPtr)(1 | (scanCode << 16) | (1u << 30) | (1u << 31));
}
