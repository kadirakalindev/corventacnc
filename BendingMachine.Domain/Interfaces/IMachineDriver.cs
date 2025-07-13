using BendingMachine.Domain.Entities;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Domain.Interfaces;

public interface IMachineDriver
{
    // Connection Management
    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    bool IsConnected { get; }
    
    // Machine Status
    Task<MachineStatus> GetMachineStatusAsync();
    Task<List<Piston>> GetAllPistonsAsync();
    Task<Piston> GetPistonAsync(PistonType pistonType);
    
    // Piston Control
    Task<bool> MovePistonAsync(PistonType pistonType, double voltage);
    Task<bool> MovePistonToPositionAsync(PistonType pistonType, double targetPosition);
    Task<bool> StopPistonAsync(PistonType pistonType);
    Task<bool> JogPistonAsync(PistonType pistonType, MotionEnum direction, double voltage);
    
    // Side Support Piston Control (Yan Dayama Pistonları)
    Task<bool> JogSideSupportPistonAsync(PistonType pistonType, MotionEnum direction);
    Task<bool> StopSideSupportPistonAsync(PistonType pistonType);
    
    // Stage Operations
    Task<bool> SetStageAsync(int stageValue);
    Task<bool> ResetRulersAsync();
    Task<RulerStatus> GetRulerStatusAsync();
    Task<bool> ResetSpecificRulerAsync(PistonType pistonType);
    
    // Safety & Emergency
    Task<bool> EmergencyStopAsync();
    Task<bool> ResetAlarmAsync();
    Task<bool> CheckSafetyAsync();
    
    // Motor Control
    Task<bool> StartHydraulicMotorAsync();
    Task<bool> StopHydraulicMotorAsync();
    Task<bool> StartFanMotorAsync();
    Task<bool> StopFanMotorAsync();
    
    // Rotation Control
    Task<bool> StartRotationAsync(RotationDirection direction, double speed);
    Task<bool> StopRotationAsync();
    Task<bool> SetRotationSpeedAsync(double speed);
    
    // Valve Control
    Task<bool> OpenS1ValveAsync();
    Task<bool> CloseS1ValveAsync();
    Task<bool> OpenS2ValveAsync();
    Task<bool> CloseS2ValveAsync();
    
    // Pneumatic Valve Control
    Task<bool> OpenPneumaticValve1Async();
    Task<bool> ClosePneumaticValve1Async();
    Task<bool> OpenPneumaticValve2Async();
    Task<bool> ClosePneumaticValve2Async();
    
    // Generic Piston Movement
    Task<bool> MovePistonAsync(string pistonType, double position);
    
    // Bending Operations
    Task<bool> CompressPartAsync(double targetPressure, double tolerance);
    Task<bool> ResetPartPositionAsync(double resetDistance);
    Task<bool> ExecuteAutoBendingAsync(DomainBendingParameters parameters);
    
    /// <summary>
    /// PASO TEST: Sadece paso işlemini test eder - hazırlık adımları yapılmaz
    /// </summary>
    Task<bool> ExecutePasoTestAsync(double sideBallTravelDistance, double profileLength, double stepSize = 20.0, int evacuationTimeSeconds = 10);
    
    // Events
    event EventHandler<MachineStatusChangedEventArgs> StatusChanged;
    event EventHandler<PistonMovedEventArgs> PistonMoved;
    event EventHandler<AlarmEventArgs> AlarmRaised;
    event EventHandler<SafetyEventArgs> SafetyViolation;
    
    // Configuration
    Task LoadConfigurationAsync();
    Task SaveConfigurationAsync();
    
    // Utility
    Task<bool> StopAllPistonsAsync();
    
    // Stage Management
    Task<int> GetCurrentStageAsync();
    Task<List<StageConfigDto>> GetAvailableStagesAsync();
    Task<StageConfigDto?> GetStageConfigAsync(int stageValue);
}

public class MachineStatusChangedEventArgs : EventArgs
{
    public MachineStatus Status { get; set; } = new();
}

public class PistonMovedEventArgs : EventArgs
{
    public PistonType PistonType { get; set; }
    public double CurrentPosition { get; set; }
    public double TargetPosition { get; set; }
    public MotionEnum Motion { get; set; }
}

public class AlarmEventArgs : EventArgs
{
    public string AlarmMessage { get; set; } = string.Empty;
    public SafetyStatus Severity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SafetyEventArgs : EventArgs
{
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresEmergencyStop { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
} 