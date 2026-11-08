using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace UXWindows.Wallpaper;

/// <summary>
/// Les images fixes passent par le vrai fond d'écran Windows, réglé écran par écran :
/// aucune ressource consommée par l'app.
/// </summary>
internal static class ImageWallpaper
{
    private const int DWPOS_FILL = 4;
    private const int DWPOS_SPAN = 5;

    /// <summary>
    /// Une seule image étalée sur tous les écrans (mode « Étendre » de Windows).
    /// Renvoie l'ancien fond s'il était le même partout.
    /// </summary>
    public static string? SetSpanned(string imagePath)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            string? previous = null;
            try { previous = wallpaper.GetWallpaper(null); }
            catch (COMException) { /* fonds différents selon les écrans */ }

            wallpaper.SetPosition(DWPOS_SPAN);
            wallpaper.SetWallpaper(null, imagePath);
            Log.Write($"Fond Windows étalé sur tous les écrans : {imagePath} (avant : {previous})");
            return previous;
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    /// <summary>Même image sur tous les écrans, en mode « Remplir » (pour revenir au fond d'origine).</summary>
    public static void SetAllFilled(string imagePath)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            wallpaper.SetPosition(DWPOS_FILL);
            wallpaper.SetWallpaper(null, imagePath);
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    /// <summary>Change le fond Windows de l'écran et renvoie l'ancien (pour pouvoir le remettre).</summary>
    public static string? Set(Forms.Screen screen, string imagePath)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            string? monitorId = FindMonitorId(wallpaper, screen);
            if (monitorId is null)
            {
                Log.Write($"Écran {screen.DeviceName} introuvable pour le fond Windows");
                return null;
            }

            string? previous = wallpaper.GetWallpaper(monitorId);
            wallpaper.SetPosition(DWPOS_FILL);
            wallpaper.SetWallpaper(monitorId, imagePath);
            Log.Write($"Fond Windows {screen.DeviceName} : {imagePath} (avant : {previous})");
            return previous;
        }
        finally
        {
            Marshal.ReleaseComObject(wallpaper);
        }
    }

    private static string? FindMonitorId(IDesktopWallpaper wallpaper, Forms.Screen screen)
    {
        uint count = wallpaper.GetMonitorDevicePathCount();
        for (uint i = 0; i < count; i++)
        {
            string id = wallpaper.GetMonitorDevicePathAt(i);
            if (string.IsNullOrEmpty(id)) continue;
            var rect = wallpaper.GetMonitorRECT(id);
            if (rect.Left == screen.Bounds.Left && rect.Top == screen.Bounds.Top) return id;
        }
        return null;
    }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass
    {
    }

    /// <summary>Interface Windows IDesktopWallpaper (l'ordre des méthodes doit rester celui de Windows).</summary>
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        Native.RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();

        void SetPosition(int position);
    }
}
