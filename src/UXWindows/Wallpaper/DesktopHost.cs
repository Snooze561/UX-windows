namespace UXWindows.Wallpaper;

/// <summary>
/// Trouve la fenêtre « WorkerW » de l'Explorateur, placée entre l'image de fond de Windows
/// et les icônes du bureau. Une fenêtre enfant de WorkerW s'affiche donc comme fond d'écran.
/// </summary>
internal static class DesktopHost
{
    public static IntPtr GetWorkerW()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        // Message non documenté qui demande à l'Explorateur de créer la couche WorkerW.
        Native.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out _);
        Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        // Windows 11 24H2 et plus : WorkerW est un enfant de Progman.
        IntPtr workerW = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (workerW != IntPtr.Zero) return workerW;

        // Versions précédentes : WorkerW est la fenêtre de premier niveau qui suit
        // celle qui contient les icônes (SHELLDLL_DefView).
        Native.EnumWindows((top, _) =>
        {
            IntPtr defView = Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                workerW = Native.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        // En dernier recours, Progman lui-même.
        return workerW != IntPtr.Zero ? workerW : progman;
    }

    /// <summary>
    /// Réapplique le fond d'écran Windows d'origine (efface les restes de notre affichage).
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
