using UXWindows.Video;
using Forms = System.Windows.Forms;

namespace UXWindows.Wallpaper;

/// <summary>
/// Une vidéo de fond sur un écran : une fenêtre Win32 accrochée derrière les icônes,
/// dans laquelle <see cref="VideoRenderer"/> affiche la vidéo.
/// </summary>
internal sealed class VideoWallpaper : IDisposable
{
    private readonly HostWindow _host;
    private readonly VideoRenderer _renderer;

    public Forms.Screen Screen { get; }
    public string Path { get; }

    public VideoWallpaper(Forms.Screen screen, DesktopHost.Layer layer, string path, Action<string> onError)
    {
        Screen = screen;
        Path = path;

        // Position de l'écran dans le repère de la fenêtre parente (pixels physiques).
        var origin = new Native.POINT { X = screen.Bounds.Left, Y = screen.Bounds.Top };
        Native.MapWindowPoints(IntPtr.Zero, layer.Parent, ref origin, 1);

        _host = new HostWindow(layer.Parent, origin.X, origin.Y, screen.Bounds.Width, screen.Bounds.Height);

        // Juste sous les icônes du bureau (organisation 24H2+).
        if (layer.Raised && layer.InsertAfter != IntPtr.Zero)
        {
            Native.SetWindowPos(_host.Handle, layer.InsertAfter, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        Log.Write($"Fond vidéo {screen.DeviceName} : fenêtre {_host.Handle:X} à ({origin.X},{origin.Y}) " +
                  $"{screen.Bounds.Width}x{screen.Bounds.Height}, fichier {path}");

        try
        {
            _renderer = new VideoRenderer(_host.Handle, screen.Bounds.Width, screen.Bounds.Height, path);
        }
        catch
        {
            _host.DestroyHandle();
            throw;
        }
        _renderer.Failed += onError;
    }

    public bool IsPaused => _renderer.IsPaused;

    public void SetPaused(bool paused)
    {
        if (paused) _renderer.Pause();
        else _renderer.Resume();
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _host.DestroyHandle();
    }

    /// <summary>Fenêtre enfant nue : rien n'y est dessiné par Windows, tout passe par DirectComposition.</summary>
    private sealed class HostWindow : Forms.NativeWindow
    {
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_DISABLED = 0x08000000;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public HostWindow(IntPtr parent, int x, int y, int width, int height)
        {
            CreateHandle(new Forms.CreateParams
            {
                Caption = "UX-windows fond",
                Parent = parent,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Style = WS_CHILD | WS_VISIBLE | WS_DISABLED,
                ExStyle = WS_EX_NOREDIRECTIONBITMAP | WS_EX_NOACTIVATE,
            });
        }
    }
}
