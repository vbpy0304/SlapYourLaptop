using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TapOS.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModWin = 0x0008;
    private const uint VkM = 0x4D;
    private readonly HwndSource _source;
    private readonly int _id = 0x544150;
    private bool _registered;

    public GlobalHotkeyService(HwndSource source)
    {
        _source = source;
        _source.AddHook(WndProc);
    }

    public bool IsRegistered => _registered;
    public string? Error { get; private set; }
    public event EventHandler? Pressed;

    public bool Register()
    {
        _registered = RegisterHotKey(_source.Handle, _id, ModWin | ModAlt, VkM);
        if (!_registered)
        {
            Error = $"Win+Alt+M could not be registered (Windows error {Marshal.GetLastWin32Error()}).";
        }

        return _registered;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, _id);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
