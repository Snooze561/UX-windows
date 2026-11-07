using System.Diagnostics;
using System.Text.Json;

namespace UXWindows.Core.Sensors;

/// <summary>
/// La température et les watts du processeur demandent des droits administrateur.
/// Plutôt que de lancer toute l'app en administrateur, on lance une petite copie d'elle-même
/// en mode « capteurs », qui écrit ses lectures dans un fichier lu par l'app normale.
/// </summary>
public static class ElevatedSensorBridge
{
    public const string HelperArgument = "--capteurs";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UXWindows", "capteurs.json");

    /// <summary>Lance l'assistant administrateur (Windows affiche une demande d'autorisation).</summary>
    public static bool StartHelper()
    {
        try
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de l'app inconnu");
            Process.Start(new ProcessStartInfo(exe, $"{HelperArgument} {Environment.ProcessId}")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // L'utilisateur a refusé la demande d'autorisation.
            return false;
        }
    }

    /// <summary>Argument utilisé par la tâche planifiée : suivre l'app principale, quel que soit son numéro.</summary>
    public const string AutoArgument = "auto";

    /// <summary>Nom du verrou tenu par l'app principale tant qu'elle tourne.</summary>
    public const string MainAppMutex = "UXWindows.SingleInstance";

    private const string HelperMutex = "UXWindows.Capteurs";

    /// <summary>
    /// Boucle de l'assistant : lit les capteurs toutes les 2 s tant que l'app principale tourne.
    /// <paramref name="parent"/> est soit le numéro du processus de l'app, soit « auto » (tâche planifiée).
    /// </summary>
    public static void RunHelper(string parent)
    {
        using var single = new Mutex(true, HelperMutex, out bool isFirst);
        if (!isFirst) return; // un assistant tourne déjà

        Func<bool> mainAppRunning;
        if (parent == AutoArgument)
        {
            mainAppRunning = IsMainAppRunning;
            // À l'ouverture de session, l'app principale peut démarrer un peu après nous.
            var deadline = DateTime.Now.AddMinutes(3);
            while (!IsMainAppRunning())
            {
                if (DateTime.Now > deadline) return;
                Thread.Sleep(2000);
            }
        }
        else if (int.TryParse(parent, out int pid))
        {
            Process process;
            try { process = Process.GetProcessById(pid); }
            catch (ArgumentException) { return; }
            mainAppRunning = () => !process.HasExited;
        }
        else
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var sensors = new HardwareSensors();
        while (mainAppRunning())
        {
            try
            {
                var json = JsonSerializer.Serialize(sensors.Read());
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception)
            {
                // On réessaie au tour suivant.
            }
            Thread.Sleep(2000);
        }

        try { File.Delete(FilePath); } catch (Exception) { }
    }

    private static bool IsMainAppRunning()
    {
        if (!Mutex.TryOpenExisting(MainAppMutex, out var mutex)) return false;
        mutex.Dispose();
        return true;
    }

    /// <summary>Dernière lecture de l'assistant, si elle a moins de 10 secondes.</summary>
    public static SensorSnapshot? ReadLatest()
    {
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || DateTime.Now - info.LastWriteTime > TimeSpan.FromSeconds(10)) return null;
            return JsonSerializer.Deserialize<SensorSnapshot>(File.ReadAllText(FilePath));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
