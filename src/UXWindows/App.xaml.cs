using System.Windows;
using UXWindows.Wallpaper;
using Forms = System.Windows.Forms;

namespace UXWindows;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private UtilityPanel? _panel;
    private WallpaperManager? _wallpapers;
    private Forms.ToolStripMenuItem? _pauseItem;
    private AppConfig? _config;
    private SettingsWindow? _settings;
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Mode « assistant capteurs » lancé en administrateur par l'app elle-même.
        if (e.Args.Length >= 2 && e.Args[0] == UXWindows.Core.Sensors.ElevatedSensorBridge.HelperArgument)
        {
            string parent = e.Args[1];
            Task.Run(() => UXWindows.Core.Sensors.ElevatedSensorBridge.RunHelper(parent))
                .ContinueWith(_ => Dispatcher.Invoke(Shutdown));
            return;
        }

        // Une seule instance à la fois.
        _singleInstance = new Mutex(true, UXWindows.Core.Sensors.ElevatedSensorBridge.MainAppMutex, out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _config = AppConfig.Load();

        CreateTrayIcon();

        _wallpapers = new WallpaperManager(_config);
        _wallpapers.Notify += ShowNotification;
        _wallpapers.Start();

        _panel = new UtilityPanel(ScreenHelper.GetUtilityScreen(_config.PanelScreen));
        _panel.ApplySections(_config.ShowSystem, _config.ShowMusic, _config.ShowClaude);
        _panel.Show();

        // Si l'app a été déplacée (nouvelle installation), le démarrage avec Windows suit.
        try { UXWindows.Core.Startup.StartupManager.RefreshStartWithWindowsPath(); }
        catch (Exception ex) { Log.Write($"Démarrage avec Windows : {ex.Message}"); }

        // Température CPU : relance l'assistant administrateur sans demande, s'il a été activé.
        Task.Run(SensorAutoStart.StartIfInstalled);
    }

    private void OpenSettings()
    {
        if (_config is null || _wallpapers is null || _panel is null) return;
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_config, _wallpapers, _panel, PickWallpaperFile);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Réglages…", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());

        // Un sous-menu par écran, de gauche à droite, puis « tous les écrans » (une seule image étalée).
        var targets = Forms.Screen.AllScreens.OrderBy(s => s.Bounds.Left).Cast<Forms.Screen?>().ToList();
        if (targets.Count > 1) targets.Add(null);
        foreach (var screen in targets)
        {
            var item = new Forms.ToolStripMenuItem($"Fond : {ScreenHelper.Label(screen)}");
            item.DropDownItems.Add("Choisir une vidéo ou une image…", null, (_, _) => PickWallpaper(screen));
            item.DropDownItems.Add("Retirer (fond Windows)", null, (_, _) => _wallpapers?.ClearWallpaper(screen));
            menu.Items.Add(item);
        }

        var statusItem = new Forms.ToolStripMenuItem { Enabled = false };
        menu.Items.Add(statusItem);
        menu.Opening += (_, _) => statusItem.Text = _wallpapers?.Status ?? "";

        _pauseItem = new Forms.ToolStripMenuItem("Mettre les fonds en pause", null, (_, _) => ToggleWallpaperPause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Afficher / masquer le panneau", null, (_, _) => TogglePanel());
        menu.Items.Add("Ouvrir le journal (en cas de problème)", null, (_, _) => OpenLog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "UX-windows",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => TogglePanel();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon)
                return icon;
        }
        catch (Exception)
        {
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void PickWallpaper(Forms.Screen? screen)
    {
        if (PickWallpaperFile(screen) is { } file) _wallpapers?.SetWallpaper(screen, file);
    }

    private static string? PickWallpaperFile(Forms.Screen? screen)
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = $"Fond d'écran : {ScreenHelper.Label(screen)}",
            Filter = "Vidéos et images|*.mp4;*.wmv;*.mov;*.m4v;*.mkv;*.avi;*.jpg;*.jpeg;*.png;*.bmp|" +
                     "Vidéos (animé)|*.mp4;*.wmv;*.mov;*.m4v;*.mkv;*.avi|" +
                     "Images (fixe)|*.jpg;*.jpeg;*.png;*.bmp",
        };
        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.FileName : null;
    }

    private void ShowNotification(string message)
    {
        _trayIcon?.ShowBalloonTip(5000, "UX-windows", message, Forms.ToolTipIcon.Warning);
    }

    private static void OpenLog()
    {
        Log.Write("Journal ouvert depuis le menu");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
    }

    private void ToggleWallpaperPause()
    {
        if (_wallpapers is null || _pauseItem is null) return;
        _wallpapers.ToggleManualPause();
        _pauseItem.Checked = _wallpapers.ManualPause;
    }

    private void TogglePanel()
    {
        if (_panel is null) return;
        if (_panel.IsVisible) _panel.Hide();
        else _panel.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wallpapers?.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
