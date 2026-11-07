using System.IO;
using System.Text.Json;

namespace UXWindows;

/// <summary>
/// Réglages enregistrés dans %APPDATA%\UXWindows\config.json.
/// </summary>
public sealed class AppConfig
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UXWindows");
    private static readonly string FilePath = Path.Combine(Folder, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Fond choisi pour chaque écran (clé : nom de l'écran Windows, ex. \\.\DISPLAY1).</summary>
    public Dictionary<string, string> Wallpapers { get; set; } = new();

    /// <summary>Fond Windows d'origine de chaque écran, remis quand on retire une image.</summary>
    public Dictionary<string, string> OriginalWallpapers { get; set; } = new();

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch (Exception)
        {
            // Fichier abîmé : on repart des réglages par défaut.
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
