using System.Diagnostics;
using System.Text.Json;

namespace UXWindows.Core.Claude;

public enum ClaudeSessionState
{
    /// <summary>Claude est en train de travailler.</summary>
    Working,

    /// <summary>Claude a répondu et attend ton message.</summary>
    WaitingForYou,
}

public sealed record ClaudeSession(string Project, ClaudeSessionState State, DateTime LastActivity);

/// <summary>
/// Suit les sessions Claude Code lancées sur ce PC, en lisant les journaux de conversation
/// que Claude Code écrit dans %USERPROFILE%\.claude\projects (un fichier .jsonl par session).
/// </summary>
public static class ClaudeSessions
{
    private static readonly string ProjectsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>Une session qui a écrit il y a moins de 20 s est considérée « en cours ».</summary>
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromSeconds(20);

    /// <summary>Au-delà de 12 h sans activité, la session n'est plus affichée.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);

    public static IReadOnlyList<ClaudeSession> Recent(int max = 4)
    {
        if (!Directory.Exists(ProjectsFolder)) return [];

        var now = DateTime.Now;
        var result = new List<ClaudeSession>();
        try
        {
            var files = new DirectoryInfo(ProjectsFolder)
                .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
                .Where(f => now - f.LastWriteTime < MaxAge)
                .OrderByDescending(f => f.LastWriteTime)
                .Take(max);

            foreach (var file in files)
            {
                var (type, cwd) = ReadLastEntry(file.FullName);
                if (type is null) continue;

                bool recent = now - file.LastWriteTime < WorkingWindow;
                var state = recent || type != "assistant" ? ClaudeSessionState.Working : ClaudeSessionState.WaitingForYou;
                // Un dernier message de l'utilisateur sans activité depuis longtemps : session abandonnée.
                if (!recent && type == "user") continue;

                string project = string.IsNullOrEmpty(cwd) ? file.Directory?.Name ?? "?" : Path.GetFileName(cwd.TrimEnd('\\', '/'));
                result.Add(new ClaudeSession(project, state, file.LastWriteTime));
            }
        }
        catch (Exception)
        {
            // Dossier en cours d'écriture : on réessaiera au prochain rafraîchissement.
        }
        return result;
    }

    /// <summary>Type ("user", "assistant"…) et dossier de travail de la dernière entrée utile du fichier.</summary>
    private static (string? Type, string? Cwd) ReadLastEntry(string path)
    {
        try
        {
            // Lecture de la fin du fichier seulement : ces journaux peuvent être gros.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, stream.Length - 64 * 1024);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                try
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("type", out var typeProp)) continue;
                    string? type = typeProp.GetString();
                    if (type is not ("user" or "assistant")) continue;
                    string? cwd = root.TryGetProperty("cwd", out var cwdProp) ? cwdProp.GetString() : null;
                    return (type, cwd);
                }
                catch (JsonException)
                {
                    // Ligne coupée (début de lecture au milieu d'une ligne) : on passe.
                }
            }
        }
        catch (IOException)
        {
        }
        return (null, null);
    }

    /// <summary>
    /// Ouvre Claude : l'app Claude pour Windows si elle est installée, sinon Claude Code dans
    /// Windows Terminal si la commande « claude » existe, sinon claude.ai/code dans le navigateur.
    /// </summary>
    public static void Launch()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string desktopApp = Path.Combine(localAppData, "AnthropicClaude", "claude.exe");
        if (File.Exists(desktopApp))
        {
            Process.Start(new ProcessStartInfo(desktopApp) { UseShellExecute = true });
            return;
        }

        if (FindOnPath("claude.exe") is not null || FindOnPath("claude.cmd") is not null)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (FindOnPath("wt.exe") is not null)
                Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{home}\" claude") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("cmd.exe", "/k claude") { UseShellExecute = true, WorkingDirectory = home });
            return;
        }

        Process.Start(new ProcessStartInfo("https://claude.ai/code") { UseShellExecute = true });
    }

    private static string? FindOnPath(string fileName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        // Windows Terminal est un alias d'app dans WindowsApps.
        var extra = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps");
        return paths.Append(extra)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.Combine(p.Trim(), fileName))
            .FirstOrDefault(File.Exists);
    }
}
