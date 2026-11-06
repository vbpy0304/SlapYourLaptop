using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
using TapOS.Models;
using TapOS.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TapOS;

public partial class MainWindow : Window, IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly SoundLibrary _soundLibrary = new();
    private ITouchpadGestureProvider _touchpadProvider = new UnsupportedTouchpadGestureProvider();
    private ScreenTouchOverlayProvider? _screenTouchOverlay;
    private AppSettings _settings;
    private GlobalHotkeyService? _hotkeyService;
    private NotifyIcon? _notifyIcon;
    private bool _allowClose;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        SoundFolderTextBox.Text = _settings.SoundFolder;
        DurationTextBox.Text = _settings.MaxGestureDurationMilliseconds.ToString();
        ScreenTouchOverlayCheckBox.IsChecked = _settings.EnableScreenTouchOverlay;
        _touchpadProvider.FourFingerTapReleased += TouchpadProvider_FourFingerTapReleased;
        Loaded += MainWindow_Loaded;
    }

    public void InitializeTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TapOS",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Enable / disable", null, (_, _) => ToggleEnabled());
        menu.Items.Add("Test sound", null, (_, _) => TestSound());
        menu.Items.Add("Reload sounds", null, (_, _) => ReloadSounds());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _allowClose = true;
            Application.Current.Shutdown();
        });
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _touchpadProvider.Dispose();
        _touchpadProvider = new RawInputTouchpadGestureProvider(source, _settings.MaxGestureDurationMilliseconds);
        _touchpadProvider.FourFingerTapReleased += TouchpadProvider_FourFingerTapReleased;
        _screenTouchOverlay = new ScreenTouchOverlayProvider(_settings.MaxGestureDurationMilliseconds);
        _screenTouchOverlay.FourFingerScreenTouchReleased += ScreenTouchOverlay_FourFingerScreenTouchReleased;
        _hotkeyService = new GlobalHotkeyService(source);
        _hotkeyService.Pressed += (_, _) => Dispatcher.Invoke(ToggleEnabled);
        _hotkeyService.Register();
        if (_settings.IsEnabled)
        {
            _touchpadProvider.Start();
            if (_settings.EnableScreenTouchOverlay)
            {
                _screenTouchOverlay?.Start();
            }
        }
        ReloadSounds();
        UpdateStatus();
    }

    private void ToggleEnabled()
    {
        _settings.IsEnabled = !_settings.IsEnabled;
        if (_settings.IsEnabled)
        {
            _touchpadProvider.Start();
            if (_settings.EnableScreenTouchOverlay)
            {
                _screenTouchOverlay?.Start();
            }
        }
        else
        {
            _touchpadProvider.Stop();
            _screenTouchOverlay?.Stop();
        }

        _settingsService.Save(_settings);
        UpdateStatus();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => ToggleEnabled();

    private void TouchpadProvider_FourFingerTapReleased(object? sender, EventArgs e)
    {
        if (_settings.IsEnabled)
        {
            TestSound();
        }
    }

    private void ScreenTouchOverlay_FourFingerScreenTouchReleased(object? sender, EventArgs e)
    {
        if (_settings.IsEnabled)
        {
            TestSound();
        }
    }

    private void TestSound()
    {
        if (!_soundLibrary.TryPlayRandom(out _, out var error) && error is not null)
        {
            MessageBox.Show(this, error, "TapOS", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReloadSounds()
    {
        var count = _soundLibrary.Reload(_settings.SoundFolder);
        UpdateStatus();
        if (count == 0 && _soundLibrary.LastError is not null)
        {
            SoundStatusText.Text = $"Audio path: {_soundLibrary.LastError}";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationTextBox.Text, out var duration) || duration < 1 || duration > 60_000)
        {
            MessageBox.Show(this, "Duration must be a whole number between 1 and 60000 ms.", "TapOS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.SoundFolder = SoundFolderTextBox.Text.Trim();
        _settings.EnableScreenTouchOverlay = ScreenTouchOverlayCheckBox.IsChecked == true;
        _settings.MaxGestureDurationMilliseconds = duration;
        _settingsService.Save(_settings);
        if (_settings.IsEnabled && _settings.EnableScreenTouchOverlay)
        {
            _screenTouchOverlay?.Start();
        }
        else
        {
            _screenTouchOverlay?.Stop();
        }
        ReloadSounds();
        Hide();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder containing .wav sounds",
            SelectedPath = SoundFolderTextBox.Text
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SoundFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void TestSoundButton_Click(object sender, RoutedEventArgs e) => TestSound();
    private void ReloadButton_Click(object sender, RoutedEventArgs e) => ReloadSounds();

    private void UpdateStatus()
    {
        var hotkey = _hotkeyService?.IsRegistered == true ? "Win+Alt+M registered" : _hotkeyService?.Error ?? "Registering Win+Alt+M...";
        StatusText.Text = $"{(_settings.IsEnabled ? "Enabled" : "Paused")} | {_soundLibrary.SoundPaths.Count} sound(s) loaded | {hotkey}";
        ToggleButton.Content = _settings.IsEnabled ? "Pause TapOS now" : "Enable TapOS now";
        SoundStatusText.Text = _soundLibrary.LastError is null
            ? "Audio path: sound library loaded."
            : $"Audio path: {_soundLibrary.LastError}";
        ApiStatusText.Text = $"Touchpad path: {_touchpadProvider.AvailabilityMessage} Raw Input messages: {(_touchpadProvider as RawInputTouchpadGestureProvider)?.RawInputMessagesReceived ?? 0}, parsed reports: {(_touchpadProvider as RawInputTouchpadGestureProvider)?.ReportsParsed ?? 0}.";
        OverlayStatusText.Text = _screenTouchOverlay?.IsRunning == true
            ? $"Screen-touch path: active fullscreen overlay; HWND ready: {_screenTouchOverlay.IsWindowHandleReady}, WM_TOUCH registered: {_screenTouchOverlay.IsTouchWindowRegistered}. WM_TOUCH messages: {_screenTouchOverlay.TouchMessagesReceived}, contacts reported: {_screenTouchOverlay.TouchContactsReported}, WM_POINTER messages: {_screenTouchOverlay.PointerMessagesReceived}, active contacts: {_screenTouchOverlay.ActiveTouchContacts}."
            : _settings.EnableScreenTouchOverlay
                ? "Screen-touch path: configured but paused until Win+Alt+M enables TapOS."
                : "Screen-touch path: disabled.";
    }

    private void ShowSettings()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdateStatus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _touchpadProvider.Dispose();
        _screenTouchOverlay?.Dispose();
        _hotkeyService?.Dispose();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
