namespace SystemMonitor;

public class SystemMetricsModel
{
    public string DeviceId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string NetworkType { get; set; } = string.Empty;
    public string WifiSsid { get; set; } = string.Empty;
    public float CpuUsagePercent { get; set; }
    //public float CpuTemperature { get; set; }
    public double AvailableMemory { get; set; }
    public List<string> LogicalDisks { get; set; } = new();
    public List<string> PhysicalDisks { get; set; } = new();
    public string GpuName { get; set; } = string.Empty;
    public float GpuUsagePercent { get; set; }
    public string GpuMemory { get; set; } = string.Empty;
    public int DatabaseCount { get; set; }
    public List<string> InstalledDatabases { get; set; } = new();
    public List<string> SqlServerDatabaseNames { get; set; } = new();
    public DateTime Timestamp { get; set; }
}