using System.IO;
using System.Windows;
using System.Windows.Controls;
using UXWindows.Core.Sensors;
using UXWindows.Core.Startup;
using UXWindows.Wallpaper;
using Forms = System.Windows.Forms;

namespace UXWindows;

/// <summary>
/// Fenêtre de réglages : chaque changement s'applique tout de suite et est enregistré.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly WallpaperManager _wallpapers;
    private readonly UtilityPanel _panel;
    private readonly Func<Forms.Screen?, string?> _pickFile;
    private bool _loading = true;

    public SettingsWindow(AppConfig config, WallpaperManager wallpapers, UtilityPanel panel, Func<Forms.Screen?, string?> pickFile)
    {
        _config = config;
        _wallpapers = wallpapers;
        _panel = panel;
        _pickFile = pickFile;
        InitializeComponent();

        BuildWallpaperRows();

        foreach (ComboBoxItem item in FpsBox.Items)
            if ((string)item.Tag == _config.MaxFps.ToString()) FpsBox.SelectedItem = item;

        ScreenBox.Items.Add(new ComboBoxItem { Content = "Automatique (écran de gauche)", Tag = "" });
        foreach (var screen in Forms.Screen.AllScreens.OrderBy(s => s.Bounds.Left))
        {
            var item = new ComboBoxItem { Content = Capitalize(ScreenHelper.Label(screen)), Tag = screen.DeviceName };
            ScreenBox.Items.Add(item);
            if (screen.DeviceName == _config.PanelScreen) ScreenBox.SelectedItem = item;
        }
        ScreenBox.SelectedIndex = ScreenBox.SelectedIndex < 0 ? 0 : ScreenBox.SelectedIndex;

        ShowSystemBox.IsChecked = _config.ShowSystem;
        ShowMusicBox.IsChecked = _config.ShowMusic;
        ShowClaudeBox.IsChecked = _config.ShowClaude;
        ShowFansBox.IsChecked = _config.ShowFans;

        GpuBox.Items.Add(new ComboBoxItem { Content = "Automatique (carte dédiée)", Tag = "" });
        foreach (var name in _panel.GpuNames.Union(_config.PanelGpu is null ? Array.Empty<string>() : new[] { _config.PanelGpu }))
        {
            var item = new ComboBoxItem { Content = name, Tag = name };
            GpuBox.Items.Add(item);
            if (name == _config.PanelGpu) GpuBox.SelectedItem = item;
        }
        GpuBox.SelectedIndex = GpuBox.SelectedIndex < 0 ? 0 : GpuBox.SelectedIndex;

        StartupBox.IsChecked = StartupManager.IsStartWithWindowsEnabled();
        RefreshCpuState();

        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        VersionText.Text = $"UX-windows {version?.ToString(3)}";

        _loading = false;
    }

    // ---------- Fonds d'écran ----------

    private void BuildWallpaperRows()
    {
        WallpaperRows.Children.Clear();
        // Chaque écran, puis « tous les écrans » : une seule image ou vidéo étalée sur tout le bureau.
        var targets = Forms.Screen.AllScreens.OrderBy(s => s.Bounds.Left).Cast<Forms.Screen?>().ToList();
        if (targets.Count > 1) targets.Add(null);
        foreach (var screen in targets)
        {
            _config.Wallpapers.TryGetValue(screen?.DeviceName ?? WallpaperManager.AllScreensKey, out var path);

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };

            var remove = new Button { Content = "Retirer", Style = (Style)FindResource("SmallButton"), IsEnabled = path is not null };
            remove.Click += (_, _) => { _wallpapers.ClearWallpaper(screen); BuildWallpaperRows(); };
            DockPanel.SetDock(remove, Dock.Right);

            var choose = new Button { Content = "Choisir…", Style = (Style)FindResource("SmallButton") };
            choose.Click += (_, _) =>
            {
                var file = _pickFile(screen);
                if (file is null) return;
                _wallpapers.SetWallpaper(screen, file);
                BuildWallpaperRows();
            };
            DockPanel.SetDock(choose, Dock.Right);

            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = Capitalize(ScreenHelper.Label(screen)), FontSize = 14 });
            text.Children.Add(new TextBlock
            {
                Text = path is not null ? Path.GetFileName(path)
                    : screen is not null && _wallpapers.SpannedWallpaper is not null ? "Utilise l'image de tous les écrans"
                    : screen is null ? "Aucune" : "Fond Windows",
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            row.Children.Add(remove);
            row.Children.Add(choose);
            row.Children.Add(text);
            WallpaperRows.Children.Add(row);
        }
    }

    private void Fps_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FpsBox.SelectedItem is not ComboBoxItem item) return;
        _config.MaxFps = int.Parse((string)item.Tag);
        _config.Save();
        _wallpapers.ReloadVideos();
    }

    // ---------- Panneau ----------

    private void Screen_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ScreenBox.SelectedItem is not ComboBoxItem item) return;
        string tag = (string)item.Tag;
        _config.PanelScreen = tag.Length == 0 ? null : tag;
        _config.Save();
        _panel.MoveTo(ScreenHelper.GetUtilityScreen(_config.PanelScreen));
    }

    private void Sections_Changed(object sender, RoutedEventArgs e)
    {
        _config.ShowSystem = ShowSystemBox.IsChecked == true;
        _config.ShowMusic = ShowMusicBox.IsChecked == true;
        _config.ShowClaude = ShowClaudeBox.IsChecked == true;
        _config.ShowFans = ShowFansBox.IsChecked == true;
        _config.Save();
        _panel.ApplySections(_config.ShowSystem, _config.ShowMusic, _config.ShowClaude, _config.ShowFans);
    }

    private void ResetCards_Click(object sender, RoutedEventArgs e)
    {
        _config.CardOrder = null;
        _config.Save();
        _panel.ApplyCardOrder(UtilityPanel.DefaultCardOrder);
    }

    private void Gpu_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || GpuBox.SelectedItem is not ComboBoxItem item) return;
        string tag = (string)item.Tag;
        _config.PanelGpu = tag.Length == 0 ? null : tag;
        _config.Save();
        _panel.SetGpu(_config.PanelGpu);
    }

    // ---------- Démarrage ----------

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupManager.SetStartWithWindows(StartupBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            Log.Write($"Démarrage avec Windows : {ex.Message}");
            StartupBox.IsChecked = StartupManager.IsStartWithWindowsEnabled();
        }
    }

    private async void Cpu_Changed(object sender, RoutedEventArgs e)
    {
        bool wanted = CpuBox.IsChecked == true;
        CpuBox.IsEnabled = false;
        CpuHint.Text = "En attente de l'autorisation de Windows…";

        if (wanted && !HardwareSensors.IsPawnIoInstalled())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://pawnio.eu/") { UseShellExecute = true });
        }
        else
        {
            await Task.Run(() => wanted ? SensorAutoStart.Enable() : SensorAutoStart.Disable());
        }

        CpuBox.IsEnabled = true;
        RefreshCpuState();
    }

    private void RefreshCpuState()
    {
        bool pawnIo = HardwareSensors.IsPawnIoInstalled();
        bool task = StartupManager.IsSensorTaskInstalled();
        CpuBox.IsChecked = pawnIo && task;
        CpuHint.Text = !pawnIo
            ? "Il faut d'abord le pilote gratuit PawnIO : dans Terminal, tape « winget install namazso.PawnIO »."
            : task
                ? "Activé : Windows ne demandera plus d'autorisation."
                : "Windows demandera une autorisation administrateur, une seule fois.";
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];
}
