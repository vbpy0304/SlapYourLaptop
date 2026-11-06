using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TapOS.Services;

public interface ITouchpadGestureProvider : IDisposable
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }
    bool IsRunning { get; }
    event EventHandler? FourFingerTapReleased;
    void Start();
    void Stop();
}

public sealed class RawInputTouchpadGestureProvider : ITouchpadGestureProvider
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiPreparsedData = 0x20000005;
    private const uint RimTypeHid = 2;
    private const uint RidevInputSink = 0x00000100;
    private const uint HidpInput = 0;
    private const uint HidpStatusSuccess = 0x00110000;
    private const ushort DigitizerUsagePage = 0x000D;
    private const ushort TouchPadUsage = 0x0005;
    private readonly HwndSource _source;
    private readonly FourFingerGestureRecognizer _recognizer;
    private bool _registered;
    private bool _disposed;

    public RawInputTouchpadGestureProvider(HwndSource source, int maxDurationMilliseconds)
    {
        _source = source;
        _recognizer = new FourFingerGestureRecognizer(TimeSpan.FromMilliseconds(maxDurationMilliseconds));
        _source.AddHook(WndProc);
        _registered = RegisterRawInputDevices(
            [new RawInputDevice
            {
                UsagePage = DigitizerUsagePage,
                Usage = TouchPadUsage,
                Flags = RidevInputSink,
                Target = _source.Handle
            }],
            1,
            (uint)Marshal.SizeOf<RawInputDevice>());

        if (!_registered)
        {
            AvailabilityMessage = $"Precision Touchpad Raw Input registration failed (Windows error {Marshal.GetLastWin32Error()}).";
        }
        else if (!TryGetPrecisionTouchpadDiagnostics(out var diagnostics))
        {
            AvailabilityMessage = $"Raw Input registered, but no Precision Touchpad HID collection (usage page 0x0D, usage 0x05) was found. HID collections seen: {diagnostics}";
        }
        else
        {
            IsAvailable = true;
            AvailabilityMessage = "Precision Touchpad Raw Input is registered. Four-contact release recognition is available.";
        }
    }

    public bool IsAvailable { get; }
    public string AvailabilityMessage { get; }
    public bool IsRunning { get; private set; }
    public int RawInputMessagesReceived { get; private set; }
    public int ReportsParsed { get; private set; }
    public event EventHandler? FourFingerTapReleased;

    public void Start()
    {
        if (IsAvailable)
        {
            IsRunning = true;
        }
    }

    public void Stop()
    {
        IsRunning = false;
        _recognizer.Reset();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _source.RemoveHook(WndProc);
        if (_registered)
        {
            RegisterRawInputDevices(
                [new RawInputDevice
                {
                    UsagePage = DigitizerUsagePage,
                    Usage = TouchPadUsage,
                    Flags = 0x00000001,
                    Target = IntPtr.Zero
                }],
                1,
                (uint)Marshal.SizeOf<RawInputDevice>());
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmInput)
        {
            RawInputMessagesReceived++;
            if (IsRunning && TryGetContactCount(lParam, out var contactCount))
            {
                ReportsParsed++;
                if (_recognizer.Process(contactCount, DateTimeOffset.UtcNow))
                {
                    FourFingerTapReleased?.Invoke(this, EventArgs.Empty);
                }
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    private static bool TryGetPrecisionTouchpadDiagnostics(out string diagnostics)
    {
        var seen = new List<string>();
        diagnostics = "none";
        uint count = 0;
        var itemSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        if (GetRawInputDeviceList(null, ref count, itemSize) != 0 || count == 0)
        {
            return false;
        }

        var devices = new RawInputDeviceList[count];
        if (GetRawInputDeviceList(devices, ref count, itemSize) != count)
        {
            return false;
        }

        foreach (var device in devices.Where(device => device.Type == RimTypeHid))
        {
            uint size = (uint)Marshal.SizeOf<RidDeviceInfo>();
            var info = new RidDeviceInfo { Size = size };
            if (GetRawInputDeviceInfo(device.Handle, 0x2000000B, ref info, ref size) != unchecked((uint)-1))
            {
                seen.Add($"0x{info.Hid.UsagePage:X2}/0x{info.Hid.Usage:X2}");
                if (info.Hid.UsagePage == DigitizerUsagePage && info.Hid.Usage == TouchPadUsage)
                {
                    diagnostics = string.Join(", ", seen.Distinct());
                    return true;
                }
            }
        }

        diagnostics = seen.Count == 0 ? "none" : string.Join(", ", seen.Distinct());
        return false;
    }

    private static bool TryGetContactCount(IntPtr rawInputHandle, out int contactCount)
    {
        contactCount = 0;
        uint rawInputSize = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref rawInputSize, headerSize) != 0 ||
            rawInputSize < headerSize + (uint)Marshal.SizeOf<RawHid>())
        {
            return false;
        }

        var rawInput = Marshal.AllocHGlobal((int)rawInputSize);
        try
        {
            if (GetRawInputData(rawInputHandle, RidInput, rawInput, ref rawInputSize, headerSize) != rawInputSize)
            {
                return false;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(rawInput);
            if (header.Type != RimTypeHid)
            {
                return false;
            }

            var hid = Marshal.PtrToStructure<RawHid>(IntPtr.Add(rawInput, Marshal.SizeOf<RawInputHeader>()));
            if (hid.Size == 0 || hid.Count == 0)
            {
                return false;
            }

            var rawHidBytes = new byte[hid.Size * hid.Count];
            var rawBytes = new byte[(int)rawInputSize];
            Marshal.Copy(rawInput, rawBytes, 0, rawBytes.Length);
            Buffer.BlockCopy(rawBytes, rawBytes.Length - rawHidBytes.Length, rawHidBytes, 0, rawHidBytes.Length);

            uint preparsedSize = 0;
            if (GetRawInputDeviceInfo(header.Device, RidiPreparsedData, IntPtr.Zero, ref preparsedSize) != 0 ||
                preparsedSize == 0)
            {
                return false;
            }

            var preparsed = Marshal.AllocHGlobal((int)preparsedSize);
            try
            {
                if (GetRawInputDeviceInfo(header.Device, RidiPreparsedData, preparsed, ref preparsedSize) != preparsedSize ||
                    HidP_GetCaps(preparsed, out var caps) != HidpStatusSuccess)
                {
                    return false;
                }

                ushort valueCapsLength = caps.NumberInputValueCaps;
                var valueCaps = new HidpValueCaps[valueCapsLength];
                if (valueCapsLength == 0 ||
                    HidP_GetValueCaps(HidpInput, valueCaps, ref valueCapsLength, preparsed) != HidpStatusSuccess)
                {
                    return false;
                }

                var report = Marshal.AllocHGlobal((int)hid.Size);
                try
                {
                    for (var reportIndex = 0; reportIndex < hid.Count; reportIndex++)
                    {
                        Marshal.Copy(rawHidBytes, (int)(reportIndex * hid.Size), report, (int)hid.Size);
                        foreach (var valueCap in valueCaps)
                        {
                            if (valueCap.UsagePage != DigitizerUsagePage ||
                                valueCap.Usage != 0x54 ||
                                valueCap.LinkCollection != 0)
                            {
                                continue;
                            }

                            if (HidP_GetUsageValue(
                                    HidpInput,
                                    valueCap.UsagePage,
                                    valueCap.LinkCollection,
                                    valueCap.Usage,
                                    out var value,
                                    preparsed,
                                    report,
                                    hid.Size) == HidpStatusSuccess)
                            {
                                contactCount = (int)value;
                                return true;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(report);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(preparsed);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(rawInput);
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] devices,
        uint deviceCount,
        uint deviceSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? devices,
        ref uint deviceCount,
        uint deviceSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        ref RidDeviceInfo data,
        ref uint size);

    [DllImport("hid.dll")]
    private static extern uint HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll")]
    private static extern uint HidP_GetValueCaps(
        uint reportType,
        [Out] HidpValueCaps[] valueCaps,
        ref ushort valueCapsLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern uint HidP_GetUsageValue(
        uint reportType,
        ushort usagePage,
        ushort linkCollection,
        ushort usage,
        out uint usageValue,
        IntPtr preparsedData,
        IntPtr report,
        uint reportLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public IntPtr Handle;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawHid
    {
        public uint Size;
        public uint Count;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfo
    {
        public uint Size;
        public uint Type;
        public RidDeviceInfoHid Hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfoHid
    {
        public uint VendorId;
        public uint ProductId;
        public uint Version;
        public ushort UsagePage;
        public ushort Usage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpValueCaps
    {
        public ushort UsagePage;
        public byte ReportId;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsAlias;
        public ushort BitField;
        public ushort LinkCollection;
        public ushort LinkUsage;
        public ushort LinkUsagePage;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsRange;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsStringRange;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsDesignatorRange;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsAbsolute;
        [MarshalAs(UnmanagedType.I1)]
        public bool HasNull;
        public byte Reserved;
        public ushort BitSize;
        public ushort ReportCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public ushort[] Reserved2;
        public uint UnitsExp;
        public uint Units;
        public int LogicalMin;
        public int LogicalMax;
        public int PhysicalMin;
        public int PhysicalMax;
        public ushort UsageMin;
        public ushort UsageMax;
        public ushort StringMin;
        public ushort StringMax;
        public ushort DesignatorMin;
        public ushort DesignatorMax;
        public ushort DataIndexMin;
        public ushort DataIndexMax;
        public ushort Usage => UsageMin;
    }
}

public sealed class UnsupportedTouchpadGestureProvider : ITouchpadGestureProvider
{
    public bool IsAvailable => false;
    public bool IsRunning { get; private set; }
    public string AvailabilityMessage =>
        "Precision Touchpad Raw Input is unavailable. Check that the device exposes a HID touchpad collection (usage page 0x0D, usage 0x05).";

#pragma warning disable CS0067
    public event EventHandler? FourFingerTapReleased;
#pragma warning restore CS0067

    public void Start() => IsRunning = false;
    public void Stop() => IsRunning = false;
    public void Dispose() => Stop();
}
