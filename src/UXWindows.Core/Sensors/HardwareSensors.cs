using LibreHardwareMonitor.Hardware;

namespace UXWindows.Core.Sensors;

/// <summary>
/// Lit les capteurs du processeur et de la carte graphique avec LibreHardwareMonitor.
///
/// Carte graphique : fonctionne sans droits particuliers.
/// Processeur (température, watts) : il faut le pilote PawnIO et des droits administrateur.
/// </summary>
public sealed class HardwareSensors : IDisposable
{
    private readonly Computer _computer;
    private readonly object _gate = new();

    public HardwareSensors()
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
        _computer.Open();
    }

    /// <summary>Le pilote PawnIO (nécessaire pour la température et les watts du processeur) est-il installé ?</summary>
    public static bool IsPawnIoInstalled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Nom de la carte graphique à afficher (réglages). Vide : choix automatique.</summary>
    public string? PreferredGpu { get; set; }

    /// <summary>Cartes graphiques trouvées lors de la dernière lecture.</summary>
    public IReadOnlyList<string> GpuNames { get; private set; } = [];

    private static bool IsGpu(IHardware hw) =>
        hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    /// <summary>
    /// Classe les cartes : NVIDIA, puis AMD Radeon RX / Intel Arc dédiées, puis les puces intégrées
    /// (Radeon Graphics des Ryzen, Intel UHD / Iris), qui ne servent pas à jouer.
    /// </summary>
    internal static int GpuScore(IHardware hw) => GpuScore(hw.HardwareType, hw.Name);

    public static int GpuScore(HardwareType type, string name)
    {
        bool discrete = type switch
        {
            HardwareType.GpuNvidia => true,
            HardwareType.GpuAmd => IsDiscreteAmdName(name),
            // « Arc A770 », « Arc B580 » sont dédiées ; « Arc Graphics » (Core Ultra) est intégrée.
            HardwareType.GpuIntel => System.Text.RegularExpressions.Regex.IsMatch(name, @"Arc\S*\s+[AB]\d{3}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            _ => false,
        };
        int brand = type switch { HardwareType.GpuNvidia => 3, HardwareType.GpuAmd => 2, _ => 1 };
        return (discrete ? 10 : 0) + brand;
    }

    /// <summary>« AMD Radeon RX 7800 XT », « Radeon PRO W7600 », « Radeon VII » sont dédiées ; « AMD Radeon(TM) Graphics », « Radeon 780M » sont intégrées.</summary>
    public static bool IsDiscreteAmdName(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name,
            @"\bRX\s?\d|\bR[579]\s?\d{3}|\bPRO\s+W\d|Radeon\s+VII|Vega\s+(56|64)|Instinct|FirePro",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        && !System.Text.RegularExpressions.Regex.IsMatch(name, @"\d{3}M\b|Vega\s+\d\b|\bGraphics\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public SensorSnapshot Read()
    {
        lock (_gate)
        {
            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            var gpus = _computer.Hardware.Where(IsGpu).ToList();
            GpuNames = gpus.Select(g => g.Name).ToList();

            // La carte choisie dans les réglages, sinon la carte dédiée la plus puissante.
            var gpu = gpus.FirstOrDefault(g => g.Name == PreferredGpu)
                ?? gpus.OrderByDescending(GpuScore).FirstOrDefault();

            cpu?.Update();
            gpu?.Update();

            return new SensorSnapshot
            {
                CpuName = cpu?.Name,
                CpuTemperature = Pick(cpu, SensorType.Temperature,
                    "CPU Package", "Core (Tctl/Tdie)", "Tctl/Tdie", "Tdie", "Tctl", "Core Average", "Core Max"),
                CpuPower = Pick(cpu, SensorType.Power, "Package", "CPU Package"),
                CpuLoad = Pick(cpu, SensorType.Load, "CPU Total"),

                GpuName = gpu?.Name,
                GpuTemperature = Pick(gpu, SensorType.Temperature, "GPU Core", "GPU Hot Spot"),
                GpuPower = Pick(gpu, SensorType.Power, "GPU Package", "GPU Power", "GPU Core", "GPU Total"),
                GpuLoad = Pick(gpu, SensorType.Load, "GPU Core", "D3D 3D"),
            };
        }
    }

    /// <summary>Premier capteur trouvé dans l'ordre des noms donnés, sinon le premier capteur du bon type.</summary>
    private static float? Pick(IHardware? hardware, SensorType type, params string[] preferredNames)
    {
        if (hardware is null) return null;

        var sensors = hardware.Sensors.Concat(hardware.SubHardware.SelectMany(s => s.Sensors))
            .Where(s => s.SensorType == type && s.Value is > 0 and < 100_000)
            .ToList();

        foreach (var name in preferredNames)
        {
            var match = sensors.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Value;
        }
        return sensors.FirstOrDefault()?.Value;
    }

    public void Dispose()
    {
        lock (_gate) _computer.Close();
    }
}
