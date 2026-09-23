using Microsoft.Data.SqlClient;
using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;


namespace SystemMonitor;

public static class SystemMetricsCollector
{
    public static SystemMetricsModel CollectAllMetrics()
    {
        var (hostName, ipAddress, networkType) = GetNetworkDetails();
        var (gpuName, gpuMemory) = GetGpuDetails();
        var (dbCount, dbList) = GetInstalledDatabases();

        return new SystemMetricsModel
        {
            DeviceId = GetDeviceId(),
            HostName = hostName,
            OperatingSystem = RuntimeInformation.OSDescription,
            IpAddress = ipAddress,
            NetworkType = networkType,
            WifiSsid = GetWifiSsid(),
            CpuUsagePercent = GetSystemCpuUsage(),
            //CpuTemperature = GetCpuTemperatureLibre(),
            GpuName = gpuName,
            GpuUsagePercent = GetGpuUsagePercent(),
            GpuMemory = gpuMemory,
            AvailableMemory = GetAvailableMemoryGB(),
            LogicalDisks = GetAllDisksUsage(),
            PhysicalDisks = GetPhysicalDisksTotalSize(),
            DatabaseCount = dbCount,
            InstalledDatabases = dbList,
            SqlServerDatabaseNames = GetLocalSqlServerDatabases(),
            Timestamp = DateTime.Now
        };
    }

    private static string GetDeviceId()
    {
        if (!OperatingSystem.IsWindows()) return "N/A";
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["UUID"]?.ToString() ?? "N/A";
            }
        }
        catch { }
        return "N/A";
    }

    private static (string hostName, string ipAddress, string connectionType) GetNetworkDetails()
    {
        string hostName = Dns.GetHostName();
        string ipAddress = "Disconnected";
        string connectionType = "No Connection";

        try
        {
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.OperationalStatus == OperationalStatus.Up &&
                    netInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    netInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    connectionType = netInterface.NetworkInterfaceType.ToString();
                    IPInterfaceProperties ipProps = netInterface.GetIPProperties();

                    foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddress = addr.Address.ToString();
                            return (hostName, ipAddress, connectionType);
                        }
                    }
                }
            }
        }
        catch { }

        return (hostName, ipAddress, connectionType);
    }

    private static string GetWifiSsid()
    {
        if (!OperatingSystem.IsWindows()) return "N/A";
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("SSID") && !line.Contains("BSSID"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1) return parts[1].Trim();
                }
            }
        }
        catch { }

        return "Not Connected to Wi-Fi";
    }

    private static float GetSystemCpuUsage()
    {
        if (!OperatingSystem.IsWindows()) return 0.0f;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToSingle(obj["LoadPercentage"]);
            }
        }
        catch { }
        return 0.0f;
    }

    //private static float GetCpuTemperatureLibre()
    //{
    //    try
    //    {
    //        var computer = new Computer
    //        {
    //            IsCpuEnabled = true
    //        };

    //        computer.Open();

    //        foreach (IHardware hardware in computer.Hardware)
    //        {
    //            if (hardware.HardwareType == HardwareType.Cpu)
    //            {
    //                hardware.Update();

    //                foreach (ISensor sensor in hardware.Sensors)
    //                {
    //                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
    //                    {
    //                        if (sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase) ||
    //                            sensor.Name.Equals("Core Average", StringComparison.OrdinalIgnoreCase) ||
    //                            sensor.Name.Equals("Core Max", StringComparison.OrdinalIgnoreCase))
    //                        {
    //                            float temp = sensor.Value.Value;
    //                            computer.Close();
    //                            return temp;
    //                        }
    //                    }
    //                }
    //            }
    //        }

    //        computer.Close();
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"[CPU Temp Error]: {ex.Message}");
    //    }

    //    return 0.0f;
    //}

    private static double GetAvailableMemoryGB()
    {
        if (!OperatingSystem.IsWindows()) return 0;

        using var searcher = new ManagementObjectSearcher(
                 "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

        using var collection = searcher.Get();
        double totalGB;
        foreach (ManagementObject obj in collection)
        {

            double totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
            totalGB = totalKB / (1024.0 * 1024.0);
            return Math.Ceiling(totalGB);
            
        }
        return 0.0;
    }

    private static List<string> GetAllDisksUsage()
    {
        var diskReport = new List<string>();
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    long totalBytes = drive.TotalSize;
                    long freeBytes = drive.AvailableFreeSpace;
                    long usedBytes = totalBytes - freeBytes;

                    float usedPercentage = (float)usedBytes / totalBytes * 100;
                    double freeSpaceGB = (double)freeBytes / (1024 * 1024 * 1024);

                    diskReport.Add($"{drive.Name.Replace("\\", "")} {usedPercentage:F1}% Used ({freeSpaceGB:F1} GB Free)");
                }
            }
        }
        catch { }
        return diskReport;
    }

    private static List<string> GetPhysicalDisksTotalSize()
    {
        var physicalDisks = new List<string>();
        if (!OperatingSystem.IsWindows()) return physicalDisks;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
            int diskNumber = 1;
            foreach (ManagementObject drive in searcher.Get())
            {
                if (drive["Size"] != null)
                {
                    ulong totalBytes = Convert.ToUInt64(drive["Size"]);
                    double totalGB = (double)totalBytes / (1024 * 1024 * 1024);
                    physicalDisks.Add($"Disk {diskNumber}: {totalGB:F0} GB");
                    diskNumber++;
                }
            }
        }
        catch { }
        return physicalDisks;
    }

    private static (string name, string memory) GetGpuDetails()
    {
        if (!OperatingSystem.IsWindows()) return ("N/A", "N/A");

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                string name = obj["Name"]?.ToString() ?? "N/A";

             
                if (name.Contains("Virtual") || name.Contains("Basic Display")) continue;

                string memory = "N/A";
                if (obj["AdapterRAM"] != null)
                {
                    ulong bytes = Convert.ToUInt64(obj["AdapterRAM"]);
                    double mb = (double)bytes / (1024 * 1024);
                    memory = mb >= 1024
                        ? $"{(mb / 1024):F1} GB"
                        : $"{mb:F0} MB";
                }

                return (name, memory);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WMI GPU Error]: {ex.Message}");
        }

        return ("N/A", "N/A");
    }

    private static float GetGpuUsagePercent()
    {
        if (!OperatingSystem.IsWindows()) return 0.0f;

        try
        {
        
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%3D%'"
            );
            using var collection = searcher.Get();

            float maxUsage = 0.0f;
            foreach (ManagementObject obj in collection)
            {
                if (obj["UtilizationPercentage"] != null)
                {
                    float usage = Convert.ToSingle(obj["UtilizationPercentage"]);
                    if (usage > maxUsage) maxUsage = usage;
                }
            }
            return maxUsage;
        }
        catch
        {
         
        }

        return 0.0f;
    }

    private static (int count, List<string> dbList) GetInstalledDatabases()
    {
        var installedDbs = new List<string>();

        if (!OperatingSystem.IsWindows()) return (0, installedDbs);

        try
        {
            
            var targetServices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "MSSQLSERVER", "Microsoft SQL Server (Default Instance)" },
            { "MSSQL$", "Microsoft SQL Server (Named Instance)" },
            { "MYSQL", "MySQL Database Server" },
            { "POSTGRESQL", "PostgreSQL Database Server" },
            { "ORACLE", "Oracle Database Server" },
            { "MONGODB", "MongoDB Service" },
            { "REDIS", "Redis Cache / Database" },
            { "MARIADB", "MariaDB Server" }
        };

            ServiceController[] services = ServiceController.GetServices();

            foreach (var service in services)
            {
                foreach (var target in targetServices)
                {
                   
                    if (service.ServiceName.StartsWith(target.Key, StringComparison.OrdinalIgnoreCase) ||
                        service.DisplayName.Contains(target.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        string status = service.Status == ServiceControllerStatus.Running ? "Running" : "Stopped";
                        string dbInfo = $"{service.DisplayName} [{status}]";

                        if (!installedDbs.Contains(dbInfo))
                        {
                            installedDbs.Add(dbInfo);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database Detection Error]: {ex.Message}");
        }

        return (installedDbs.Count, installedDbs);
    }

    private static List<string> GetLocalSqlServerDatabases()
    {
        var databaseNames = new List<string>();

        if (!OperatingSystem.IsWindows()) return databaseNames;

        string connectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=2;";

        try
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT name FROM sys.databases WHERE database_id > 4 AND state_desc = 'ONLINE'";

                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        databaseNames.Add(reader["name"].ToString()!);
                    }
                }
            }
        }
        catch
        {
        }

        return databaseNames;
    }

}