using Microsoft.AspNetCore.SignalR;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Domain.Entities;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Api.Hubs;

public class MachineStatusHub : Hub
{
    private readonly IMachineDriver _machineDriver;
    private readonly ILogger<MachineStatusHub> _logger;
    
    public MachineStatusHub(IMachineDriver machineDriver, ILogger<MachineStatusHub> logger)
    {
        _machineDriver = machineDriver;
        _logger = logger;
    }
    
    // Client bağlantı yönetimi
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString();
        
        _logger.LogInformation("Client connected: {ConnectionId}, UserAgent: {UserAgent}", 
            connectionId, userAgent);
        
        // Client'i "MachineUsers" grubuna ekle
        await Groups.AddToGroupAsync(connectionId, "MachineUsers");
        
        // İlk connection'da mevcut status'u gönder
        try
        {
            if (_machineDriver.IsConnected)
            {
                var currentStatus = await _machineDriver.GetMachineStatusAsync();
                await Clients.Caller.SendAsync("MachineStatusUpdate", currentStatus);
                
                var allPistons = await _machineDriver.GetAllPistonsAsync();
                await Clients.Caller.SendAsync("PistonStatusUpdate", allPistons);
            }
            else
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending initial status to client {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("Error", "Failed to get machine status");
        }
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        if (exception != null)
        {
            _logger.LogWarning("Client disconnected with error: {ConnectionId}, Error: {Error}", 
                connectionId, exception.Message);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        }
        
        // Client'i gruptan çıkar
        await Groups.RemoveFromGroupAsync(connectionId, "MachineUsers");
        
        await base.OnDisconnectedAsync(exception);
    }
    
    // Client'tan gelen komutlar
    [HubMethodName("RequestMachineStatus")]
    public async Task RequestMachineStatus()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            var status = await _machineDriver.GetMachineStatusAsync();
            await Clients.Caller.SendAsync("MachineStatusUpdate", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RequestMachineStatus from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get machine status: {ex.Message}");
        }
    }
    
    [HubMethodName("RequestPistonStatus")]
    public async Task RequestPistonStatus()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            var pistons = await _machineDriver.GetAllPistonsAsync();
            await Clients.Caller.SendAsync("PistonStatusUpdate", pistons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RequestPistonStatus from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get piston status: {ex.Message}");
        }
    }
    
    [HubMethodName("MovePiston")]
    public async Task MovePiston(string pistonType, double voltage)
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                await Clients.Caller.SendAsync("SafetyViolation", "System not safe for operation");
                return;
            }
            
            // Enum dönüşümü
            if (!Enum.TryParse<PistonType>(pistonType, out var parsedPistonType))
            {
                await Clients.Caller.SendAsync("Error", "Invalid piston type");
                return;
            }
            
            // Voltaj limitlerini kontrol et
            if (voltage < -10 || voltage > 10)
            {
                await Clients.Caller.SendAsync("Error", "Voltage must be between -10V and +10V");
                return;
            }
            
            var result = await _machineDriver.MovePistonAsync(parsedPistonType, voltage);
            
            if (result)
            {
                await Clients.All.SendAsync("PistonCommandSuccess", new 
                { 
                    PistonType = pistonType, 
                    Voltage = voltage,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to move piston");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MovePiston command from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Piston command failed: {ex.Message}");
        }
    }
    
    [HubMethodName("StopPiston")]
    public async Task StopPiston(string pistonType)
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            if (!Enum.TryParse<PistonType>(pistonType, out var parsedPistonType))
            {
                await Clients.Caller.SendAsync("Error", "Invalid piston type");
                return;
            }
            
            var result = await _machineDriver.StopPistonAsync(parsedPistonType);
            
            if (result)
            {
                await Clients.All.SendAsync("PistonStopped", new 
                { 
                    PistonType = pistonType,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to stop piston");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StopPiston command from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Stop piston command failed: {ex.Message}");
        }
    }
    
    [HubMethodName("EmergencyStop")]
    public async Task EmergencyStop()
    {
        try
        {
            _logger.LogWarning("Emergency Stop triggered by client {ConnectionId}", Context.ConnectionId);
            
            var result = await _machineDriver.EmergencyStopAsync();
            
            if (result)
            {
                // Tüm client'lara emergency stop bildir
                await Clients.All.SendAsync("EmergencyStopActivated", new 
                { 
                    Timestamp = DateTime.UtcNow,
                    TriggeredBy = Context.ConnectionId
                });
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Emergency stop failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling EmergencyStop from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Emergency stop failed: {ex.Message}");
        }
    }
    
    [HubMethodName("StartHydraulicMotor")]
    public async Task StartHydraulicMotor()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            var result = await _machineDriver.StartHydraulicMotorAsync();
            
            if (result)
            {
                await Clients.All.SendAsync("HydraulicMotorStarted", DateTime.UtcNow);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to start hydraulic motor");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting hydraulic motor from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to start hydraulic motor: {ex.Message}");
        }
    }
    
    [HubMethodName("StopHydraulicMotor")]
    public async Task StopHydraulicMotor()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }
            
            var result = await _machineDriver.StopHydraulicMotorAsync();
            
            if (result)
            {
                await Clients.All.SendAsync("HydraulicMotorStopped", DateTime.UtcNow);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to stop hydraulic motor");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping hydraulic motor from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to stop hydraulic motor: {ex.Message}");
        }
    }
    
    // Utility methods
    public async Task BroadcastToMachineUsers(string method, object data)
    {
        await Clients.Group("MachineUsers").SendAsync(method, data);
    }
    
    public int GetConnectedClientCount()
    {
        // TODO: Implement actual client count tracking
        return 1;
    }

    // ✅ YENİ ÖZELLİKLER - Real-time Precision Control Broadcasting

    /// <summary>
    /// Gerçek basınç değerlerini real-time olarak yayınlar
    /// </summary>
    [HubMethodName("RequestRealTimePressure")]
    public async Task RequestRealTimePressure()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }

            var status = await _machineDriver.GetMachineStatusAsync();
            
            // ✅ DÜZELTME: status.S1OilPressure ve S2OilPressure ZATEN bar cinsinden!
            // MachineDriver'da RegisterToBarAndMilliamps ile dönüştürülmüş durumdalar
            var s1Pressure = status.S1OilPressure; // Zaten bar cinsinden
            var s2Pressure = status.S2OilPressure; // Zaten bar cinsinden  
            var maxPressure = Math.Max(s1Pressure, s2Pressure);

            await Clients.Caller.SendAsync("RealTimePressureUpdate", new
            {
                S1Pressure = Math.Round(s1Pressure, 2),
                S2Pressure = Math.Round(s2Pressure, 2),
                MaxPressure = Math.Round(maxPressure, 2),
                S1Raw = s1Pressure, // Bar değerini raw olarak gönder (debug için)
                S2Raw = s2Pressure, // Bar değerini raw olarak gönder (debug için)
                Unit = "bar",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RequestRealTimePressure from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get real-time pressure: {ex.Message}");
        }
    }

    /// <summary>
    /// Encoder durumunu real-time olarak yayınlar
    /// </summary>
    [HubMethodName("RequestEncoderStatus")]
    public async Task RequestEncoderStatus()
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }

            var status = await _machineDriver.GetMachineStatusAsync();
            var currentDistance = Math.Round((status.RotationEncoderRaw * Math.PI * 220.0) / 1024.0, 2);

            await Clients.Caller.SendAsync("EncoderStatusUpdate", new
            {
                CurrentPosition = status.RotationEncoderRaw,
                CurrentDistance = currentDistance,
                EncoderType = "RV3100",
                PulsesPerRevolution = 1024,
                BallDiameter = 220.0,
                IsHealthy = true, // TODO: Encoder sağlık kontrolü
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RequestEncoderStatus from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get encoder status: {ex.Message}");
        }
    }

    /// <summary>
    /// Hassas rotasyon başlatır
    /// </summary>
    [HubMethodName("StartPreciseRotation")]
    public async Task StartPreciseRotation(string direction, double speed)
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }

            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                await Clients.Caller.SendAsync("SafetyViolation", "System not safe for operation");
                return;
            }

            // Hız limitlerini kontrol et
            if (speed < 1.0 || speed > 100.0)
            {
                await Clients.Caller.SendAsync("Error", "Speed must be between 1% and 100%");
                return;
            }

            // Direction'ı kontrol et
            if (!Enum.TryParse<RotationDirection>(direction, true, out var rotationDirection))
            {
                await Clients.Caller.SendAsync("Error", "Invalid rotation direction");
                return;
            }

            var result = await _machineDriver.StartRotationAsync(rotationDirection, speed);

            if (result)
            {
                await Clients.All.SendAsync("PreciseRotationStarted", new
                {
                    Direction = direction,
                    Speed = speed,
                    Mode = "Precision Control",
                    EncoderMonitoring = "Active",
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to start precise rotation");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StartPreciseRotation command from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Precise rotation command failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Parça sıfırlama işlemini başlatır (hassas konumlandırma ile)
    /// </summary>
    [HubMethodName("ResetPartPosition")]
    public async Task ResetPartPosition(double resetDistance)
    {
        try
        {
            if (!_machineDriver.IsConnected)
            {
                await Clients.Caller.SendAsync("MachineDisconnected", "Machine is not connected");
                return;
            }

            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                await Clients.Caller.SendAsync("SafetyViolation", "System not safe for operation");
                return;
            }

            // Mesafe limitlerini kontrol et
            if (resetDistance < 100.0 || resetDistance > 1000.0)
            {
                await Clients.Caller.SendAsync("Error", "Reset distance must be between 100mm and 1000mm");
                return;
            }

            var result = await _machineDriver.ResetPartPositionAsync(resetDistance);

            if (result)
            {
                await Clients.All.SendAsync("PartPositionReset", new
                {
                    ResetDistance = resetDistance,
                    Algorithm = "3-phase precision positioning (Fast→Medium→Slow)",
                    Safety = "Encoder freeze monitoring active",
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Failed to reset part position");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ResetPartPosition command from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Part position reset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Hassas konumlandırma konfigürasyonunu gönderir
    /// </summary>
    [HubMethodName("RequestPrecisionConfig")]
    public async Task RequestPrecisionConfig()
    {
        try
        {
            await Clients.Caller.SendAsync("PrecisionConfigUpdate", new
            {
                PrecisionControl = new
                {
                    FastSpeed = 70.0,   // %70 - İlk %80 mesafe
                    MediumSpeed = 40.0, // %40 - %80-95 mesafe
                    SlowSpeed = 15.0,   // %15 - Son %5 mesafe
                    PreciseSpeed = 20.0 // %20 - Hassas konumlandırma
                },
                MachineParameters = new
                {
                    BallDiameter = 220.0, // mm
                    EncoderPulsesPerRevolution = 1024,
                    PressureRange = "0-400 bar",
                    MaxPistonVoltage = "±10V"
                },
                SafetySettings = new
                {
                    EncoderFreezeTimeout = 2.0, // seconds
                    MaxEncoderStuckCount = 3,
                    OvershootProtection = true
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RequestPrecisionConfig from {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get precision config: {ex.Message}");
        }
    }

    /// <summary>
    /// Büküm ilerleme durumunu yayınlar
    /// </summary>
    [HubMethodName("BroadcastBendingProgress")]
    public async Task BroadcastBendingProgress(int currentStep, int totalSteps, string currentOperation)
    {
        try
        {
            await Clients.Group("MachineUsers").SendAsync("BendingProgressUpdate", new
            {
                CurrentStep = currentStep,
                TotalSteps = totalSteps,
                Progress = (int)((double)currentStep / totalSteps * 100),
                CurrentOperation = currentOperation,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting bending progress");
            await Clients.Caller.SendAsync("Error", $"Failed to broadcast progress: {ex.Message}");
        }
    }

    [HubMethodName("JoinMachineUsersGroup")]
    public async Task JoinMachineUsersGroup()
    {
        try
        {
            var connectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(connectionId, "MachineUsers");
            _logger.LogInformation("Client {ConnectionId} joined MachineUsers group", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding client to MachineUsers group");
            throw;
        }
    }
} 