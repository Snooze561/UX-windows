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
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Une seule instance à la fois.
        _singleInstance = new Mutex(true, "UXWindows.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        var config = AppConfig.Load();

        _wallpapers = new WallpaperManager(config);
        _wallpapers.Start();

        _panel = new UtilityPanel(ScreenHelper.GetUtilityScreen());
        _panel.Show();

        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();

        // Un sous-menu par écran, de gauche à droite.
        foreach (var screen in Forms.Screen.AllScreens.OrderBy(s => s.Bounds.Left))
        {
            var item = new Forms.ToolStripMenuItem($"Fond : {ScreenHelper.Label(screen)}");
            item.DropDownItems.Add("Choisir une vidéo ou une image…", null, (_, _) => PickWallpaper(screen));
            item.DropDownItems.Add("Retirer (fond Windows)", null, (_, _) => _wallpapers?.ClearWallpaper(screen));
            menu.Items.Add(item);
        }

        _pauseItem = new Forms.ToolStripMenuItem("Mettre les fonds en pause", null, (_, _) => ToggleWallpaperPause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Afficher / masquer le panneau", null, (_, _) => TogglePanel());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "UX-windows",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => TogglePanel();
    }

    private void PickWallpaper(Forms.Screen screen)
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = $"Fond d'écran : {ScreenHelper.Label(screen)}",
            Filter = "Vidéos et images|*.mp4;*.wmv;*.mov;*.m4v;*.jpg;*.jpeg;*.png;*.bmp|" +
                     "Vidéos (animé)|*.mp4;*.wmv;*.mov;*.m4v|" +
                     "Images (fixe)|*.jpg;*.jpeg;*.png;*.bmp",
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _wallpapers?.SetWallpaper(screen, dialog.FileName);
        }
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
