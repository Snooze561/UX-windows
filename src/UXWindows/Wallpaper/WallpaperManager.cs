using System.IO;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace UXWindows.Wallpaper;

/// <summary>
/// Gère les fonds de chaque écran et les met en pause quand un jeu ou une vidéo
/// tourne en plein écran, sur batterie, ou à la demande.
/// </summary>
public sealed class WallpaperManager : IDisposable
{
    private static readonly string[] VideoExtensions = [".mp4", ".wmv", ".mov", ".m4v", ".mkv", ".avi"];

    private readonly AppConfig _config;
    private readonly Dictionary<string, VideoWallpaper> _videos = new();
    private readonly DispatcherTimer _watcher;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    /// <summary>Message à montrer à l'utilisateur (erreur de lecture, etc.).</summary>
    public event Action<string>? Notify;

    /// <summary>Pause forcée depuis le menu.</summary>
    public bool ManualPause { get; private set; }

    /// <summary>Phrase d'état affichée dans le menu de l'icône.</summary>
    public string Status { get; private set; } = "Aucun fond animé";

    public WallpaperManager(AppConfig config)
    {
        _config = config;

        // Une vérification toutes les 2 secondes : coût négligeable.
        _watcher = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _watcher.Tick += (_, _) => UpdatePauseState();
    }

    public static bool IsVideo(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public void Start()
    {
        Log.Write($"Démarrage, Windows {Environment.OSVersion.Version}");
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (_config.Wallpapers.TryGetValue(screen.DeviceName, out var path) && File.Exists(path) && IsVideo(path))
                StartVideo(screen, path);
        }
        _watcher.Start();
        UpdatePauseState();
    }

    public void SetWallpaper(Forms.Screen screen, string path)
    {
        StopVideo(screen);

        if (IsVideo(path))
        {
            if (!StartVideo(screen, path)) return;
        }
        else
        {
            try
            {
                string? previous = ImageWallpaper.Set(screen, path);
                if (previous is not null && !_config.OriginalWallpapers.ContainsKey(screen.DeviceName))
                    _config.OriginalWallpapers[screen.DeviceName] = previous;
            }
            catch (Exception ex)
            {
                Log.Write($"Échec du fond image : {ex}");
                Notify?.Invoke($"Impossible de mettre cette image en fond : {ex.Message}");
                return;
            }
        }

        _config.Wallpapers[screen.DeviceName] = path;
        _config.Save();
        UpdatePauseState();
    }

    public void ClearWallpaper(Forms.Screen screen)
    {
        StopVideo(screen);
        _config.Wallpapers.Remove(screen.DeviceName);

        if (_config.OriginalWallpapers.Remove(screen.DeviceName, out var original) && File.Exists(original))
        {
            try { ImageWallpaper.Set(screen, original); }
            catch (Exception ex) { Log.Write($"Échec du retour au fond d'origine : {ex}"); }
        }
        _config.Save();
        UpdatePauseState();
    }

    public void ToggleManualPause()
    {
        ManualPause = !ManualPause;
        UpdatePauseState();
    }

    private bool StartVideo(Forms.Screen screen, string path)
    {
        var layer = DesktopHost.Find();
        if (layer is null)
        {
            Notify?.Invoke("Le bureau Windows est introuvable, réessaie dans quelques secondes.");
            return false;
        }

        try
        {
            var video = new VideoWallpaper(screen, layer, path, reason =>
                _dispatcher.InvokeAsync(() =>
                {
                    Log.Write($"Lecture impossible ({path}) : {reason}");
                    Notify?.Invoke($"Vidéo illisible : {reason}.");
                }));
            _videos[screen.DeviceName] = video;
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Échec du fond vidéo : {ex}");
            Notify?.Invoke($"Impossible de lancer la vidéo : {ex.Message}");
            return false;
        }
    }

    private void StopVideo(Forms.Screen screen)
    {
        if (_videos.Remove(screen.DeviceName, out var video))
        {
            video.Dispose();
            DesktopHost.RefreshSystemWallpaper();
        }
    }

    private void UpdatePauseState()
    {
        if (_videos.Count == 0)
        {
            Status = "Aucun fond animé";
            return;
        }

        string? reason =
            ManualPause ? "pause manuelle" :
            IsFullscreenAppRunning() ? "application en plein écran" :
            IsOnBattery() ? "sur batterie" :
            null;

        foreach (var video in _videos.Values) video.SetPaused(reason is not null);

        Status = reason is null ? "Fonds animés : en lecture" : $"Fonds animés : en pause ({reason})";
    }

    /// <summary>Jeu, vidéo ou présentation en plein écran, sur n'importe quel écran.</summary>
    private static bool IsFullscreenAppRunning()
    {
        if (Native.SHQueryUserNotificationState(out int state) == 0 && state is 3 or 4)
            return true;

        IntPtr fg = Native.GetForegroundWindow();
        if (!IsRealAppWindow(fg)) return false;

        Native.GetWindowRect(fg, out var rect);
        var info = MonitorInfoOf(fg);
        return rect.Left <= info.rcMonitor.Left && rect.Top <= info.rcMonitor.Top &&
               rect.Right >= info.rcMonitor.Right && rect.Bottom >= info.rcMonitor.Bottom;
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
        bool hadAny = _videos.Count > 0;
        foreach (var video in _videos.Values) video.Dispose();
        _videos.Clear();
        if (hadAny) DesktopHost.RefreshSystemWallpaper();
    }
}
