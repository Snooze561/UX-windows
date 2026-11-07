namespace UXWindows.Wallpaper;

/// <summary>
/// Trouve où accrocher une fenêtre pour qu'elle s'affiche derrière les icônes du bureau.
///
/// Deux organisations existent :
/// - « classique » (Windows 10, Windows 11 jusqu'à 23H2) : une fenêtre WorkerW de premier niveau,
///   juste derrière celle qui contient les icônes ;
/// - « surélevée » (Windows 11 24H2 et plus) : Progman contient les icônes (SHELLDLL_DefView) ;
///   on se place dans Progman, juste sous les icônes, et l'affichage doit passer par DirectComposition.
/// </summary>
internal static class DesktopHost
{
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private static readonly IntPtr HWND_BOTTOM = new(1);

    public sealed record Layer(IntPtr Parent, IntPtr InsertAfter, bool Raised);

    public static Layer? Find()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            Log.Write("Progman introuvable : l'Explorateur ne tourne pas ?");
            return null;
        }

        // Message non documenté qui demande à l'Explorateur de créer la couche WorkerW.
        Native.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out _);
        Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        bool raised = (Native.GetWindowLong(progman, Native.GWL_EXSTYLE) & WS_EX_NOREDIRECTIONBITMAP) != 0;

        if (raised)
        {
            IntPtr defView = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            IntPtr shellWorkerW = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

            // La couche de l'Explorateur passe tout au fond, notre fond vient juste sous les icônes.
            if (shellWorkerW != IntPtr.Zero)
            {
                Native.SetWindowPos(shellWorkerW, HWND_BOTTOM, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            Log.Write($"Bureau 24H2+ : Progman={progman:X}, icônes={defView:X}, WorkerW={shellWorkerW:X}");
            return new Layer(progman, defView, true);
        }

        IntPtr workerW = IntPtr.Zero;
        Native.EnumWindows((top, _) =>
        {
            if (Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                workerW = Native.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);

        Log.Write($"Bureau classique : Progman={progman:X}, WorkerW={workerW:X}");
        return new Layer(workerW != IntPtr.Zero ? workerW : progman, IntPtr.Zero, false);
    }

    /// <summary>
    /// Réapplique le fond d'écran Windows (efface les restes de notre affichage).
    /// </summary>
    public static void RefreshSystemWallpaper()
    {
        var path = new System.Text.StringBuilder(520);
        if (Native.SystemParametersInfo(Native.SPI_GETDESKWALLPAPER, (uint)path.Capacity, path, 0))
        {
            Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, path.ToString(), Native.SPIF_SENDCHANGE);
        }
    }
}
