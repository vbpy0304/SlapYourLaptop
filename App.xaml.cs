using System.Windows;

namespace TapOS;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "TapOS.SingleInstance.7A1D5A5D", out var createdNew);
        if (!createdNew)
        {
            System.Windows.Application.Current.Shutdown();
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.InitializeTray();
        _mainWindow.Show();
        _mainWindow.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
