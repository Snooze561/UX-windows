using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UXWindows.Core.Fans;

/// <summary>Un ventilateur (ou une pompe) vu par EZFAN.</summary>
public sealed record FanReading(string Id, string Name, float? Rpm, float? Percent, string? Reason);

/// <summary>Ce qu'EZFAN fait en ce moment.</summary>
public sealed record FanState
{
    public string? ActiveProfile { get; init; }
    public IReadOnlyList<string> Profiles { get; init; } = [];
    public IReadOnlyList<FanReading> Fans { get; init; } = [];
    public bool SafetyActive { get; init; }
    public bool SetupDone { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public enum EzfanLaunch { Opened, Started, NotFound }

/// <summary>
/// Parle à EZFAN, l'app de pilotage des ventilateurs, par son interface locale (http://localhost:5400).
/// UX-windows ne touche jamais lui-même aux ventilateurs : un seul programme doit les piloter,
/// sinon les deux se battent. Il lit l'état d'EZFAN et peut changer son profil actif.
/// </summary>
public sealed class EzfanClient : IDisposable
{
    public const string Url = "http://localhost:5400";
    public const string ReleasesUrl = "https://github.com/Snooze561/EZFAN/releases";
    private const string TaskName = "EZFAN";

    // 127.0.0.1 plutôt que localhost : une seule tentative de connexion quand EZFAN est fermé.
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:5400"), Timeout = TimeSpan.FromSeconds(2) };

    /// <summary>État actuel, ou null si EZFAN n'est pas lancé.</summary>
    public async Task<FanState?> GetStateAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/state");
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            return Parse(doc.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Change le profil actif d'EZFAN (Bureautique, Jeux légers, Jeux AAA, Performance…).</summary>
    public async Task<bool> SetProfileAsync(string profile)
    {
        try
        {
            // On renvoie la configuration d'EZFAN telle quelle, seul le profil actif change.
            var state = JsonNode.Parse(await _http.GetStringAsync("/api/state"));
            if (state?["config"] is not JsonObject config) return false;
            config["activeProfile"] = profile;
            using var body = new StringContent(config.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.PutAsync("/api/config", body);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ouvre l'interface d'EZFAN. S'il ne tourne pas, le démarre : par sa tâche planifiée
    /// (pas de demande d'autorisation), sinon par son exe (Windows demande l'autorisation).
    /// </summary>
    public async Task<EzfanLaunch> OpenAsync(string? knownExePath)
    {
        if (await GetStateAsync() is not null)
        {
            OpenUrl(Url);
            return EzfanLaunch.Opened;
        }

        if (RunSchtasks($"/Run /TN \"{TaskName}\"") == 0)
        {
            // La tâche lance EZFAN en arrière-plan : on ouvre son interface dès qu'il répond.
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500);
                if (await GetStateAsync() is not null)
                {
                    OpenUrl(Url);
                    return EzfanLaunch.Started;
                }
            }
            return EzfanLaunch.Started;
        }

        var exe = FindExe(knownExePath);
        if (exe is null) return EzfanLaunch.NotFound;
        try
        {
            // EZFAN ouvre lui-même son interface au démarrage.
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) })?.Dispose();
            return EzfanLaunch.Started;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Autorisation refusée.
            return EzfanLaunch.Started;
        }
    }

    /// <summary>Chemin d'EZFAN.exe : celui retenu, celui de sa tâche planifiée, ou un emplacement courant.</summary>
    public static string? FindExe(string? knownExePath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            knownExePath,
            TaskExePath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "EZFAN", "EZFAN.exe"),
            // Installateur d'EZFAN (Program Files).
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EZFAN", "EZFAN.exe"),
            Path.Combine(home, "Downloads", "EZFAN.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EZFAN.exe"),
        };
        return candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
    }

    private static string? TaskExePath()
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\" /XML")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            using var process = Process.Start(info);
            if (process is null) return null;
            string xml = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5_000);
            var match = Regex.Match(xml, @"<Command>([^<]+)</Command>");
            return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim().Trim('"')) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static FanState Parse(JsonElement root)
    {
        var config = Get(root, "config");
        var snapshot = Get(root, "snapshot");

        var names = new Dictionary<string, string>();
        if (Get(config, "fanNames") is { ValueKind: JsonValueKind.Object } fanNames)
            foreach (var p in fanNames.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                    names[p.Name] = p.Value.GetString()!;

        var roles = new Dictionary<string, string>();
        if (Get(config, "specialHeaders") is { ValueKind: JsonValueKind.Object } special)
            foreach (var p in special.EnumerateObject())
                roles[p.Name] = p.Value.GetString() == "Pump" ? "Pompe" : "Ventirad";

        var runtime = new Dictionary<string, (float? applied, string? reason)>();
        foreach (var fan in Items(root, "fans"))
            if (Str(fan, "controlId") is string id)
                runtime[id] = (Num(fan, "applied"), Str(fan, "reason"));

        var controls = Items(snapshot, "controls").ToList();

        // Les ventilateurs du profil actif, dans l'ordre d'EZFAN ; sinon toutes les sorties de la carte mère.
        string? active = Str(config, "activeProfile");
        var profiles = Get(config, "profiles") is { ValueKind: JsonValueKind.Object } p2
            ? p2.EnumerateObject().Select(p => p.Name).ToList()
            : [];
        var ids = active is not null && Get(Get(config, "profiles"), active) is { } profile
            ? Items(profile, "fans").Select(f => Str(f, "controlId")).OfType<string>().ToList()
            : [];
        if (ids.Count == 0) ids = controls.Select(c => Str(c, "id")).OfType<string>().ToList();

        var fans = new List<FanReading>();
        foreach (var id in ids)
        {
            var control = controls.FirstOrDefault(c => Str(c, "id") == id);
            if (control.ValueKind != JsonValueKind.Object) continue;
            float? rpm = Num(control, "pairedFanRpm");
            names.TryGetValue(id, out var custom);
            roles.TryGetValue(id, out var role);
            // Une prise sans ventilateur branché ni nom donné n'a rien à faire dans le panneau.
            if (custom is null && role is null && rpm is null or <= 0) continue;

            runtime.TryGetValue(id, out var run);
            fans.Add(new FanReading(id, custom ?? role ?? Str(control, "name") ?? id, rpm,
                run.applied ?? Num(control, "percent"), run.reason));
        }

        return new FanState
        {
            ActiveProfile = active,
            Profiles = profiles,
            Fans = fans,
            SafetyActive = Get(root, "safetyActive") is { ValueKind: JsonValueKind.True },
            SetupDone = Get(config, "setupDone") is { ValueKind: JsonValueKind.True },
            Warnings = Items(root, "warnings").Where(w => w.ValueKind == JsonValueKind.String).Select(w => w.GetString()!).ToList(),
        };
    }

    private static JsonElement Get(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

    private static IEnumerable<JsonElement> Items(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static float? Num(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.Number } v ? v.GetSingle() : null;

    private static int RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (process is null) return -1;
            process.WaitForExit(10_000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    public void Dispose() => _http.Dispose();
}
