using AutoMapper;
using Microsoft.Extensions.Logging;
using BendingMachine.Application.DTOs;
using BendingMachine.Application.Interfaces;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Domain.Enums;
using BendingMachine.Domain.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using BendingMachine.Domain.Entities;

namespace BendingMachine.Application.Services;

public class MachineService : IMachineService
{
    private readonly IMachineDriver _machineDriver;
    private readonly IMapper _mapper;
    private readonly ILogger<MachineService> _logger;

    // Events
    public event EventHandler<MachineStatusDto>? StatusChanged;
    public event EventHandler<string>? AlarmRaised;
    public event EventHandler<string>? SafetyViolation;

    public MachineService(
        IMachineDriver machineDriver,
        IMapper mapper,
        ILogger<MachineService> logger)
    {
        _machineDriver = machineDriver;
        _mapper = mapper;
        _logger = logger;

        // Driver events'lerini Application layer events'lerine yönlendir
        _machineDriver.StatusChanged += OnDriverStatusChanged;
        _machineDriver.AlarmRaised += OnDriverAlarmRaised;
        _machineDriver.SafetyViolation += OnDriverSafetyViolation;
    }

    #region Connection Management

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _logger.LogInformation("Makine bağlantısı başlatılıyor...");
            
            var result = await _machineDriver.ConnectAsync();
            
            if (result)
            {
                _logger.LogInformation("Makine bağlantısı başarılı");
            }
            else
            {
                _logger.LogWarning("Makine bağlantısı başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine bağlantısı sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Makine bağlantısı kesiliyor...");
            
            var result = await _machineDriver.DisconnectAsync();
            
            if (result)
            {
                _logger.LogInformation("Makine bağlantısı başarıyla kesildi");
            }
            else
            {
                _logger.LogWarning("Makine bağlantısını kesme işlemi başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine bağlantısını kesme sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> IsConnectedAsync()
    {
        return await Task.FromResult(_machineDriver.IsConnected);
    }

    #endregion

    #region Status Management

    public async Task<MachineStatusDto> GetMachineStatusAsync()
    {
        try
        {
            var machineStatus = await _machineDriver.GetMachineStatusAsync();
            var statusDto = _mapper.Map<MachineStatusDto>(machineStatus);
            
            // IsConnected değerini driver'dan al
            statusDto.IsConnected = _machineDriver.IsConnected;
            
            // Pistonları al ve DTO'ya map et
            var pistons = await _machineDriver.GetAllPistonsAsync();
            statusDto.Pistons = _mapper.Map<List<PistonStatusDto>>(pistons);
            
            return statusDto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine durumu alınırken hata oluştu");
            throw;
        }
    }

    public async Task<bool> CheckSafetyAsync()
    {
        try
        {
            var result = await _machineDriver.CheckSafetyAsync();
            
            if (!result)
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - sistem güvenli değil");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güvenlik kontrolü sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> EmergencyStopAsync()
    {
        try
        {
            _logger.LogCritical("ACİL DURDURMA aktivasyonu!");
            
            var result = await _machineDriver.EmergencyStopAsync();
            
            if (result)
            {
                _logger.LogInformation("Acil durdurma başarıyla çalıştırıldı");
            }
            else
            {
                _logger.LogError("Acil durdurma başarısız!");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acil durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResetAlarmAsync()
    {
        try
        {
            _logger.LogInformation("Alarm sıfırlanıyor...");
            
            var result = await _machineDriver.ResetAlarmAsync();
            
            if (result)
            {
                _logger.LogInformation("Alarm başarıyla sıfırlandı");
            }
            else
            {
                _logger.LogWarning("Alarm sıfırlama başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alarm sıfırlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Configuration Management
    
    public async Task<MachineConfiguration> GetMachineConfigurationAsync()
    {
        try
        {
            // TODO: Repository pattern ile configuration okuma
            var configPath = Path.Combine("Infrastructure", "Configuration", "machineConfig.json");
            
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Configuration file not found: {ConfigPath}", configPath);
                return CreateDefaultConfiguration();
            }

            var jsonContent = await File.ReadAllTextAsync(configPath);
            var configuration = JsonSerializer.Deserialize<MachineConfiguration>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Machine configuration loaded successfully");
            return configuration ?? CreateDefaultConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading machine configuration");
            return CreateDefaultConfiguration();
        }
    }

    public async Task<bool> UpdateMachineConfigurationAsync(MachineConfiguration configuration)
    {
        try
        {
            // Validate configuration before saving
            if (!ValidateConfiguration(configuration))
            {
                _logger.LogWarning("Invalid configuration provided");
                return false;
            }

            // TODO: Repository pattern ile configuration yazma
            var configPath = Path.Combine("Infrastructure", "Configuration", "machineConfig.json");
            
            var jsonContent = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(configPath, jsonContent);
            
            _logger.LogInformation("Machine configuration updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating machine configuration");
            return false;
        }
    }

    public async Task<bool> SaveConfigurationToFileAsync()
    {
        try
        {
            // Driver'dan güncel konfigürasyonu al ve kaydet
            await _machineDriver.SaveConfigurationAsync();
            _logger.LogInformation("Configuration saved to file successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to file");
            return false;
        }
    }

    public async Task<bool> LoadConfigurationFromFileAsync()
    {
        try
        {
            // Driver'da konfigürasyonu dosyadan yükle
            await _machineDriver.LoadConfigurationAsync();
            _logger.LogInformation("Configuration loaded from file successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration from file");
            return false;
        }
    }

    private MachineConfiguration CreateDefaultConfiguration()
    {
        return new MachineConfiguration
        {
            Modbus = new ModbusSettings
            {
                IpAddress = "192.168.1.100",
                Port = 502,
                SlaveId = 1,
                TimeoutMs = 3000,
                RetryCount = 3,
                UpdateIntervalMs = 100
            },
            Stages = new StageSettings
            {
                Stages = new List<StageConfig>
                {
                    new() { Name = "Stage 0", Value = 0, LeftPistonOffset = 0, RightPistonOffset = 0 },
                    new() { Name = "Stage 60", Value = 60, LeftPistonOffset = 67.34, RightPistonOffset = 67.34 },
                    new() { Name = "Stage 120", Value = 120, LeftPistonOffset = 134.68, RightPistonOffset = 134.68 }
                }
            },
            Balls = new BallSettings
            {
                TopBallDiameter = 220,
                BottomBallDiameter = 220,
                LeftBallDiameter = 220,
                RightBallDiameter = 220,
                TopBallReferenceMaxHeight = 473
            },
            Geometry = new GeometrySettings
            {
                TriangleWidth = 493,
                TriangleAngle = 27,
                DefaultProfileHeight = 80,
                DefaultBendingRadius = 500,
                StepSize = 20
            },
            Safety = new SafetySettings
            {
                MaxPressure = 250,
                DefaultTargetPressure = 50,
                PressureTolerance = 5,
                WorkingOilTemperature = 40,
                MaxOilTemperature = 80,
                MinOilLevel = 20,
                FanOnTemperature = 50,
                FanOffTemperature = 40
            }
        };
    }

    private bool ValidateConfiguration(MachineConfiguration configuration)
    {
        if (configuration == null) return false;
        
        // Basic validation
        if (configuration.Modbus?.Port <= 0 || configuration.Modbus?.Port > 65535) return false;
        if (configuration.Safety?.MaxPressure <= 0 || configuration.Safety?.MaxPressure > 1000) return false;
        if (configuration.Balls?.TopBallDiameter <= 0 || configuration.Balls?.TopBallDiameter > 500) return false;
        
        return true;
    }

    #endregion

    #region Motor Control

    public async Task<bool> StartHydraulicMotorAsync()
    {
        try
        {
            _logger.LogInformation("Hidrolik motor başlatılıyor...");
            
            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - hidrolik motor başlatılamaz");
                return false;
            }
            
            var result = await _machineDriver.StartHydraulicMotorAsync();
            
            if (result)
            {
                _logger.LogInformation("Hidrolik motor başarıyla başlatıldı");
            }
            else
            {
                _logger.LogWarning("Hidrolik motor başlatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hidrolik motor başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopHydraulicMotorAsync()
    {
        try
        {
            _logger.LogInformation("Hidrolik motor durduruluyor...");
            
            var result = await _machineDriver.StopHydraulicMotorAsync();
            
            if (result)
            {
                _logger.LogInformation("Hidrolik motor başarıyla durduruldu");
            }
            else
            {
                _logger.LogWarning("Hidrolik motor durdurma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hidrolik motor durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StartFanMotorAsync()
    {
        try
        {
            _logger.LogInformation("Fan motor başlatılıyor...");
            
            var result = await _machineDriver.StartFanMotorAsync();
            
            if (result)
            {
                _logger.LogInformation("Fan motor başarıyla başlatıldı");
            }
            else
            {
                _logger.LogWarning("Fan motor başlatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fan motor başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopFanMotorAsync()
    {
        try
        {
            _logger.LogInformation("Fan motor durduruluyor...");
            
            var result = await _machineDriver.StopFanMotorAsync();
            
            if (result)
            {
                _logger.LogInformation("Fan motor başarıyla durduruldu");
            }
            else
            {
                _logger.LogWarning("Fan motor durdurma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fan motor durdurma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Valve Control

    public async Task<bool> OpenS1ValveAsync()
    {
        try
        {
            _logger.LogInformation("S1 valfi açılıyor...");
            
            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - S1 valfi açılamaz");
                return false;
            }
            
            var result = await _machineDriver.OpenS1ValveAsync();
            
            if (result)
            {
                _logger.LogInformation("S1 valfi başarıyla açıldı");
            }
            else
            {
                _logger.LogWarning("S1 valfi açma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S1 valfi açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> CloseS1ValveAsync()
    {
        try
        {
            _logger.LogInformation("S1 valfi kapatılıyor...");
            
            var result = await _machineDriver.CloseS1ValveAsync();
            
            if (result)
            {
                _logger.LogInformation("S1 valfi başarıyla kapatıldı");
            }
            else
            {
                _logger.LogWarning("S1 valfi kapatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S1 valfi kapatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> OpenS2ValveAsync()
    {
        try
        {
            _logger.LogInformation("S2 valfi açılıyor...");
            
            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - S2 valfi açılamaz");
                return false;
            }
            
            var result = await _machineDriver.OpenS2ValveAsync();
            
            if (result)
            {
                _logger.LogInformation("S2 valfi başarıyla açıldı");
            }
            else
            {
                _logger.LogWarning("S2 valfi açma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S2 valfi açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> CloseS2ValveAsync()
    {
        try
        {
            _logger.LogInformation("S2 valfi kapatılıyor...");
            
            var result = await _machineDriver.CloseS2ValveAsync();
            
            if (result)
            {
                _logger.LogInformation("S2 valfi başarıyla kapatıldı");
            }
            else
            {
                _logger.LogWarning("S2 valfi kapatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S2 valfi kapatma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Rotation Control

    public async Task<bool> StartRotationAsync(string direction, double speed)
    {
        try
        {
            _logger.LogInformation("Rotasyon başlatılıyor - Yön: {Direction}, Hız: {Speed}", direction, speed);
            
            var rotationDirection = direction.ToLower() switch
            {
                "clockwise" or "cw" or "forward" or "ileri" => BendingMachine.Domain.Enums.RotationDirection.Clockwise,
                "counterclockwise" or "ccw" or "backward" or "geri" => BendingMachine.Domain.Enums.RotationDirection.CounterClockwise,
                _ => BendingMachine.Domain.Enums.RotationDirection.Stopped
            };
            
            if (rotationDirection == BendingMachine.Domain.Enums.RotationDirection.Stopped)
            {
                _logger.LogWarning("Geçersiz rotasyon yönü: {Direction}", direction);
                return false;
            }
            
            var result = await _machineDriver.StartRotationAsync(rotationDirection, speed);
            
            if (result)
            {
                _logger.LogInformation("Rotasyon başarıyla başlatıldı");
            }
            else
            {
                _logger.LogWarning("Rotasyon başlatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopRotationAsync()
    {
        try
        {
            _logger.LogInformation("Rotasyon durduruluyor...");
            
            var result = await _machineDriver.StopRotationAsync();
            
            if (result)
            {
                _logger.LogInformation("Rotasyon başarıyla durduruldu");
            }
            else
            {
                _logger.LogWarning("Rotasyon durdurma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> SetRotationSpeedAsync(double speed)
    {
        try
        {
            _logger.LogInformation("Rotasyon hızı ayarlanıyor: {Speed}", speed);
            
            var result = await _machineDriver.SetRotationSpeedAsync(speed);
            
            if (result)
            {
                _logger.LogInformation("Rotasyon hızı başarıyla ayarlandı");
            }
            else
            {
                _logger.LogWarning("Rotasyon hızı ayarlama başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon hızı ayarlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Generic Valve Control

    public async Task<bool> OpenValveAsync(string valveType)
    {
        try
        {
            _logger.LogInformation("{ValveType} valfi açılıyor...", valveType);
            
            var result = valveType.ToUpper() switch
            {
                "S1" => await OpenS1ValveAsync(),
                "S2" => await OpenS2ValveAsync(),
                "P1" => await OpenPneumaticValve1Async(),
                "P2" => await OpenPneumaticValve2Async(),
                _ => false
            };
            
            if (result)
            {
                _logger.LogInformation("{ValveType} valfi başarıyla açıldı", valveType);
            }
            else
            {
                _logger.LogWarning("{ValveType} valfi açma başarısız", valveType);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ValveType} valfi açma sırasında hata oluştu", valveType);
            return false;
        }
    }

    public async Task<bool> CloseValveAsync(string valveType)
    {
        try
        {
            _logger.LogInformation("{ValveType} valfi kapatılıyor...", valveType);
            
            var result = valveType.ToUpper() switch
            {
                "S1" => await CloseS1ValveAsync(),
                "S2" => await CloseS2ValveAsync(),
                "P1" => await ClosePneumaticValve1Async(),
                "P2" => await ClosePneumaticValve2Async(),
                _ => false
            };
            
            if (result)
            {
                _logger.LogInformation("{ValveType} valfi başarıyla kapatıldı", valveType);
            }
            else
            {
                _logger.LogWarning("{ValveType} valfi kapatma başarısız", valveType);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ValveType} valfi kapatma sırasında hata oluştu", valveType);
            return false;
        }
    }

    #endregion

    #region Pneumatic Valve Control

    public async Task<bool> OpenPneumaticValve1Async()
    {
        try
        {
            _logger.LogInformation("Pnömatik valf 1 açılıyor...");
            
            var result = await _machineDriver.OpenPneumaticValve1Async();
            
            if (result)
            {
                _logger.LogInformation("Pnömatik valf 1 başarıyla açıldı");
            }
            else
            {
                _logger.LogWarning("Pnömatik valf 1 açma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pnömatik valf 1 açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ClosePneumaticValve1Async()
    {
        try
        {
            _logger.LogInformation("Pnömatik valf 1 kapatılıyor...");
            
            var result = await _machineDriver.ClosePneumaticValve1Async();
            
            if (result)
            {
                _logger.LogInformation("Pnömatik valf 1 başarıyla kapatıldı");
            }
            else
            {
                _logger.LogWarning("Pnömatik valf 1 kapatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pnömatik valf 1 kapatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> OpenPneumaticValve2Async()
    {
        try
        {
            _logger.LogInformation("Pnömatik valf 2 açılıyor...");
            
            var result = await _machineDriver.OpenPneumaticValve2Async();
            
            if (result)
            {
                _logger.LogInformation("Pnömatik valf 2 başarıyla açıldı");
            }
            else
            {
                _logger.LogWarning("Pnömatik valf 2 açma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pnömatik valf 2 açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ClosePneumaticValve2Async()
    {
        try
        {
            _logger.LogInformation("Pnömatik valf 2 kapatılıyor...");
            
            var result = await _machineDriver.ClosePneumaticValve2Async();
            
            if (result)
            {
                _logger.LogInformation("Pnömatik valf 2 başarıyla kapatıldı");
            }
            else
            {
                _logger.LogWarning("Pnömatik valf 2 kapatma başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pnömatik valf 2 kapatma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Side Support Piston Control

    public async Task<bool> JogSideSupportPistonAsync(string pistonType, string direction)
    {
        try
        {
            _logger.LogInformation("Yan dayama piston kontrolü - {PistonType}: {Direction}", pistonType, direction);
            
            // String piston type'ı enum'a çevir - Friendly mapping
            var pistonEnum = pistonType.ToLower() switch
            {
                "leftreel" => PistonType.LeftReelPiston,
                "leftbody" => PistonType.LeftBodyPiston, 
                "leftjoin" => PistonType.LeftJoinPiston,
                "rightreel" => PistonType.RightReelPiston,
                "rightbody" => PistonType.RightBodyPiston,
                "rightjoin" => PistonType.RightJoinPiston,
                // Fallback: Direct enum parsing için
                _ when Enum.TryParse<PistonType>(pistonType, true, out var parsed) => parsed,
                _ => (PistonType?)null
            };
            
            if (pistonEnum == null)
            {
                _logger.LogWarning("Geçersiz piston tipi: {PistonType}", pistonType);
                return false;
            }
            
            // String direction'ı enum'a çevir
            var motionDirection = direction.ToLower() switch
            {
                "forward" or "ileri" => MotionEnum.Forward,
                "backward" or "geri" => MotionEnum.Backward,
                _ => MotionEnum.Closed
            };
            
            if (motionDirection == MotionEnum.Closed)
            {
                _logger.LogWarning("Geçersiz yön: {Direction}", direction);
                return false;
            }
            
            // Driver'dan yan dayama özel metodunu çağır
            var result = await _machineDriver.JogSideSupportPistonAsync(pistonEnum.Value, motionDirection);
            
            if (result)
            {
                _logger.LogInformation("Yan dayama piston kontrolü başarılı - {PistonType}: {Direction}", pistonType, direction);
            }
            else
            {
                _logger.LogWarning("Yan dayama piston kontrolü başarısız - {PistonType}: {Direction}", pistonType, direction);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yan dayama piston kontrolü sırasında hata - {PistonType}: {Direction}", pistonType, direction);
            return false;
        }
    }

    public async Task<bool> StopSideSupportPistonAsync(string pistonType)
    {
        try
        {
            // Enum dönüşümü
            if (Enum.TryParse<PistonType>(pistonType, out var pistonEnum))
            {
                return await _machineDriver.StopSideSupportPistonAsync(pistonEnum);
            }
            
            _logger.LogWarning("Geçersiz piston tipi: {PistonType}", pistonType);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yan dayama piston durdurma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Event Handlers

    private void OnDriverStatusChanged(object? sender, BendingMachine.Domain.Interfaces.MachineStatusChangedEventArgs e)
    {
        try
        {
            var statusDto = _mapper.Map<MachineStatusDto>(e.Status);
            statusDto.IsConnected = _machineDriver.IsConnected;
            
            StatusChanged?.Invoke(this, statusDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status changed event işlenirken hata oluştu");
        }
    }

    private void OnDriverAlarmRaised(object? sender, BendingMachine.Domain.Interfaces.AlarmEventArgs e)
    {
        try
        {
            _logger.LogWarning("Alarm: {AlarmMessage} - Severity: {Severity}", e.AlarmMessage, e.Severity);
            AlarmRaised?.Invoke(this, e.AlarmMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alarm raised event işlenirken hata oluştu");
        }
    }

    private void OnDriverSafetyViolation(object? sender, BendingMachine.Domain.Interfaces.SafetyEventArgs e)
    {
        try
        {
            _logger.LogCritical("GÜVENLİK İHLALİ: {ViolationType} - {Description}", e.ViolationType, e.Description);
            SafetyViolation?.Invoke(this, $"{e.ViolationType}: {e.Description}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Safety violation event işlenirken hata oluştu");
        }
    }

    #endregion

    // Bending Operations
    public async Task<bool> CompressPartAsync(double targetPressure, double tolerance)
    {
        try
        {
            _logger.LogInformation("Parça sıkıştırma işlemi başlatılıyor - Hedef basınç: {Pressure} bar", targetPressure);
            return await _machineDriver.CompressPartAsync(targetPressure, tolerance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça sıkıştırma sırasında hata oluştu");
            return false;
        }
    }
    
    public async Task<bool> ResetPartPositionAsync(double resetDistance)
    {
        try
        {
            _logger.LogInformation($"Parça sıfırlama başlatılıyor - Sıfırlama mesafesi: {resetDistance} mm");

            // Güvenlik kontrolü
            var isSafe = await CheckSafetyAsync();
            if (!isSafe)
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - parça sıfırlama iptal edildi");
                return false;
            }

            // S2 valfini aç (geri çekme için)
            var valveOpened = await OpenS2ValveAsync();
            if (!valveOpened)
            {
                _logger.LogError("S2 valfi açılamadı");
                return false;
            }

            // Ana pistonları geri çek (Driver'dan direkt çağır)
            var pistonResetTasks = new[]
            {
                _machineDriver.JogPistonAsync(PistonType.TopPiston, MotionEnum.Backward, 5.0),
                _machineDriver.JogPistonAsync(PistonType.BottomPiston, MotionEnum.Backward, 5.0),
                _machineDriver.JogPistonAsync(PistonType.LeftPiston, MotionEnum.Backward, 5.0),
                _machineDriver.JogPistonAsync(PistonType.RightPiston, MotionEnum.Backward, 5.0)
            };

            var results = await Task.WhenAll(pistonResetTasks);
            var allSuccess = results.All(r => r);

            // resetDistance'a göre uygun süre bekle
            var moveTime = (int)(resetDistance / 10 * 1000); // 10mm/sn hızla hareket varsayımı
            await Task.Delay(Math.Min(moveTime, 5000)); // Max 5 saniye

            // Pistonları durdur
            var stopTasks = new[]
            {
                _machineDriver.StopPistonAsync(PistonType.TopPiston),
                _machineDriver.StopPistonAsync(PistonType.BottomPiston),
                _machineDriver.StopPistonAsync(PistonType.LeftPiston),
                _machineDriver.StopPistonAsync(PistonType.RightPiston)
            };

            await Task.WhenAll(stopTasks);

            if (allSuccess)
            {
                _logger.LogInformation("Parça sıfırlama tamamlandı");
                
                // S2 valfini kapat
                await CloseS2ValveAsync();
                return true;
            }
            else
            {
                _logger.LogError("Bazı pistonlar sıfırlanamadı");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça pozisyon sıfırlama sırasında hata oluştu");
            return false;
        }
    }
    
    public async Task<bool> ResetRulersAsync()
    {
        try
        {
            _logger.LogInformation("Cetvel sıfırlama işlemi başlatılıyor");
            return await _machineDriver.ResetRulersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetvel sıfırlama sırasında hata oluştu");
            return false;
        }
    }

    public async Task<RulerStatus> GetRulerStatusAsync()
    {
        try
        {
            _logger.LogInformation("Cetvel durumları okunuyor");
            
            // MachineDriver'dan gerçek modbus okuma
            var status = await _machineDriver.GetRulerStatusAsync();
            
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetvel durumları okunurken hata oluştu");
            // Hata durumunda varsayılan değerler döndür
            return new RulerStatus
            {
                RulerResetM13toM16 = 0,
                RulerResetM17toM20 = 0,
                RulerResetPneumaticValve = 0,
                RulerResetRotation = 0,
                AllReset = false,
                LastChecked = DateTime.UtcNow
            };
        }
    }
    
    public async Task<bool> SetStageAsync(int stageValue)
    {
        try
        {
            _logger.LogInformation("Stage ayarlanıyor - Değer: {Stage} mm", stageValue);
            return await _machineDriver.SetStageAsync(stageValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage ayarlama sırasında hata oluştu");
            return false;
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut stage'leri listeler
    /// </summary>
    public async Task<List<StageConfigDto>> GetAvailableStagesAsync()
    {
        try
        {
            var config = await GetMachineConfigurationAsync();
            var stages = new List<StageConfigDto>();
            
            foreach (var stage in config.Stages.Stages)
            {
                stages.Add(new StageConfigDto
                {
                    Name = stage.Name,
                    Value = stage.Value,
                    LeftPistonOffset = stage.LeftPistonOffset,
                    RightPistonOffset = stage.RightPistonOffset,
                    IsActive = false, // TODO: Mevcut aktif stage'i belirle
                    Description = $"Alt pistonlar: {stage.Value}mm, Yan pistonlar: {stage.LeftPistonOffset:F2}mm"
                });
            }
            
            _logger.LogInformation("{Count} stage konfigürasyonu okundu", stages.Count);
            return stages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage listesi okunurken hata oluştu");
            return new List<StageConfigDto>();
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut aktif stage'i döndürür
    /// </summary>
    public async Task<int> GetCurrentStageAsync()
    {
        try
        {
            // TODO: Gerçek aktif stage'i machine driver'dan oku
            // Şu an için default 0 döndürüyoruz
            await Task.CompletedTask;
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mevcut stage okunurken hata oluştu");
            return 0;
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Belirli bir stage konfigürasyonunu döndürür
    /// </summary>
    public async Task<StageConfigDto?> GetStageConfigAsync(int stageValue)
    {
        try
        {
            var stages = await GetAvailableStagesAsync();
            return stages.FirstOrDefault(s => Math.Abs(s.Value - stageValue) < 0.1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage konfigürasyonu okunurken hata oluştu - Stage: {Stage}", stageValue);
            return null;
        }
    }
    
    public async Task<bool> ExecuteAutoBendingAsync(Domain.Entities.DomainBendingParameters parameters)
    {
        try
        {
            _logger.LogInformation("Otomatik büküm başlatılıyor...");
            
            var result = await _machineDriver.ExecuteAutoBendingAsync(parameters);
            
            if (result)
            {
                _logger.LogInformation("Otomatik büküm başarıyla tamamlandı");
            }
            else
            {
                _logger.LogWarning("Otomatik büküm başarısız");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Otomatik büküm sırasında hata oluştu");
            return false;
        }
    }

    #region ✅ YENİ ÖZELLİKLER - Hassas Konumlandırma ve Gerçek Sensör Okuma

    /// <summary>
    /// Gerçek S1/S2 basınç sensörlerinden anlık basınç okur (4-20mA → 0-400 bar)
    /// </summary>
    public async Task<(double s1Pressure, double s2Pressure)> ReadActualPressureAsync()
    {
        try
        {
            _logger.LogDebug("Gerçek basınç sensörlerinden okuma yapılıyor...");
            
            // ✅ DÜZELTME: Driver'dan basınç değerleri ZATEN bar cinsinden geliyor!
            // MachineDriver'da RegisterToBarAndMilliamps ile dönüştürülmüş durumda
            var machineStatus = await _machineDriver.GetMachineStatusAsync();
            
            // S1 ve S2 basınç değerleri zaten bar cinsinden
            var s1Pressure = machineStatus.S1OilPressure; // Zaten bar
            var s2Pressure = machineStatus.S2OilPressure; // Zaten bar
            
            _logger.LogDebug("Basınç değerleri okundu - S1: {S1:F2}bar, S2: {S2:F2}bar", s1Pressure, s2Pressure);
            
            return (s1Pressure, s2Pressure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gerçek basınç değerleri okunurken hata oluştu");
            return (0.0, 0.0);
        }
    }

    /// <summary>
    /// RV3100 encoder durumu ve pozisyon bilgilerini getirir
    /// </summary>
    public async Task<EncoderStatusDto> GetEncoderStatusAsync()
    {
        try
        {
            _logger.LogDebug("Encoder durumu okunuyor...");
            
            var machineStatus = await _machineDriver.GetMachineStatusAsync();
            
            var encoderStatus = new EncoderStatusDto
            {
                CurrentPosition = machineStatus.RotationEncoderRaw,
                EncoderType = "RV3100",
                PulsesPerRevolution = 1024,
                CurrentDistance = Math.Round((machineStatus.RotationEncoderRaw * Math.PI * 220.0) / 1024.0, 2),
                IsHealthy = true, // TODO: Encoder sağlık kontrolü implementasyonu
                LastUpdateTime = DateTime.UtcNow
            };
            
            _logger.LogDebug("Encoder durumu okundu - Pozisyon: {Position}, Mesafe: {Distance}mm", 
                encoderStatus.CurrentPosition, encoderStatus.CurrentDistance);
            
            return encoderStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encoder durumu okunurken hata oluştu");
            return new EncoderStatusDto { IsHealthy = false };
        }
    }

    /// <summary>
    /// Hassas rotasyon kontrolü (3 aşamalı hız kontrolü ile)
    /// </summary>
    public async Task<bool> StartPreciseRotationAsync(string direction, double speed)
    {
        try
        {
            _logger.LogInformation("Hassas rotasyon başlatılıyor - Yön: {Direction}, Hız: {Speed}%", direction, speed);
            
            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - hassas rotasyon başlatılamaz");
                return false;
            }
            
            // Direction string'ini enum'a çevir
            if (!Enum.TryParse<RotationDirection>(direction, true, out var rotationDirection))
            {
                _logger.LogError("Geçersiz rotasyon yönü: {Direction}", direction);
                return false;
            }
            
            var result = await _machineDriver.StartRotationAsync(rotationDirection, speed);
            
            if (result)
            {
                _logger.LogInformation("Hassas rotasyon başarıyla başlatıldı - Yön: {Direction}, Hız: {Speed}%", direction, speed);
            }
            else
            {
                _logger.LogWarning("Hassas rotasyon başlatılamadı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hassas rotasyon başlatılırken hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// Hassas konumlandırma konfigürasyonunu getirir
    /// </summary>
    public async Task<PrecisionControlConfigDto> GetPrecisionControlConfigAsync()
    {
        try
        {
            _logger.LogDebug("Hassas konumlandırma konfigürasyonu okunuyor...");
            
            var config = new PrecisionControlConfigDto
            {
                FastSpeed = 70.0,    // %70 - İlk %80 mesafe
                MediumSpeed = 40.0,  // %40 - %80-95 mesafe  
                SlowSpeed = 15.0,    // %15 - Son %5 mesafe
                PreciseSpeed = 20.0, // %20 - Hassas konumlandırma
                BallDiameter = 220.0, // mm
                EncoderPulsesPerRevolution = 1024,
                EncoderFreezeTimeoutSeconds = 2.0,
                MaxEncoderStuckCount = 3
            };
            
            _logger.LogDebug("Hassas konumlandırma konfigürasyonu okundu");
            
            await Task.CompletedTask; // Async metod uyarısını gidermek için
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hassas konumlandırma konfigürasyonu okunurken hata oluştu");
            return new PrecisionControlConfigDto();
        }
    }

    #endregion

    #region Legacy Configuration Methods (To be removed)
    
    public async Task LoadConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Makine konfigürasyonu yükleniyor...");
            await _machineDriver.LoadConfigurationAsync();
            _logger.LogInformation("Makine konfigürasyonu başarıyla yüklendi");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine konfigürasyonu yüklenirken hata oluştu");
            throw;
        }
    }

    public async Task SaveConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Makine konfigürasyonu kaydediliyor...");
            await _machineDriver.SaveConfigurationAsync();
            _logger.LogInformation("Makine konfigürasyonu başarıyla kaydedildi");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine konfigürasyonu kaydedilirken hata oluştu");
            throw;
        }
    }

    public async Task<object> GetConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Makine konfigürasyonu alınıyor...");
            
            var config = new
            {
                MachineType = "CNC Profile Bending Machine",
                Version = "1.0.0",
                ModbusConfig = new
                {
                    IpAddress = "192.168.1.100",
                    Port = 502,
                    SlaveId = 1,
                    TimeoutMs = 1000
                },
                SafetyLimits = new
                {
                    MaxPressure = 250, // bar
                    MinPressure = 10,  // bar
                    MaxVoltage = 10,   // V
                    MinVoltage = -10   // V
                },
                UpdateInterval = 100 // ms
            };
            
            await Task.CompletedTask; // Async metod uyarısını gidermek için
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine konfigürasyonu alınırken hata oluştu");
            throw;
        }
    }

    public async Task<bool> UpdateConfigurationAsync(object configuration)
    {
        try
        {
            _logger.LogInformation("Makine konfigürasyonu güncelleniyor...");
            await Task.CompletedTask; // Async metod uyarısını gidermek için
            _logger.LogInformation("Makine konfigürasyonu başarıyla güncellendi");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine konfigürasyonu güncellenirken hata oluştu");
            return false;
        }
    }

    #endregion

    #region Additional Controller Methods
    
    public async Task<BendingParametersDto> CalculateBendingParametersAsync(BendingCalculationRequestDto request)
    {
        try
        {
            _logger?.LogInformation("Büküm parametreleri hesaplanıyor...");
            
            // TODO: Gerçek hesaplama algoritması
            var parameters = new BendingParametersDto
            {
                BendingAngle = request.BendingAngle,
                ProfileHeight = request.ProfileHeight,
                BendingRadius = request.BendingRadius,
                StepCount = Math.Max(1, (int)(request.BendingAngle / 15)), // Her 15 derecede bir paso
                StageValue = request.ProfileHeight > 60 ? 120 : 60
            };
            
            _logger?.LogInformation("Büküm parametreleri hesaplandı");
            return parameters;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Büküm parametreleri hesaplanırken hata oluştu");
            throw;
        }
    }

    public async Task<bool> ValidateBendingParametersAsync(BendingParametersDto parameters)
    {
        try
        {
            _logger.LogInformation("Büküm parametreleri doğrulanıyor...");
            
            // Basic validation
            var isValid = parameters.BendingAngle > 0 && parameters.BendingAngle <= 180 &&
                         parameters.ProfileHeight > 0 && parameters.ProfileHeight <= 200 &&
                         parameters.BendingRadius > 0 && parameters.BendingRadius <= 2000;
            
            _logger.LogInformation("Büküm parametreleri doğrulama tamamlandı: {IsValid}", isValid);
            return await Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm parametreleri doğrulanırken hata oluştu");
            return false;
        }
    }

    public async Task<bool> StartBendingAsync(BendingParametersDto parameters)
    {
        try
        {
            _logger?.LogInformation("Büküm işlemi başlatılıyor...");
            
            // Convert DTO to Domain entity
            var domainParameters = new Domain.Entities.DomainBendingParameters
            {
                BendingRadius = parameters.BendingRadius,
                ProfileHeight = parameters.ProfileHeight,
                TriangleWidth = parameters.TriangleWidth,
                TriangleAngle = parameters.TriangleAngle,
                StepCount = parameters.StepCount,
                StageValue = parameters.StageValue,
                TargetPressure = parameters.TargetPressure,
                PressureTolerance = parameters.PressureTolerance,
                ResetDistance = parameters.ResetDistance
            };
            
            var result = await ExecuteAutoBendingAsync(domainParameters);
            
            if (result)
            {
                _logger?.LogInformation("Büküm işlemi başarıyla başlatıldı");
            }
            else
            {
                _logger?.LogWarning("Büküm işlemi başlatılamadı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Büküm işlemi başlatılırken hata oluştu");
            return false;
        }
    }

    #endregion
} 