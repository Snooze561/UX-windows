using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace UXWindows.Wallpaper;

/// <summary>
/// Une fenêtre de fond par écran, accrochée derrière les icônes du bureau.
/// </summary>
public partial class WallpaperWindow : Window
{
    private static readonly string[] VideoExtensions = [".mp4", ".wmv", ".mov", ".m4v"];

    public Forms.Screen Screen { get; }
    public bool HasVideo { get; private set; }
    public bool IsPaused { get; private set; }

    private readonly IntPtr _workerW;

    public WallpaperWindow(Forms.Screen screen, IntPtr workerW, string path)
    {
        Screen = screen;
        _workerW = workerW;
        InitializeComponent();

        SourceInitialized += (_, _) => AttachToDesktop();
        Video.MediaEnded += (_, _) =>
        {
            // Lecture en boucle.
            Video.Position = TimeSpan.Zero;
            if (!IsPaused) Video.Play();
        };

        Load(path);
    }

    public void Load(string path)
    {
        Video.Stop();
        Video.Source = null;
        Picture.Source = null;

        HasVideo = VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        if (HasVideo)
        {
            Picture.Visibility = Visibility.Collapsed;
            Video.Visibility = Visibility.Visible;
            Video.Source = new Uri(path);
            if (!IsPaused) Video.Play();
        }
        else
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            // Pas besoin de garder en mémoire une image plus grande que l'écran.
            image.DecodePixelWidth = Screen.Bounds.Width;
            image.EndInit();
            image.Freeze();

            Video.Visibility = Visibility.Collapsed;
            Picture.Visibility = Visibility.Visible;
            Picture.Source = image;
        }
    }

    public void SetPaused(bool paused)
    {
        if (paused == IsPaused) return;
        IsPaused = paused;
        if (!HasVideo) return;
        if (paused) Video.Pause();
        else Video.Play();
    }

    private void AttachToDesktop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        int style = Native.GetWindowLong(hwnd, Native.GWL_STYLE);
        style = (style & ~Native.WS_POPUP) | Native.WS_CHILD;
        Native.SetWindowLong(hwnd, Native.GWL_STYLE, style);

        int exStyle = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, exStyle | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);

        Native.SetParent(hwnd, _workerW);

        // Coordonnées de l'écran converties dans le repère de WorkerW (pixels physiques).
        var origin = new Native.POINT { X = Screen.Bounds.Left, Y = Screen.Bounds.Top };
        Native.MapWindowPoints(IntPtr.Zero, _workerW, ref origin, 1);

        Native.SetWindowPos(hwnd, IntPtr.Zero, origin.X, origin.Y, Screen.Bounds.Width, Screen.Bounds.Height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }
}
