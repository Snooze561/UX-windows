using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace UXWindows;

/// <summary>
/// Flou de l'arrière-plan pour le style « Liquid glass » : Windows floute ce qui se trouve
/// derrière la fenêtre (le fond d'écran), et la forme de la fenêtre est réduite aux cartes
/// pour que le flou ne déborde pas autour d'elles.
/// </summary>
public sealed class GlassEffect
{
    private readonly Window _window;
    private readonly Panel _cards;
    private bool _enabled;
    private string _lastShape = "";

    public GlassEffect(Window window, Panel cards)
    {
        _window = window;
        _cards = cards;
        // Les cartes bougent (glisser-déposer, défilement, sections masquées) : la forme suit.
        _window.LayoutUpdated += (_, _) => { if (_enabled) UpdateShape(); };
    }

    /// <summary>Rayon des coins des cartes, en unités WPF.</summary>
    public double CornerRadius { get; set; } = 22;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;

        try
        {
            SetAccent(hwnd, enabled ? AccentState.EnableBlurBehind : AccentState.Disabled);
            if (enabled)
            {
                _lastShape = "";
                UpdateShape();
            }
            else
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                _lastShape = "";
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Flou du style Liquid glass indisponible : {ex.Message}");
        }
    }

    /// <summary>Forme de la fenêtre = l'union des cartes visibles, coins arrondis compris.</summary>
    private void UpdateShape()
    {
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero || !_window.IsVisible) return;

        var dpi = VisualTreeHelper.GetDpi(_window);
        var rects = new List<Int32Rect>();
        foreach (var card in _cards.Children.OfType<FrameworkElement>())
        {
            if (card.Visibility != Visibility.Visible || card.ActualHeight <= 0) continue;
            var r = card.TransformToAncestor(_window).TransformBounds(new Rect(card.RenderSize));
            rects.Add(new Int32Rect(
                (int)Math.Round(r.X * dpi.DpiScaleX), (int)Math.Round(r.Y * dpi.DpiScaleY),
                (int)Math.Round(r.Width * dpi.DpiScaleX), (int)Math.Round(r.Height * dpi.DpiScaleY)));
        }

        string shape = string.Join(";", rects.Select(r => $"{r.X},{r.Y},{r.Width},{r.Height}"));
        if (shape == _lastShape) return;
        _lastShape = shape;

        int corner = (int)Math.Round(CornerRadius * 2 * dpi.DpiScaleX);
        IntPtr region = CreateRectRgn(0, 0, 0, 0);
        foreach (var r in rects)
        {
            IntPtr card = CreateRoundRectRgn(r.X, r.Y, r.X + r.Width + 1, r.Y + r.Height + 1, corner, corner);
            CombineRgn(region, region, card, RGN_OR);
            DeleteObject(card);
        }
        // Windows garde la région : on ne la libère pas après SetWindowRgn.
        if (SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
    }

    // ---------- Windows ----------

    private enum AccentState
    {
        Disabled = 0,
        EnableBlurBehind = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;
    private const int RGN_OR = 2;

    private static void SetAccent(IntPtr hwnd, AccentState state)
    {
        var accent = new AccentPolicy { AccentState = state };
        int size = Marshal.SizeOf(accent);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);
}
