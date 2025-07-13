using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BendingMachine.Domain.Interfaces;

namespace BendingMachine.Driver.Services;

public class MachineStatusService : BackgroundService
{
    private readonly IMachineDriver _machineDriver;
    private readonly ILogger<MachineStatusService> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    // Configuration
    private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(100);
    private readonly SemaphoreSlim _updateSemaphore;
    
    // Status
    public bool IsRunning { get; private set; }
    public DateTime LastUpdateTime { get; private set; }
    public int UpdateCount { get; private set; }
    public TimeSpan AverageUpdateDuration { get; private set; }
    
    // Events for SignalR
    public event EventHandler<MachineStatusUpdatedEventArgs>? StatusUpdated;
    
    private readonly List<TimeSpan> _updateDurations = new();
    private readonly object _durationLock = new();
    
    public MachineStatusService(
        IMachineDriver machineDriver, 
        ILogger<MachineStatusService> logger)
    {
        _machineDriver = machineDriver;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        _updateSemaphore = new SemaphoreSlim(1, 1); // Ensure only one update at a time
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Machine Status Service started - Updates every {Interval}ms", 
            _updateInterval.TotalMilliseconds);
            
        IsRunning = true;
        
        try
        {
            // Connection monitoring
            bool wasConnected = false;
            
            // Wait for machine to be connected before starting updates
            while (!_machineDriver.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                if (!wasConnected) // Log sadece ilk kez
                {
                    _logger.LogInformation("Waiting for machine connection...");
                    wasConnected = false;
                }
                await Task.Delay(1000, stoppingToken);
            }
            
            if (_machineDriver.IsConnected)
            {
                _logger.LogInformation("Machine connected - Starting status updates");
                
                // Main update loop
                await UpdateLoop(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Machine Status Service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Machine Status Service failed");
        }
        finally
        {
            IsRunning = false;
        }
    }
    
    private async Task UpdateLoop(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var updateStart = DateTime.UtcNow;
            var updateStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Use semaphore to prevent overlapping updates
                if (await _updateSemaphore.WaitAsync(50, cancellationToken)) // 50ms timeout
                {
                    try
                    {
                        await PerformStatusUpdate(cancellationToken);
                        
                        LastUpdateTime = updateStart;
                        UpdateCount++;
                        
                        // Track performance
                        updateStopwatch.Stop();
                        TrackUpdateDuration(updateStopwatch.Elapsed);
                        
                        // Fire StatusUpdated event for SignalR
                        OnStatusUpdated();
                        
                        // Log every 100 updates (10 seconds)
                        if (UpdateCount % 100 == 0)
                        {
                            _logger.LogInformation(
                                "Status updates: {Count}, Average duration: {Duration}ms", 
                                UpdateCount, 
                                AverageUpdateDuration.TotalMilliseconds);
                        }
                    }
                    finally
                    {
                        _updateSemaphore.Release();
                    }
                }
                else
                {
                    _logger.LogWarning("Status update skipped - previous update still running");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during status update");
            }
            
            // Calculate next update time to maintain consistent interval
            var elapsed = stopwatch.Elapsed;
            var nextUpdate = _updateInterval - elapsed;
            
            if (nextUpdate > TimeSpan.Zero)
            {
                await Task.Delay(nextUpdate, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Status update taking longer than interval: {Duration}ms", 
                    elapsed.TotalMilliseconds);
            }
            
            stopwatch.Restart();
        }
    }
    
    private async Task PerformStatusUpdate(CancellationToken cancellationToken)
    {
        if (!_machineDriver.IsConnected)
        {
            _logger.LogWarning("Machine disconnected during status update - Modbus connection lost");
            
            // Connection status event'i tetikle
            OnStatusUpdated(); // Connection status değişikliği için tetikle
            return;
        }
        
        try
        {
            // Get latest machine status
            var status = await _machineDriver.GetMachineStatusAsync();
            
            // Additional processing if needed
            // (e.g., data validation, derived calculations)
            
            // Status'u SignalR'a gönder
            OnStatusUpdated();
            
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get machine status - Connection may be lost");
            throw; // Re-throw to trigger error handling in calling method
        }
    }
    
    private void TrackUpdateDuration(TimeSpan duration)
    {
        lock (_durationLock)
        {
            _updateDurations.Add(duration);
            
            // Keep only last 1000 samples for average calculation
            if (_updateDurations.Count > 1000)
            {
                _updateDurations.RemoveAt(0);
            }
            
            // Calculate average
            AverageUpdateDuration = TimeSpan.FromTicks(
                (long)_updateDurations.Average(d => d.Ticks));
        }
    }
    
    private void OnStatusUpdated()
    {
        try
        {
            StatusUpdated?.Invoke(this, new MachineStatusUpdatedEventArgs
            {
                UpdateTime = LastUpdateTime,
                UpdateCount = UpdateCount,
                AverageDuration = AverageUpdateDuration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error firing StatusUpdated event");
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Machine Status Service...");
        
        _cancellationTokenSource.Cancel();
        
        await base.StopAsync(cancellationToken);
        
        _updateSemaphore.Dispose();
        _cancellationTokenSource.Dispose();
        
        _logger.LogInformation("Machine Status Service stopped");
    }
}

public class MachineStatusUpdatedEventArgs : EventArgs
{
    public DateTime UpdateTime { get; set; }
    public int UpdateCount { get; set; }
    public TimeSpan AverageDuration { get; set; }
} 