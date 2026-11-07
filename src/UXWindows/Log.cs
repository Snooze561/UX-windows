using System.IO;

namespace UXWindows;

/// <summary>
/// Petit journal dans %APPDATA%\UXWindows\journal.txt, pour comprendre un problème à distance.
/// </summary>
public static class Log
{
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UXWindows", "journal.txt");

    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                // On repart d'un fichier neuf au-delà de 1 Mo.
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Le journal ne doit jamais faire planter l'app.
        }
    }
}
