namespace UXWindows.Core.Sensors;

/// <summary>Une lecture des capteurs. Une valeur absente (null) veut dire « non disponible ».</summary>
public sealed record SensorSnapshot
{
    public string? CpuName { get; init; }
    public float? CpuTemperature { get; init; }
    public float? CpuPower { get; init; }
    public float? CpuLoad { get; init; }

    public string? GpuName { get; init; }
    public float? GpuTemperature { get; init; }
    public float? GpuPower { get; init; }
    public float? GpuLoad { get; init; }

    public DateTime TakenAt { get; init; } = DateTime.Now;

    public static readonly SensorSnapshot Empty = new();

    /// <summary>
    /// Prend le processeur dans la lecture de l'assistant administrateur (seul à le voir),
    /// et garde la carte graphique de cette lecture-ci (celle choisie dans les réglages).
    /// </summary>
    public SensorSnapshot WithCpuFrom(SensorSnapshot? helper) => helper is null ? this : this with
    {
        CpuName = helper.CpuName ?? CpuName,
        CpuTemperature = helper.CpuTemperature ?? CpuTemperature,
        CpuPower = helper.CpuPower ?? CpuPower,
        CpuLoad = helper.CpuLoad ?? CpuLoad,
        GpuName = GpuName ?? helper.GpuName,
        GpuTemperature = GpuName is null ? helper.GpuTemperature : GpuTemperature,
        GpuPower = GpuName is null ? helper.GpuPower : GpuPower,
        GpuLoad = GpuName is null ? helper.GpuLoad : GpuLoad,
    };
}
