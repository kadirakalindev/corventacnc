using BendingMachine.Application.DTOs;
using BendingMachine.Domain.Configuration;
using BendingMachine.Domain.Entities;

namespace BendingMachine.Application.Interfaces;

/// <summary>
/// Machine operations için business logic interface
/// </summary>
public interface IMachineService
{
    // Events
    event EventHandler<MachineStatusDto>? StatusChanged;
    event EventHandler<string>? AlarmRaised;
    event EventHandler<string>? SafetyViolation;
    
    // Connection Management
    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    Task<bool> IsConnectedAsync();
    
    // Status Management
    Task<MachineStatusDto> GetMachineStatusAsync();
    Task<bool> CheckSafetyAsync();
    Task<bool> EmergencyStopAsync();
    Task<bool> ResetAlarmAsync();
    
    // Configuration Management
    Task<MachineConfiguration> GetMachineConfigurationAsync();
    Task<bool> UpdateMachineConfigurationAsync(MachineConfiguration configuration);
    Task<bool> SaveConfigurationToFileAsync();
    Task<bool> LoadConfigurationFromFileAsync();
    
    // Legacy methods (will be removed)
    Task LoadConfigurationAsync();
    Task SaveConfigurationAsync();
    Task<object> GetConfigurationAsync();
    Task<bool> UpdateConfigurationAsync(object configuration);
    
    // Additional Methods for Controllers  
    Task<BendingParametersDto> CalculateBendingParametersAsync(BendingCalculationRequestDto request);
    Task<bool> ValidateBendingParametersAsync(BendingParametersDto parameters);
    Task<bool> StartBendingAsync(BendingParametersDto parameters);
    
    // Motor Control
    Task<bool> StartHydraulicMotorAsync();
    Task<bool> StopHydraulicMotorAsync();
    Task<bool> StartFanMotorAsync();
    Task<bool> StopFanMotorAsync();
    
    // Rotation Control
    Task<bool> StartRotationAsync(string direction, double speed);
    Task<bool> StopRotationAsync();
    Task<bool> SetRotationSpeedAsync(double speed);
    
    // Valve Control
    Task<bool> OpenS1ValveAsync();
    Task<bool> CloseS1ValveAsync();
    Task<bool> OpenS2ValveAsync();
    Task<bool> CloseS2ValveAsync();
    
    // Generic Valve Control
    Task<bool> OpenValveAsync(string valveType);
    Task<bool> CloseValveAsync(string valveType);
    
    // Pneumatic Valve Control
    Task<bool> OpenPneumaticValve1Async();
    Task<bool> ClosePneumaticValve1Async();
    Task<bool> OpenPneumaticValve2Async();
    Task<bool> ClosePneumaticValve2Async();
    
    // Side Support Piston Control (Yan Dayama Pistonları)
    Task<bool> JogSideSupportPistonAsync(string pistonType, string direction);
    Task<bool> StopSideSupportPistonAsync(string pistonType);
    
    // Bending Operations
    Task<bool> CompressPartAsync(double targetPressure, double tolerance);
    Task<bool> ResetPartPositionAsync(double resetDistance);
    Task<bool> ResetRulersAsync();
    Task<RulerStatus> GetRulerStatusAsync();
    Task<bool> SetStageAsync(int stageValue);
    Task<bool> ExecuteAutoBendingAsync(Domain.Entities.DomainBendingParameters parameters);
    
    // ✅ STAGE YÖNETİMİ - Yeni metodlar
    Task<List<StageConfigDto>> GetAvailableStagesAsync();
    Task<int> GetCurrentStageAsync();
    Task<StageConfigDto?> GetStageConfigAsync(int stageValue);
    
    // ✅ YENİ ÖZELLİKLER - Hassas Konumlandırma ve Gerçek Sensör Okuma
    Task<(double s1Pressure, double s2Pressure)> ReadActualPressureAsync();
    Task<EncoderStatusDto> GetEncoderStatusAsync();
    Task<bool> StartPreciseRotationAsync(string direction, double speed);
    Task<PrecisionControlConfigDto> GetPrecisionControlConfigAsync();
} 