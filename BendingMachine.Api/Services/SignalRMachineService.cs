using Microsoft.AspNetCore.SignalR;
using BendingMachine.Api.Hubs;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Driver.Services;

namespace BendingMachine.Api.Services;

public class SignalRMachineService : IHostedService
{
    private readonly IHubContext<MachineStatusHub> _hubContext;
    private readonly MachineStatusService _machineStatusService;
    private readonly IMachineDriver _machineDriver;
    private readonly ILogger<SignalRMachineService> _logger;
    
    public SignalRMachineService(
        IHubContext<MachineStatusHub> hubContext,
        MachineStatusService machineStatusService,
        IMachineDriver machineDriver,
        ILogger<SignalRMachineService> logger)
    {
        _hubContext = hubContext;
        _machineStatusService = machineStatusService;
        _machineDriver = machineDriver;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR Machine Service starting...");
        
        // Background Service events'leri SignalR Hub'a yönlendir
        _machineStatusService.StatusUpdated += OnMachineStatusUpdated;
        
        // Machine Driver events'leri SignalR Hub'a yönlendir
        _machineDriver.StatusChanged += OnMachineStatusChanged;
        _machineDriver.PistonMoved += OnPistonMoved;
        _machineDriver.AlarmRaised += OnAlarmRaised;
        _machineDriver.SafetyViolation += OnSafetyViolation;
        
        _logger.LogInformation("SignalR Machine Service started - Event bridges established");
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR Machine Service stopping...");
        
        // Events'leri unsubscribe et
        _machineStatusService.StatusUpdated -= OnMachineStatusUpdated;
        _machineDriver.StatusChanged -= OnMachineStatusChanged;
        _machineDriver.PistonMoved -= OnPistonMoved;
        _machineDriver.AlarmRaised -= OnAlarmRaised;
        _machineDriver.SafetyViolation -= OnSafetyViolation;
        
        _logger.LogInformation("SignalR Machine Service stopped");
        
        return Task.CompletedTask;
    }
    
    #region Event Handlers - Background Service → SignalR
    
    private async void OnMachineStatusUpdated(object? sender, MachineStatusUpdatedEventArgs e)
    {
        try
        {
            // Her 100ms'de bir gelecek - tüm client'lara machine status gönder
            var status = await _machineDriver.GetMachineStatusAsync();
            
            // Makine durumunu MachineUsers grubuna gönder
            await _hubContext.Clients.Group("MachineUsers").SendAsync("MachineStatusUpdate", status);
            
            // Performance monitoring - her 10 saniyede bir gönder
            if (e.UpdateCount % 100 == 0) // 100 * 100ms = 10 saniye
            {
                await _hubContext.Clients.Group("MachineUsers").SendAsync("PerformanceUpdate", new 
                {
                    UpdateCount = e.UpdateCount,
                    AverageDuration = e.AverageDuration.TotalMilliseconds,
                    LastUpdateTime = e.UpdateTime
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting machine status update");
        }
    }
    
    #endregion
    
    #region Event Handlers - Machine Driver → SignalR
    
    private async void OnMachineStatusChanged(object? sender, MachineStatusChangedEventArgs e)
    {
        try
        {
            // Machine status değişikliği - immediate broadcast
            await _hubContext.Clients.Group("MachineUsers").SendAsync("MachineStatusUpdate", e.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting machine status change");
        }
    }
    
    private async void OnPistonMoved(object? sender, PistonMovedEventArgs e)
    {
        try
        {
            // Piston hareket bildirimi
            await _hubContext.Clients.Group("MachineUsers").SendAsync("PistonMoved", new 
            {
                PistonType = e.PistonType.ToString(),
                CurrentPosition = e.CurrentPosition,
                TargetPosition = e.TargetPosition,
                Motion = e.Motion.ToString(),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting piston moved event");
        }
    }
    
    private async void OnAlarmRaised(object? sender, AlarmEventArgs e)
    {
        try
        {
            // Alarm durumu - CRITICAL
            await _hubContext.Clients.All.SendAsync("AlarmRaised", new 
            {
                Message = e.AlarmMessage,
                Severity = e.Severity.ToString(),
                Timestamp = e.Timestamp
            });
            
            _logger.LogWarning("Alarm broadcasted: {Message}, Severity: {Severity}", 
                e.AlarmMessage, e.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting alarm");
        }
    }
    
    private async void OnSafetyViolation(object? sender, SafetyEventArgs e)
    {
        try
        {
            // Güvenlik ihlali - CRITICAL  
            await _hubContext.Clients.All.SendAsync("SafetyViolation", new 
            {
                ViolationType = e.ViolationType,
                RequiresEmergencyStop = e.RequiresEmergencyStop,
                Timestamp = e.Timestamp
            });
            
            _logger.LogCritical("Safety violation broadcasted: {ViolationType}, Emergency Stop Required: {EmergencyStop}", 
                e.ViolationType, e.RequiresEmergencyStop);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting safety violation");
        }
    }
    
    #endregion
    
    #region Public Methods for Manual Broadcasting
    
    public async Task BroadcastConnectionStatus(bool isConnected, string? message = null)
    {
        try
        {
            if (isConnected)
            {
                await _hubContext.Clients.All.SendAsync("MachineConnected", new 
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message ?? "Machine connected successfully"
                });
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("MachineDisconnected", new 
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message ?? "Machine connection lost - Check Modbus connection"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting connection status");
        }
    }
    
    public async Task BroadcastSystemMessage(string message, string level = "Info")
    {
        try
        {
            await _hubContext.Clients.Group("MachineUsers").SendAsync("SystemMessage", new 
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system message");
        }
    }
    
    #endregion
} 