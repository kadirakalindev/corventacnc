using BendingMachine.Domain.Enums;

namespace BendingMachine.Application.DTOs;

public class PistonControlDto
{
    public string PistonType { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double TargetPosition { get; set; }
    public double Speed { get; set; } = 5.0;
}

public class PistonStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ValveGroup { get; set; } = string.Empty;
    public double CurrentPosition { get; set; } // mm
    public double TargetPosition { get; set; } // mm
    public double CurrentVoltage { get; set; } // V
    public double Speed { get; set; } // mm/s
    public string Motion { get; set; } = string.Empty;
    public bool IsMoving { get; set; }
    public bool IsAtTarget { get; set; }
    
    // Physical Properties
    public double StrokeLength { get; set; } // mm
    public double MinPosition { get; set; } // mm
    public double MaxPosition { get; set; } // mm
    public double PositionTolerance { get; set; } // mm
    
    // Ruler Data
    public double RulerValue { get; set; }
    public int RegisterCount { get; set; }
    
    // Control Type
    public bool IsVoltageControlled { get; set; }
    public bool IsDigitalControlled { get; set; }
}

public class PistonMoveRequestDto
{
    public string PistonType { get; set; } = string.Empty;
    public double Voltage { get; set; }
}

public class PistonPositionRequestDto
{
    public string PistonType { get; set; } = string.Empty;
    public double TargetPosition { get; set; }
    public double Speed { get; set; } = 5.0;
}

public class PistonJogRequestDto
{
    public string PistonType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // Forward, Backward
    public double Voltage { get; set; } = 5.0;
} 