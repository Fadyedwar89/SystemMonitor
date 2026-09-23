using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SystemMonitor;

public class Worker(ILogger<Worker>_logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
     
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        int secondsCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            SystemMetricsModel metrics = SystemMetricsCollector.CollectAllMetrics();
            string jsonOutput = JsonSerializer.Serialize(metrics, jsonOptions);
            _logger.LogInformation("System Metrics Payload:\n{Json}", jsonOutput);
            secondsCount += 3;
            if (secondsCount >= 15)
            {
                _logger.LogWarning("Testing LOCK command now...");
                ExecuteLockCommand();
                //ExecuteShutdownCommand();
                //ExecuteRestartCommand();
                break;
            }
            await Task.Delay(5000, stoppingToken);
        }
    }

    // method for Restart Command, Shutdown Command, Lock Command
    #region command
    private void ExecuteRestartCommand()
    {
        _logger.LogWarning("Received RESTART command! Restarting system in 5 seconds...");

        if (OperatingSystem.IsWindows())
        {

            Process.Start("shutdown", "/r /t 5 /c \"SystemMonitor Request\"");
        }
    }
    private void ExecuteShutdownCommand()
    {
        _logger.LogWarning("Received SHUTDOWN command! Shutting down in 5 seconds...");

        if (OperatingSystem.IsWindows())
        {
            Process.Start("shutdown", "/s /t 5 /c \"SystemMonitor Request\"");
        }
    }
    private void ExecuteLockCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            LockWorkStation();
        }
    }


    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();
    #endregion
}