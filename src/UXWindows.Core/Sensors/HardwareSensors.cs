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

    public SensorSnapshot Read()
    {
        lock (_gate)
        {
            IHardware? cpu = null;
            IHardware? gpu = null;

            foreach (var hw in _computer.Hardware)
            {
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        cpu ??= hw;
                        break;
                    // On préfère une carte graphique dédiée à la puce intégrée.
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                        if (gpu is null || gpu.HardwareType == HardwareType.GpuIntel) gpu = hw;
                        break;
                    case HardwareType.GpuIntel:
                        gpu ??= hw;
                        break;
                }
            }

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
