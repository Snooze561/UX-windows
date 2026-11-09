using System.Windows;
using System.Windows.Media;

namespace UXWindows;

/// <summary>Les deux styles du panneau.</summary>
public static class PanelStyles
{
    public const string Liquid = "liquid";
    public const string Classic = "classic";

    /// <summary>Applique un style aux cartes d'une fenêtre (ses ressources passent avant celles de l'app).</summary>
    public static void Apply(FrameworkElement target, string style)
    {
        var r = target.Resources;
        if (style == Classic)
        {
            // On retire les valeurs du style Liquid : celles de l'app (App.xaml) reprennent la main.
            foreach (var key in new[] { "WidgetBackground", "WidgetBorder", "WidgetRadius", "WidgetPadding" })
                r.Remove(key);
            return;
        }

        // Verre : voile clair en haut qui s'assombrit vers le bas, comme une lumière venant d'en haut.
        r["WidgetBackground"] = Frozen(new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF), 0.0),
                new(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.35),
                new(Color.FromArgb(0x26, 0x0A, 0x0C, 0x14), 1.0),
            },
            new Point(0, 0), new Point(0, 1)));

        // Liseré lumineux : reflet vif en haut à gauche, plus discret en bas à droite.
        r["WidgetBorder"] = Frozen(new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF), 0.0),
                new(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF), 0.45),
                new(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.7),
                new(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF), 1.0),
            },
            new Point(0, 0), new Point(1, 1)));

        r["WidgetRadius"] = new CornerRadius(LiquidRadius);
        r["WidgetPadding"] = new Thickness(20, 16, 20, 16);
    }

    public const double LiquidRadius = 22;

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
