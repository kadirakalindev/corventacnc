namespace BendingMachine.Application.DTOs;

public class MachineStatusDto
{
    public bool IsConnected { get; set; }
    public DateTime LastUpdateTime { get; set; }
    
    // Safety Status
    public bool EmergencyStop { get; set; }
    public bool HydraulicThermalError { get; set; }
    public bool FanThermalError { get; set; }
    public bool PhaseSequenceError { get; set; }
    public bool AlarmActive { get; set; }
    
    // Motor Status
    public bool HydraulicMotorRunning { get; set; }
    public bool FanMotorRunning { get; set; }
    public bool RotationMotorRunning { get; set; }
    
    // Valve Status
    public bool S1ValveOpen { get; set; }
    public bool S2ValveOpen { get; set; }
    
    // Controller için valve status
    public bool LeftValveGroupOpen { get; set; }
    public bool RightValveGroupOpen { get; set; }
    public bool TopValveGroupOpen { get; set; }
    public bool BottomValveGroupOpen { get; set; }
    public bool PneumaticValve1Open { get; set; }
    public bool PneumaticValve2Open { get; set; }
    
    // Sensor Status
    public bool LeftPartPresent { get; set; }
    public bool RightPartPresent { get; set; }
    
    // Pollution Sensors
    public bool PollutionSensor1 { get; set; }
    public bool PollutionSensor2 { get; set; }
    public bool PollutionSensor3 { get; set; }
    
    // Oil System
    public double S1OilPressure { get; set; } // bar
    public double S2OilPressure { get; set; } // bar
    public double OilTemperature { get; set; } // °C
    
    // ✅ YENİ - Yağ Akış Hızları
    public double S1OilFlowRate { get; set; } // cm/sn
    public double S2OilFlowRate { get; set; } // cm/sn
    
    // ✅ YENİ - Yağ Kalite Verileri
    public double OilHumidity { get; set; } // %
    public double OilLevel { get; set; } // %
    
    // ✅ YENİ - Encoder Information
    public int RotationEncoderRaw { get; set; } // RV3100 encoder raw pulse value
    
    // Pistons
    public List<PistonStatusDto> Pistons { get; set; } = new();
    
    // Utility methods
    public bool IsSafeToOperate()
    {
        return !EmergencyStop && 
               !HydraulicThermalError && 
               !FanThermalError && 
               !PhaseSequenceError &&
               HydraulicMotorRunning;
    }
    
    public bool IsOilSystemHealthy()
    {
        return OilTemperature > 0 && 
               OilTemperature < 80 && 
               S1OilPressure > 0 && 
               S2OilPressure > 0 &&
               OilLevel > 20 &&        // Tank en az %20 dolu olmalı
               OilHumidity < 30 &&     // Nem %30'dan az olmalı
               S1OilFlowRate > 0 &&    // Akış hızı pozitif olmalı
               S2OilFlowRate > 0;      // Akış hızı pozitif olmalı
    }
}

public class RulerStatusDto
{
    public int RulerResetM13toM16 { get; set; }
    public int RulerResetM17toM20 { get; set; }
    public int RulerResetPneumaticValve { get; set; }
    public int RulerResetRotation { get; set; }
    public bool AllReset { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
} 