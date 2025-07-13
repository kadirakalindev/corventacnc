namespace BendingMachine.Domain.Configuration;

public class MachineConfiguration
{
    public ModbusSettings Modbus { get; set; } = new();
    public PistonSettings Pistons { get; set; } = new();
    public StageSettings Stages { get; set; } = new();
    public BallSettings Balls { get; set; } = new();
    public GeometrySettings Geometry { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public OilSystemSettings OilSystem { get; set; } = new();
}

public class ModbusSettings
{
    public string IpAddress { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 502;
    public byte SlaveId { get; set; } = 1;
    public int TimeoutMs { get; set; } = 3000;
    public int RetryCount { get; set; } = 3;
    public int UpdateIntervalMs { get; set; } = 100;
}

public class PistonSettings
{
    public PistonConfig TopPiston { get; set; } = new() 
    { 
        Name = "Top Piston", 
        StrokeLength = 160, 
        RegisterCount = 32767 
    };
    
    public PistonConfig BottomPiston { get; set; } = new() 
    { 
        Name = "Bottom Piston", 
        StrokeLength = 195, 
        RegisterCount = 32767 
    };
    
    public PistonConfig LeftPiston { get; set; } = new() 
    { 
        Name = "Left Piston", 
        StrokeLength = 422, 
        RegisterCount = 32767 
    };
    
    public PistonConfig RightPiston { get; set; } = new() 
    { 
        Name = "Right Piston", 
        StrokeLength = 422, 
        RegisterCount = 32767 
    };
    
    // Side Support Pistons
    public PistonConfig LeftReelPiston { get; set; } = new() 
    { 
        Name = "Left Reel Piston", 
        StrokeLength = 352, 
        RegisterCount = 4095 
    };
    
    public PistonConfig RightReelPiston { get; set; } = new() 
    { 
        Name = "Right Reel Piston", 
        StrokeLength = 352, 
        RegisterCount = 4095 
    };
    
    public PistonConfig LeftBodyPiston { get; set; } = new() 
    { 
        Name = "Left Body Piston", 
        StrokeLength = 129, 
        RegisterCount = 4095 
    };
    
    public PistonConfig RightBodyPiston { get; set; } = new() 
    { 
        Name = "Right Body Piston", 
        StrokeLength = 129, 
        RegisterCount = 4095 
    };
    
    public PistonConfig LeftJoinPiston { get; set; } = new() 
    { 
        Name = "Left Join Piston", 
        StrokeLength = 187, 
        RegisterCount = 4095 
    };
    
    public PistonConfig RightJoinPiston { get; set; } = new() 
    { 
        Name = "Right Join Piston", 
        StrokeLength = 187, 
        RegisterCount = 4095 
    };
}

public class PistonConfig
{
    public string Name { get; set; } = string.Empty;
    public double StrokeLength { get; set; }
    public int RegisterCount { get; set; }
    public double PositionTolerance { get; set; } = 1.0;
    public double MaxSpeed { get; set; } = 10.0; // V
    public double DefaultSpeed { get; set; } = 5.0; // V
}

public class StageSettings
{
    public List<StageConfig> Stages { get; set; } = new()
    {
        new() { Name = "Stage 0", Value = 0 },
        new() { Name = "Stage 60", Value = 60 },
        new() { Name = "Stage 120", Value = 120 }
    };
}

public class StageConfig
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public double LeftPistonOffset { get; set; } = 67.34; // For 60mm: 67.34, 120mm: 134.68
    public double RightPistonOffset { get; set; } = 67.34;
}

public class BallSettings
{
    public double TopBallDiameter { get; set; } = 220; // mm
    public double BottomBallDiameter { get; set; } = 220; // mm
    public double LeftBallDiameter { get; set; } = 220; // mm
    public double RightBallDiameter { get; set; } = 220; // mm
    public double TopBallReferenceMaxHeight { get; set; } = 473; // mm
}

public class GeometrySettings
{
    public double TriangleWidth { get; set; } = 493; // mm
    public double TriangleAngle { get; set; } = 27; // degrees
    public double DefaultProfileHeight { get; set; } = 80; // mm
    public double DefaultBendingRadius { get; set; } = 500; // mm
    public double StepSize { get; set; } = 20; // mm
}

public class SafetySettings
{
    public double MaxPressure { get; set; } = 250; // bar
    public double DefaultTargetPressure { get; set; } = 50; // bar
    public double PressureTolerance { get; set; } = 5; // ±bar
    public double WorkingOilTemperature { get; set; } = 40; // °C
    public double MaxOilTemperature { get; set; } = 80; // °C
    public double MinOilLevel { get; set; } = 20; // %
    public double FanOnTemperature { get; set; } = 50; // °C
    public double FanOffTemperature { get; set; } = 40; // °C
}

public class OilSystemSettings
{
    public double S1MaxPressure { get; set; } = 250; // bar
    public double S2MaxPressure { get; set; } = 250; // bar
    public double MaxFlowRate { get; set; } = 297; // cm³/s
    public double MinFlowRate { get; set; } = 0; // cm³/s
} 