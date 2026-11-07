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

    /// <summary>Complète les valeurs manquantes avec celles d'une autre lecture.</summary>
    public SensorSnapshot FillFrom(SensorSnapshot? other) => other is null ? this : this with
    {
        CpuName = CpuName ?? other.CpuName,
        CpuTemperature = CpuTemperature ?? other.CpuTemperature,
        CpuPower = CpuPower ?? other.CpuPower,
        CpuLoad = CpuLoad ?? other.CpuLoad,
        GpuName = GpuName ?? other.GpuName,
        GpuTemperature = GpuTemperature ?? other.GpuTemperature,
        GpuPower = GpuPower ?? other.GpuPower,
        GpuLoad = GpuLoad ?? other.GpuLoad,
    };
}
