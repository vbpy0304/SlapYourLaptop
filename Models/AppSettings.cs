using TapOS.Services;

namespace TapOS.Models;

public sealed class AppSettings
{
    public bool IsEnabled { get; set; }
    public bool EnableScreenTouchOverlay { get; set; }
    public int MaxGestureDurationMilliseconds { get; set; } = 500;
    public string SoundFolder { get; set; } = SoundFolderDefaults.GetDefaultPath();
}
