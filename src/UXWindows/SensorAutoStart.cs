using UXWindows.Core.Sensors;
using UXWindows.Core.Startup;

namespace UXWindows;

/// <summary>
/// Température du processeur : l'assistant administrateur est lancé par une tâche planifiée,
/// créée une seule fois (une demande d'autorisation), puis démarrée sans demande.
/// </summary>
public static class SensorAutoStart
{
    /// <summary>Crée la tâche si besoin (une demande d'autorisation) et lance l'assistant.</summary>
    public static bool Enable()
    {
        if (!StartupManager.IsSensorTaskInstalled() && !StartupManager.InstallSensorTask())
            return false;
        return StartupManager.RunSensorTask() || ElevatedSensorBridge.StartHelper();
    }

    public static bool Disable() => !StartupManager.IsSensorTaskInstalled() || StartupManager.UninstallSensorTask();

    /// <summary>Au lancement de l'app : démarre l'assistant si la tâche existe et qu'il ne tourne pas déjà.</summary>
    public static void StartIfInstalled()
    {
        try
        {
            if (ElevatedSensorBridge.ReadLatest() is null && StartupManager.IsSensorTaskInstalled())
            {
                bool ok = StartupManager.RunSensorTask();
                Log.Write(ok ? "Assistant capteurs démarré par la tâche planifiée" : "Échec du démarrage de la tâche capteurs");
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Assistant capteurs : {ex.Message}");
        }
    }
}
