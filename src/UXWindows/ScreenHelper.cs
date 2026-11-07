using Forms = System.Windows.Forms;

namespace UXWindows;

public static class ScreenHelper
{
    /// <summary>
    /// L'écran des utilitaires : l'écran secondaire le plus à gauche.
    /// S'il n'y a qu'un seul écran, on prend l'écran principal.
    /// </summary>
    public static Forms.Screen GetUtilityScreen(string? preferredDeviceName = null)
    {
        var preferred = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == preferredDeviceName);
        if (preferred is not null) return preferred;

        var secondary = Forms.Screen.AllScreens
            .Where(s => !s.Primary)
            .OrderBy(s => s.Bounds.Left)
            .FirstOrDefault();

        return secondary ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }

    /// <summary>Nom lisible : « écran principal », « écran de gauche », « écran de droite »…</summary>
    public static string Label(Forms.Screen screen)
    {
        if (screen.Primary) return "écran principal";

        var primary = Forms.Screen.PrimaryScreen;
        if (primary is null) return screen.DeviceName;
        if (screen.Bounds.Right <= primary.Bounds.Left) return "écran de gauche";
        if (screen.Bounds.Left >= primary.Bounds.Right) return "écran de droite";
        return screen.Bounds.Top < primary.Bounds.Top ? "écran du haut" : "écran du bas";
    }
}
