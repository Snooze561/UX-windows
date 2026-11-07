using System.IO;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace UXWindows.Wallpaper;

/// <summary>
/// Gère les fonds de chaque écran et les met en pause quand personne ne peut les voir
/// ou quand un jeu tourne en plein écran.
/// </summary>
public sealed class WallpaperManager : IDisposable
{
    private readonly AppConfig _config;
    private readonly Dictionary<string, WallpaperWindow> _windows = new();
    private readonly DispatcherTimer _watcher;
    private IntPtr _workerW;

    /// <summary>Pause forcée depuis le menu.</summary>
    public bool ManualPause { get; private set; }

    public WallpaperManager(AppConfig config)
    {
        _config = config;

        // Une vérification toutes les 2 secondes : coût négligeable.
        _watcher = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _watcher.Tick += (_, _) => UpdatePauseState();
    }

    public void Start()
    {
        _workerW = DesktopHost.GetWorkerW();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (_config.Wallpapers.TryGetValue(screen.DeviceName, out var path) && File.Exists(path))
            {
                Show(screen, path);
            }
        }
        _watcher.Start();
        UpdatePauseState();
    }

    public void SetWallpaper(Forms.Screen screen, string path)
    {
        _config.Wallpapers[screen.DeviceName] = path;
        _config.Save();

        if (_workerW == IntPtr.Zero) _workerW = DesktopHost.GetWorkerW();
        if (_windows.TryGetValue(screen.DeviceName, out var window)) window.Load(path);
        else Show(screen, path);
        UpdatePauseState();
    }

    public void ClearWallpaper(Forms.Screen screen)
    {
        _config.Wallpapers.Remove(screen.DeviceName);
        _config.Save();

        if (_windows.Remove(screen.DeviceName, out var window))
        {
            window.Close();
            DesktopHost.RefreshSystemWallpaper();
        }
    }

    public void ToggleManualPause()
    {
        ManualPause = !ManualPause;
        UpdatePauseState();
    }

    private void Show(Forms.Screen screen, string path)
    {
        var window = new WallpaperWindow(screen, _workerW, path);
        _windows[screen.DeviceName] = window;
        window.Show();
    }

    private void UpdatePauseState()
    {
        if (_windows.Count == 0) return;

        bool pauseAll = ManualPause || IsFullscreenAppRunning() || IsOnBattery();
        IntPtr coveredMonitor = pauseAll ? IntPtr.Zero : MonitorHiddenByForegroundWindow();

        foreach (var window in _windows.Values)
        {
            bool hidden = coveredMonitor != IntPtr.Zero &&
                          Native.MonitorFromWindow(new WindowInteropHelper(window).Handle,
                              Native.MONITOR_DEFAULTTONEAREST) == coveredMonitor;
            window.SetPaused(pauseAll || hidden);
        }
    }

    /// <summary>Jeu, vidéo ou présentation en plein écran, sur n'importe quel écran.</summary>
    private static bool IsFullscreenAppRunning()
    {
        if (Native.SHQueryUserNotificationState(out int state) == 0 && state is 2 or 3 or 4)
            return true;

        IntPtr fg = Native.GetForegroundWindow();
        if (!IsRealAppWindow(fg)) return false;

        Native.GetWindowRect(fg, out var rect);
        var info = MonitorInfoOf(fg);
        return rect.Left <= info.rcMonitor.Left && rect.Top <= info.rcMonitor.Top &&
               rect.Right >= info.rcMonitor.Right && rect.Bottom >= info.rcMonitor.Bottom;
    }

    /// <summary>
    /// Écran entièrement recouvert par la fenêtre active agrandie : son fond est invisible,
    /// inutile de le faire tourner.
    /// </summary>
    private static IntPtr MonitorHiddenByForegroundWindow()
    {
        IntPtr fg = Native.GetForegroundWindow();
        if (!IsRealAppWindow(fg)) return IntPtr.Zero;

        var placement = new Native.WINDOWPLACEMENT
        {
            length = System.Runtime.InteropServices.Marshal.SizeOf<Native.WINDOWPLACEMENT>()
        };
        if (!Native.GetWindowPlacement(fg, ref placement) || placement.showCmd != Native.SW_SHOWMAXIMIZED)
            return IntPtr.Zero;

        return Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST);
    }

    private static bool IsRealAppWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindowVisible(hwnd)) return false;
        string cls = Native.ClassNameOf(hwnd);
        // Le bureau et la barre des tâches ne comptent pas.
        return cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }

    private static Native.MONITORINFO MonitorInfoOf(IntPtr hwnd)
    {
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST), ref info);
        return info;
    }

    private static bool IsOnBattery() =>
        Forms.SystemInformation.PowerStatus.PowerLineStatus == Forms.PowerLineStatus.Offline;

    public void Dispose()
    {
        _watcher.Stop();
        bool hadAny = _windows.Count > 0;
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
        if (hadAny) DesktopHost.RefreshSystemWallpaper();
    }
}
