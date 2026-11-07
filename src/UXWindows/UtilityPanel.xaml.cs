using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace UXWindows;

/// <summary>
/// Panneau ancré sur toute la hauteur de l'écran utilitaire.
/// </summary>
public partial class UtilityPanel : Window
{
    private const int PanelWidthPx = 380;

    private readonly Forms.Screen _screen;
    private readonly DispatcherTimer _clock;
    private static readonly CultureInfo French = new("fr-FR");

    public UtilityPanel(Forms.Screen screen)
    {
        _screen = screen;
        InitializeComponent();

        // Une mise à jour par seconde suffit : coût quasi nul.
        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clock.Start();

        SourceInitialized += (_, _) => PlaceOnScreen();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm", French);
        DateText.Text = now.ToString("dddd d MMMM", French);
    }

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

        SetWindowPos(hwnd, IntPtr.Zero, area.Left, area.Top, PanelWidthPx, area.Height,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);
}
