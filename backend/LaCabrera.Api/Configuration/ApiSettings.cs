namespace LaCabrera.Api.Configuration;

public class ApiSettings
{
    public string SchemaVersionSupported { get; set; } = "1.0";
    public int MaxBatchSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    public double ControlTotalsTolerancePercent { get; set; } = 1.0;
}
