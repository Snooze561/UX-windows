using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UXWindows.Core.Claude;
using UXWindows.Core.Fans;
using UXWindows.Core.Music;
using UXWindows.Core.Sensors;
using Forms = System.Windows.Forms;

namespace UXWindows;

/// <summary>
/// Panneau ancré sur toute la hauteur de l'écran utilitaire :
/// horloge, capteurs CPU / GPU, musique en cours, sessions Claude Code.
/// </summary>
public partial class UtilityPanel : Window
{
    /// <summary>Largeur en unités indépendantes de la résolution (mise à l'échelle Windows prise en compte).</summary>
    private const int PanelWidthDip = 380;

    private static readonly CultureInfo French = new("fr-FR");

    private Forms.Screen _screen;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _sensorTimer;
    private readonly DispatcherTimer _claudeTimer;
    private readonly DispatcherTimer _fanTimer;
    private readonly EzfanClient _ezfan = new();
    private bool _readingFans;
    private readonly ObservableCollection<ClaudeRow> _claudeRows = new();
    private readonly MusicService _music = new();

    private HardwareSensors? _sensors;
    private bool _readingSensors;
    private bool _askedForAdmin;

    public UtilityPanel(Forms.Screen screen, AppConfig config)
    {
        _screen = screen;
        _config = config;
        InitializeComponent();
        ClaudeList.ItemsSource = _claudeRows;

        // Horloge : une fois par seconde.
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();

        // Capteurs : toutes les 2 secondes, lus hors du thread de l'interface.
        _sensorTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _sensorTimer.Tick += async (_, _) => await UpdateSensorsAsync();

        // Sessions Claude Code : toutes les 5 secondes.
        _claudeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
        _claudeTimer.Tick += async (_, _) => await UpdateClaudeAsync();

        // Ventilateurs (EZFAN) : toutes les 2 secondes.
        _fanTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _fanTimer.Tick += async (_, _) => await UpdateFansAsync();

        SourceInitialized += (_, _) => PlaceOnScreen();
        Loaded += async (_, _) => await StartWidgetsAsync();
        IsVisibleChanged += (_, _) => SetTimersRunning(IsVisible);
        Closed += (_, _) =>
        {
            SetTimersRunning(false);
            _music.Dispose();
            _ezfan.Dispose();
            _sensors?.Dispose();
        };
    }

    /// <summary>Déplace le panneau sur un autre écran.</summary>
    public void MoveTo(Forms.Screen screen)
    {
        _screen = screen;
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero) PlaceOnScreen();
    }

    /// <summary>Affiche ou masque chaque section selon les réglages.</summary>
    public void ApplySections(bool system, bool music, bool claude, bool fans)
    {
        SystemSection.Visibility = system ? Visibility.Visible : Visibility.Collapsed;
        MusicSection.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        ClaudeSection.Visibility = claude ? Visibility.Visible : Visibility.Collapsed;
        FansSection.Visibility = fans ? Visibility.Visible : Visibility.Collapsed;
        if (fans) _ = UpdateFansAsync();
    }

    /// <summary>Cartes graphiques trouvées (pour les réglages).</summary>
    public IReadOnlyList<string> GpuNames => _sensors?.GpuNames ?? [];

    /// <summary>Change la carte graphique affichée (null : choix automatique).</summary>
    public void SetGpu(string? name)
    {
        if (_sensors is null) return;
        _sensors.PreferredGpu = name;
        _ = UpdateSensorsAsync();
    }

    private async Task StartWidgetsAsync()
    {
        SetTimersRunning(true);
        _ = UpdateClaudeAsync();
        _ = UpdateFansAsync();

        _music.Changed += playing => Dispatcher.InvokeAsync(() => ShowMusic(playing));
        try
        {
            await _music.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Write($"Musique indisponible : {ex.Message}");
        }

        try
        {
            // L'ouverture des capteurs peut prendre une seconde : hors de l'interface.
            _sensors = await Task.Run(() => new HardwareSensors { PreferredGpu = _config.PanelGpu });
            await UpdateSensorsAsync();
            Log.Write($"Cartes graphiques trouvées : {string.Join(", ", _sensors.GpuNames)}");
        }
        catch (Exception ex)
        {
            Log.Write($"Capteurs indisponibles : {ex}");
        }
    }

    private void SetTimersRunning(bool running)
    {
        // Panneau masqué : on ne calcule plus rien.
        foreach (var timer in new[] { _clockTimer, _sensorTimer, _claudeTimer, _fanTimer })
        {
            if (running) timer.Start();
            else timer.Stop();
        }
    }

    // ---------- Horloge ----------

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm", French);
        DateText.Text = now.ToString("dddd d MMMM", French);
    }

    // ---------- Capteurs ----------

    private async Task UpdateSensorsAsync()
    {
        if (_sensors is null || _readingSensors || SystemSection.Visibility != Visibility.Visible) return;
        _readingSensors = true;
        try
        {
            var local = await Task.Run(() => _sensors.Read());
            // Les lectures de l'assistant administrateur (CPU) passent en priorité.
            var snapshot = local.WithCpuFrom(ElevatedSensorBridge.ReadLatest());
            ShowSensors(snapshot);
        }
        catch (Exception ex)
        {
            Log.Write($"Lecture des capteurs : {ex.Message}");
        }
        finally
        {
            _readingSensors = false;
        }
    }

    private void ShowSensors(SensorSnapshot s)
    {
        CpuName.Text = ShortName(s.CpuName);
        CpuTemp.Text = Format(s.CpuTemperature, "°");
        CpuPower.Text = Format(s.CpuPower, " W");
        CpuLoadText.Text = Format(s.CpuLoad, " %");
        CpuLoadBar.Value = s.CpuLoad ?? 0;

        GpuName.Text = ShortName(s.GpuName);
        GpuTemp.Text = Format(s.GpuTemperature, "°");
        GpuPower.Text = Format(s.GpuPower, " W");
        GpuLoadText.Text = Format(s.GpuLoad, " %");
        GpuLoadBar.Value = s.GpuLoad ?? 0;

        if (s.CpuTemperature is not null)
        {
            CpuHint.Visibility = Visibility.Collapsed;
        }
        else
        {
            CpuHint.Visibility = Visibility.Visible;
            CpuHint.Text = !HardwareSensors.IsPawnIoInstalled()
                ? "Température et watts du processeur : installe d'abord le pilote PawnIO (clique ici)."
                : _askedForAdmin
                    ? "En attente de l'autorisation administrateur…"
                    : "Clique ici pour afficher la température et les watts du processeur (Windows demandera une autorisation, une seule fois).";
        }
    }

    private async void CpuHint_Click(object sender, MouseButtonEventArgs e)
    {
        if (!HardwareSensors.IsPawnIoInstalled())
        {
            OpenUrl("https://pawnio.eu/");
            return;
        }
        _askedForAdmin = true;
        CpuHint.Text = "En attente de l'autorisation administrateur…";
        bool ok = await Task.Run(SensorAutoStart.Enable);
        if (!ok) _askedForAdmin = false;
        Log.Write(ok ? "Température CPU activée" : "Autorisation administrateur refusée");
    }

    private static string Format(float? value, string unit) =>
        value is null ? "–" : $"{Math.Round(value.Value):0}{unit}";

    /// <summary>« NVIDIA GeForce RTX 4070 » → « RTX 4070 », « AMD Ryzen 7 7800X3D 8-Core Processor » → « Ryzen 7 7800X3D ».</summary>
    private static string ShortName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        foreach (var noise in new[] { "NVIDIA", "GeForce", "AMD", "Radeon", "Intel(R)", "Core(TM)", "(R)", "(TM)", "Processor", "CPU" })
            name = name.Replace(noise, "", StringComparison.OrdinalIgnoreCase);
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\d+-Core", "");
        return System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
    }

    // ---------- Ventilateurs (EZFAN) ----------

    private async Task UpdateFansAsync()
    {
        if (_readingFans || FansSection.Visibility != Visibility.Visible) return;
        _readingFans = true;
        try
        {
            var state = await _ezfan.GetStateAsync();
            ShowFans(state);
            // EZFAN fermé : on regarde moins souvent s'il a été lancé.
            _fanTimer.Interval = TimeSpan.FromSeconds(state is null ? 10 : 2);
        }
        catch (Exception ex)
        {
            Log.Write($"Lecture d'EZFAN : {ex.Message}");
        }
        finally
        {
            _readingFans = false;
        }
    }

    private void ShowFans(FanState? state)
    {
        FanProfiles.ItemsSource = state?.Profiles.Select(p => new ProfileChip(p, p == state.ActiveProfile)).ToList();
        FanList.ItemsSource = state?.Fans.Select(f => new FanRow(f)).ToList();
        FanSafety.Visibility = state?.SafetyActive == true ? Visibility.Visible : Visibility.Collapsed;

        string? idle = state is null ? "EZFAN n'est pas lancé."
            : !state.SetupDone ? "EZFAN n'est pas encore configuré : ouvre-le pour choisir tes ventilateurs."
            : state.Fans.Count == 0 ? "Aucun ventilateur détecté par EZFAN."
            : null;
        FanIdle.Text = idle ?? "";
        FanIdle.Visibility = idle is null ? Visibility.Collapsed : Visibility.Visible;

        string? warning = state?.Warnings.FirstOrDefault();
        FanWarning.Text = warning ?? "";
        FanWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;

        OpenEzfanText.Text = state is null ? "Lancer EZFAN" : "Ouvrir EZFAN";
    }

    private async void FanProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string profile }) return;
        if (!await _ezfan.SetProfileAsync(profile)) Log.Write($"EZFAN : profil « {profile} » non appliqué");
        await UpdateFansAsync();
    }

    private void OpenEzfan_Click(object sender, RoutedEventArgs e) => OpenEzfan();

    /// <summary>Ouvre EZFAN, le lance s'il ne tourne pas, ou demande où il se trouve.</summary>
    public async void OpenEzfan()
    {
        try
        {
            OpenEzfanText.Text = "Ouverture…";
            var result = await _ezfan.OpenAsync(_config.EzfanPath);
            if (result == EzfanLaunch.NotFound)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Où se trouve EZFAN.exe ?",
                    Filter = "EZFAN|EZFAN*.exe|Programmes|*.exe",
                };
                if (dialog.ShowDialog() == true)
                {
                    _config.EzfanPath = dialog.FileName;
                    _config.Save();
                    await _ezfan.OpenAsync(_config.EzfanPath);
                }
                else
                {
                    // Pas encore installé : la page de téléchargement.
                    OpenUrl(EzfanClient.ReleasesUrl);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Ouverture d'EZFAN impossible : {ex.Message}");
        }
        await UpdateFansAsync();
    }

    /// <summary>Un bouton de profil EZFAN ; le profil actif est en couleur.</summary>
    public sealed class ProfileChip(string name, bool active)
    {
        public string Name { get; } = name;
        public Brush Background { get; } = (Brush)System.Windows.Application.Current.Resources[active ? "Accent" : "CardHover"];
        public Brush Foreground { get; } = (Brush)System.Windows.Application.Current.Resources["TextPrimary"];
    }

    /// <summary>Ligne affichée pour un ventilateur.</summary>
    public sealed class FanRow(FanReading fan)
    {
        public string Name { get; } = fan.Name;
        public double Percent { get; } = fan.Percent ?? 0;
        public string? Reason { get; } = string.IsNullOrEmpty(fan.Reason) ? null : fan.Reason;

        public string Detail { get; } = string.Join(" · ", new[]
        {
            fan.Rpm is > 0 ? $"{Math.Round(fan.Rpm.Value).ToString("N0", French)} tr/min" : null,
            fan.Percent is not null ? $"{Math.Round(fan.Percent.Value):0} %" : null,
        }.Where(t => t is not null));
    }

    // ---------- Musique ----------

    private void ShowMusic(NowPlaying? playing)
    {
        if (playing is null || string.IsNullOrEmpty(playing.Title))
        {
            MusicPanel.Visibility = Visibility.Collapsed;
            MusicIdle.Visibility = Visibility.Visible;
            return;
        }

        MusicIdle.Visibility = Visibility.Collapsed;
        MusicPanel.Visibility = Visibility.Visible;
        TrackTitle.Text = playing.Title;
        TrackArtist.Text = playing.Artist;
        PlayPauseButton.Content = playing.IsPlaying ? "" : ""; // pause / lecture
        Artwork.Source = ToImage(playing.Artwork);
    }

    private static ImageSource? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(bytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 128;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _music.PlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _music.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _music.PreviousAsync();

    // ---------- Claude Code ----------

    private async Task UpdateClaudeAsync()
    {
        var sessions = await Task.Run(() => ClaudeSessions.Recent());
        _claudeRows.Clear();
        foreach (var session in sessions) _claudeRows.Add(new ClaudeRow(session));
        ClaudeIdle.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LaunchClaude_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClaudeSessions.Launch();
        }
        catch (Exception ex)
        {
            Log.Write($"Lancement de Claude impossible : {ex.Message}");
        }
    }

    /// <summary>Ligne affichée pour une session Claude Code.</summary>
    public sealed class ClaudeRow(ClaudeSession session)
    {
        public string Project { get; } = session.Project;

        public string StateText { get; } = session.State == ClaudeSessionState.Working
            ? "en cours…"
            : $"attend ta réponse · {Ago(session.LastActivity)}";

        public Brush Dot { get; } = (Brush)System.Windows.Application.Current.Resources[
            session.State == ClaudeSessionState.Working ? "Accent" : "Ok"];

        private static string Ago(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalMinutes < 1) return "à l'instant";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min";
            return $"{(int)span.TotalHours} h";
        }
    }

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    // ---------- Placement ----------

    /// <summary>
    /// Positionne la fenêtre en pixels physiques sur le bord gauche de l'écran,
    /// sans passer au-dessus de la barre des tâches.
    /// </summary>
    private void PlaceOnScreen()
    {
        var area = _screen.WorkingArea;
        var hwnd = new WindowInteropHelper(this).Handle;

        // Pas d'icône dans Alt+Tab.
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Largeur adaptée à la mise à l'échelle de cet écran (100 %, 125 %, 150 %…).
        uint dpi = 96;
        var monitor = MonitorFromPoint(new POINT { X = area.Left + 1, Y = area.Top + 1 }, MONITOR_DEFAULTTONEAREST);
        if (GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 && dpiX > 0) dpi = dpiX;
        int widthPx = (int)Math.Round(PanelWidthDip * dpi / 96.0);

        SetWindowPos(hwnd, IntPtr.Zero, area.Left, area.Top, widthPx, area.Height,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
