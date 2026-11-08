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

    /// <summary>Clé utilisée dans les réglages pour une image ou vidéo étalée sur tous les écrans.</summary>
    public const string AllScreensKey = "*";

    /// <summary>Fond étalé sur tous les écrans, s'il y en a un.</summary>
    public string? SpannedWallpaper => _config.Wallpapers.GetValueOrDefault(AllScreensKey);

    public void Start()
    {
        Log.Write($"Démarrage, Windows {Environment.OSVersion.Version}");
        if (SpannedWallpaper is { } spanned)
        {
            if (File.Exists(spanned) && IsVideo(spanned)) StartVideo(null, spanned);
        }
        else
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                if (_config.Wallpapers.TryGetValue(screen.DeviceName, out var path) && File.Exists(path) && IsVideo(path))
                    StartVideo(screen, path);
            }
        }
        _watcher.Start();
        UpdatePauseState();
    }

    /// <summary>Change le fond d'un écran, ou de tous les écrans à la fois (une seule image) si <paramref name="screen"/> est null.</summary>
    public void SetWallpaper(Forms.Screen? screen, string path)
    {
        if (screen is null)
        {
            // Une seule image sur tout le bureau : elle remplace les fonds de chaque écran.
            StopAllVideos();
            foreach (var key in _config.Wallpapers.Keys.ToList()) _config.Wallpapers.Remove(key);
        }
        else
        {
            StopVideo(screen.DeviceName);
            if (_config.Wallpapers.Remove(AllScreensKey)) StopVideo(AllScreensKey);
        }

        if (IsVideo(path))
        {
            if (!StartVideo(screen, path)) return;
        }
        else
        {
            try
            {
                string key = screen?.DeviceName ?? AllScreensKey;
                string? previous = screen is null ? ImageWallpaper.SetSpanned(path) : ImageWallpaper.Set(screen, path);
                if (previous is not null && !_config.OriginalWallpapers.ContainsKey(key))
                    _config.OriginalWallpapers[key] = previous;
            }
            catch (Exception ex)
            {
                Log.Write($"Échec du fond image : {ex}");
                Notify?.Invoke($"Impossible de mettre cette image en fond : {ex.Message}");
                return;
            }
        }

        _config.Wallpapers[screen?.DeviceName ?? AllScreensKey] = path;
        _config.Save();
        UpdatePauseState();
    }

    /// <summary>Retire le fond d'un écran, ou le fond étalé si <paramref name="screen"/> est null.</summary>
    public void ClearWallpaper(Forms.Screen? screen)
    {
        string key = screen?.DeviceName ?? AllScreensKey;
        StopVideo(key);
        _config.Wallpapers.Remove(key);

        if (_config.OriginalWallpapers.Remove(key, out var original) && File.Exists(original))
        {
            try
            {
                if (screen is null) ImageWallpaper.SetAllFilled(original);
                else ImageWallpaper.Set(screen, original);
            }
            catch (Exception ex) { Log.Write($"Échec du retour au fond d'origine : {ex}"); }
        }
        _config.Save();
        UpdatePauseState();
    }

    /// <summary>Relance les vidéos (après un changement de réglage comme les images par seconde).</summary>
    public void ReloadVideos()
    {
        foreach (var key in _videos.Keys.ToList())
        {
            StopVideo(key);
            if (!_config.Wallpapers.TryGetValue(key, out var path) || !File.Exists(path)) continue;
            var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == key);
            if (key == AllScreensKey || screen is not null) StartVideo(screen, path);
        }
        UpdatePauseState();
    }

    public void ToggleManualPause()
    {
        ManualPause = !ManualPause;
        UpdatePauseState();
    }

    /// <summary>Lance une vidéo sur un écran, ou sur tout le bureau si <paramref name="screen"/> est null.</summary>
    private bool StartVideo(Forms.Screen? screen, string path)
    {
        var layer = DesktopHost.Find();
        if (layer is null)
        {
            Notify?.Invoke("Le bureau Windows est introuvable, réessaie dans quelques secondes.");
            return false;
        }

        string key = screen?.DeviceName ?? AllScreensKey;
        var bounds = screen?.Bounds ?? Forms.SystemInformation.VirtualScreen;
        try
        {
            var video = new VideoWallpaper(screen?.DeviceName ?? "tous les écrans", bounds, layer, path, _config.MaxFps, reason =>
                _dispatcher.InvokeAsync(() =>
                {
                    Log.Write($"Lecture impossible ({path}) : {reason}");
                    Notify?.Invoke($"Vidéo illisible : {reason}.");
                }));
            _videos[key] = video;
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Échec du fond vidéo : {ex}");
            Notify?.Invoke($"Impossible de lancer la vidéo : {ex.Message}");
            return false;
        }
    }

    private void StopVideo(string key)
    {
        if (_videos.Remove(key, out var video))
        {
            video.Dispose();
            DesktopHost.RefreshSystemWallpaper();
        }
    }

    private void StopAllVideos()
    {
        foreach (var key in _videos.Keys.ToList()) StopVideo(key);
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
