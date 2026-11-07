using Forms = System.Windows.Forms;

namespace UXWindows;

public static class ScreenHelper
{
    /// <summary>
    /// L'écran des utilitaires : l'écran secondaire le plus à gauche.
    /// S'il n'y a qu'un seul écran, on prend l'écran principal.
    /// </summary>
    public static Forms.Screen GetUtilityScreen()
    {
        var secondary = Forms.Screen.AllScreens
            .Where(s => !s.Primary)
            .OrderBy(s => s.Bounds.Left)
            .FirstOrDefault();

        return secondary ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }
}
