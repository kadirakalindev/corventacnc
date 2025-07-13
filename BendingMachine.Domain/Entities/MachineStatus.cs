using BendingMachine.Domain.Common;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Domain.Entities;

public class MachineStatus : BaseEntity
{
    // Connection Status
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Disconnected;
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
    
    // Safety Status
    public bool EmergencyStop { get; set; }
    public bool HydraulicThermalError { get; set; }
    public bool FanThermalError { get; set; }
    public bool PhaseSequenceError { get; set; }
    
    // Motor Status
    public bool HydraulicMotorRunning { get; set; }
    public bool FanMotorRunning { get; set; }
    public bool AlarmActive { get; set; }
    
    // Valve Status
    public bool S1ValveOpen { get; set; }
    public bool S2ValveOpen { get; set; }
    
    // Rotation Status
    public RotationDirection RotationDirection { get; set; } = RotationDirection.Stopped;
    public double RotationSpeed { get; set; } // Percentage 0-100
    public double RotationPosition { get; set; } // Encoder position (degrees 0-360)
    public short RotationEncoderRaw { get; set; } // Raw encoder value (0-1023)
    public bool LeftRotationSensor { get; set; }
    public bool RightRotationSensor { get; set; }
    
    // Part Presence
    public bool LeftPartPresent { get; set; }
    public bool RightPartPresent { get; set; }
    
    // Pollution Sensors
    public bool PollutionSensor1 { get; set; }
    public bool PollutionSensor2 { get; set; }
    public bool PollutionSensor3 { get; set; }
    
    // Oil System
    public double S1OilPressure { get; set; } // Bar
    public double S2OilPressure { get; set; } // Bar
    public double S1OilFlowRate { get; set; } // cm³/s
    public double S2OilFlowRate { get; set; } // cm³/s
    public double OilTemperature { get; set; } // °C
    public double OilHumidity { get; set; } // %
    public double OilLevel { get; set; } // %
    
    // Piston Positions
    public double TopPistonPosition { get; set; } // mm
    public double BottomPistonPosition { get; set; } // mm
    public double LeftPistonPosition { get; set; } // mm
    public double RightPistonPosition { get; set; } // mm
    public double LeftReelPistonPosition { get; set; } // mm
    public double RightReelPistonPosition { get; set; } // mm
    public double LeftBodyPistonPosition { get; set; } // mm
    public double RightBodyPistonPosition { get; set; } // mm
    public double LeftJoinPistonPosition { get; set; } // mm
    public double RightJoinPistonPosition { get; set; } // mm
    
    // Pneumatic Valves
    public bool P1Open { get; set; }
    public bool P2Open { get; set; }
    
    // Current Stage
    public int CurrentStage { get; set; } = 0; // 0mm, 60mm, 120mm etc.
    
    // Pistons Collection
    public List<Piston> Pistons { get; set; } = new();
    
    // Working Mode
    public string WorkingMode { get; set; } = "Manual"; // Manual, Auto, Maintenance
    
    // Performance
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    
    // Safety Check
    public bool IsSafeToOperate()
    {
        return !EmergencyStop && 
               !HydraulicThermalError && 
               !FanThermalError && 
               !PhaseSequenceError;
        // HydraulicMotorRunning kontrolü kaldırıldı - motor başlatmak için güvenlik gerekiyor!
    }
    
    public bool IsOilSystemHealthy()
    {
        return OilTemperature > 0 && 
               OilTemperature < 80 && 
               S1OilPressure > 0 && 
               S2OilPressure > 0;
    }
}

/// <summary>
/// Cetvel reset durumları
/// </summary>
public class RulerStatus
{
    public int RulerResetM13toM16 { get; set; }
    public int RulerResetM17toM20 { get; set; }
    public int RulerResetPneumaticValve { get; set; }
    public int RulerResetRotation { get; set; }
    public bool AllReset { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stage konfigürasyonu entity
/// </summary>
public class StageConfigDto
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public double LeftPistonOffset { get; set; }
    public double RightPistonOffset { get; set; }
    public bool IsActive { get; set; } // Mevcut aktif stage mi?
    public string Description { get; set; } = string.Empty;
} 