using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TapOS.Services;

public sealed class ScreenTouchOverlayProvider : IDisposable
{
    private const int WmPointerDown = 0x0246;
    private const int WmPointerUpdate = 0x0247;
    private const int WmPointerUp = 0x0248;
    private const int WmPointerCaptureChanged = 0x0249;
    private const int WmTouch = 0x0240;
    private const uint PointerTypeTouch = 2;
    private const uint TouchEventUp = 0x0002;
    private readonly FourFingerGestureRecognizer _recognizer;
    private readonly Window _window;
    private readonly HashSet<uint> _activeTouchPointers = [];
    private readonly HashSet<uint> _activeTouchContacts = [];
    private HwndSource? _source;
    private bool _touchRegistered;
    private bool _disposed;

    public ScreenTouchOverlayProvider(int maxDurationMilliseconds)
    {
        _recognizer = new FourFingerGestureRecognizer(TimeSpan.FromMilliseconds(maxDurationMilliseconds));
        _window = new Window
        {
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowActivated = false,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize
        };
        _window.SourceInitialized += Window_SourceInitialized;
        _window.Closing += (_, e) =>
        {
            if (!_disposed)
            {
                e.Cancel = true;
                _window.Hide();
            }
        };
    }

    public bool IsRunning { get; private set; }
    public int PointerMessagesReceived { get; private set; }
    public int TouchMessagesReceived { get; private set; }
    public int TouchContactsReported { get; private set; }
    public int ActiveTouchContacts => Math.Max(_activeTouchPointers.Count, _activeTouchContacts.Count);
    public bool IsWindowHandleReady => _source?.Handle != IntPtr.Zero;
    public bool IsTouchWindowRegistered => _touchRegistered;
    public string AvailabilityMessage =>
        "Optional screen-touch overlay: detects four simultaneous touchscreen contacts, not touchpad contacts. It can intercept interaction underneath while active.";

    public event EventHandler? FourFingerScreenTouchReleased;

    public void Start()
    {
        if (_disposed || IsRunning)
        {
            return;
        }

        _window.Left = SystemParameters.VirtualScreenLeft;
        _window.Top = SystemParameters.VirtualScreenTop;
        _window.Width = SystemParameters.VirtualScreenWidth;
        _window.Height = SystemParameters.VirtualScreenHeight;
        IsRunning = true;
        _window.Show();
        _window.Topmost = true;
    }

    public void Stop()
    {
        IsRunning = false;
        ResetContacts();
        _window.Hide();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _source?.RemoveHook(WndProc);
        if (_touchRegistered && _source is not null)
        {
            UnregisterTouchWindow(_source.Handle);
            _touchRegistered = false;
        }

        _window.SourceInitialized -= Window_SourceInitialized;
        _window.Close();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _source = (HwndSource)PresentationSource.FromVisual(_window)!;
        _source.AddHook(WndProc);
        _touchRegistered = RegisterTouchWindow(_source.Handle, 0);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!IsRunning)
        {
            return IntPtr.Zero;
        }

        if (message == WmPointerCaptureChanged)
        {
            ResetContacts();
            return IntPtr.Zero;
        }

        if (message == WmTouch)
        {
            TouchMessagesReceived++;
            ProcessTouchMessage(lParam);
            handled = true;
            return IntPtr.Zero;
        }

        if (message != WmPointerDown && message != WmPointerUpdate && message != WmPointerUp)
        {
            return IntPtr.Zero;
        }

        PointerMessagesReceived++;
        var pointerId = unchecked((uint)wParam.ToInt64() & 0xFFFF);
        if (!GetPointerType(pointerId, out var pointerType) || pointerType != PointerTypeTouch)
        {
            return IntPtr.Zero;
        }

        if (message == WmPointerDown)
        {
            _activeTouchPointers.Add(pointerId);
        }
        else if (message == WmPointerUp)
        {
            _activeTouchPointers.Remove(pointerId);
        }

        if (_recognizer.Process(_activeTouchPointers.Count, DateTimeOffset.UtcNow))
        {
            FourFingerScreenTouchReleased?.Invoke(this, EventArgs.Empty);
        }

        handled = true;
        return IntPtr.Zero;
    }

    private void ProcessTouchMessage(IntPtr lParam)
    {
        var touchCount = unchecked((uint)lParam.ToInt64() & 0xFFFF);
        if (touchCount == 0)
        {
            CloseTouchInputHandle(lParam);
            return;
        }

        var inputs = new TouchInput[(int)touchCount];
        if (!GetTouchInputInfo(lParam, touchCount, inputs, Marshal.SizeOf<TouchInput>()))
        {
            CloseTouchInputHandle(lParam);
            return;
        }

        foreach (var input in inputs)
        {
            TouchContactsReported++;
            if ((input.Flags & TouchEventUp) != 0)
            {
                _activeTouchContacts.Remove(input.Id);
            }
            else
            {
                _activeTouchContacts.Add(input.Id);
            }
        }

        if (_recognizer.Process(_activeTouchContacts.Count, DateTimeOffset.UtcNow))
        {
            FourFingerScreenTouchReleased?.Invoke(this, EventArgs.Empty);
        }

        CloseTouchInputHandle(lParam);
    }

    private void ResetContacts()
    {
        _activeTouchPointers.Clear();
        _activeTouchContacts.Clear();
        _recognizer.Reset();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterTouchWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterTouchWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetTouchInputInfo(
        IntPtr touchInputHandle,
        uint touchInputCount,
        [Out] TouchInput[] touchInputs,
        int touchInputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseTouchInputHandle(IntPtr touchInputHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetPointerType(uint pointerId, out uint pointerType);

    [StructLayout(LayoutKind.Sequential)]
    private struct TouchInput
    {
        public int X;
        public int Y;
        public IntPtr Device;
        public uint Id;
        public uint Flags;
        public uint Mask;
        public uint Time;
        public IntPtr ExtraInfo;
        public uint SizeX;
        public uint SizeY;
    }
}
