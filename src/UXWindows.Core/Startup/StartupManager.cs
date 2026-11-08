using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace UXWindows.Core.Startup;

/// <summary>
/// Démarrage avec Windows, et lancement de l'assistant capteurs (administrateur) sans demande à chaque fois.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "UX-windows";

    /// <summary>Tâche planifiée qui lance l'assistant capteurs en administrateur à l'ouverture de session.</summary>
    public const string SensorTaskName = "UX-windows capteurs";

    private static string ExePath => Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de l'app inconnu");

    // ---------- Démarrage avec Windows ----------

    public static bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(RunValue, $"\"{ExePath}\"");
        else key.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    /// <summary>Après un déplacement de l'app (nouvelle version installée ailleurs), on remet le bon chemin.</summary>
    public static void RefreshStartWithWindowsPath()
    {
        if (IsStartWithWindowsEnabled()) SetStartWithWindows(true);
    }

    // ---------- Assistant capteurs automatique ----------

    public static bool IsSensorTaskInstalled() => RunSchtasks($"/Query /TN \"{SensorTaskName}\"", elevated: false) == 0;

    /// <summary>
    /// Crée la tâche planifiée (Windows demande l'autorisation administrateur une seule fois).
    /// Renvoie false si l'utilisateur refuse.
    /// </summary>
    public static bool InstallSensorTask()
    {
        string xmlPath = Path.Combine(Path.GetTempPath(), "uxwindows-capteurs.xml");
        File.WriteAllText(xmlPath, BuildTaskXml(), System.Text.Encoding.Unicode);
        try
        {
            return RunSchtasks($"/Create /TN \"{SensorTaskName}\" /XML \"{xmlPath}\" /F", elevated: true) == 0;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch (IOException) { }
        }
    }

    public static bool UninstallSensorTask() =>
        RunSchtasks($"/Delete /TN \"{SensorTaskName}\" /F", elevated: true) == 0;

    /// <summary>Lance tout de suite l'assistant via la tâche (aucune demande d'autorisation).</summary>
    public static bool RunSensorTask() => RunSchtasks($"/Run /TN \"{SensorTaskName}\"", elevated: false) == 0;

    private static string BuildTaskXml()
    {
        string user = SecurityElement.Escape($"{Environment.UserDomainName}\\{Environment.UserName}");
        string exe = SecurityElement.Escape(ExePath);
        string arguments = SecurityElement.Escape($"{Sensors.ElevatedSensorBridge.HelperArgument} {Sensors.ElevatedSensorBridge.AutoArgument}");

        // Pas de limite de durée, fonctionne aussi sur batterie, une seule copie à la fois.
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Lit la température et la consommation du processeur pour le panneau UX-windows.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exe}</Command>
                  <Arguments>{arguments}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int RunSchtasks(string arguments, bool elevated)
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = elevated,
            };
            if (elevated) info.Verb = "runas";

            using var process = Process.Start(info);
            if (process is null) return -1;
            process.WaitForExit(15_000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Autorisation refusée, ou schtasks introuvable.
            return -1;
        }
    }
}
