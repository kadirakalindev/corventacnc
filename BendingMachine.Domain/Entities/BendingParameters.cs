using BendingMachine.Domain.Common;

namespace BendingMachine.Domain.Entities;

public class DomainBendingParameters : BaseEntity
{
    public double BendingRadius { get; set; }
    public double ProfileHeight { get; set; }
    public double TriangleWidth { get; set; }
    public double TriangleAngle { get; set; }
    public int StepCount { get; set; }
    public double StepSize { get; set; }
    public double TargetPressure { get; set; }
    public double PressureTolerance { get; set; }
    public double ResetDistance { get; set; }
    public int StageValue { get; set; }
    
    // Yeni otomatik büküm parametreleri
    public double ProfileLength { get; set; }
    public double ProfileResetDistance { get; set; }
    public int EvacuationTimeSeconds { get; set; }
    public double BendingAngle { get; set; }
    public string ProfileType { get; set; } = "Custom";
    public string Material { get; set; } = "Aluminum";
    public double Thickness { get; set; }
    
    // Hesaplanan değerler
    public double EffectiveBendingRadius { get; set; }
    public double SideBallXPosition { get; set; }
    public double SideBallYPosition { get; set; }
    public double SideBallTravelDistance { get; set; } // Alt Ana piston büküm mesafesi
    public double TriangleHeight { get; set; }
    
    // Yan dayama pozisyonları (şimdilik devre dışı)
    public double LeftReelPosition { get; set; }
    public double LeftBodyPosition { get; set; }
    public double LeftJoinPosition { get; set; }
    public double RightReelPosition { get; set; }
    public double RightBodyPosition { get; set; }
    public double RightJoinPosition { get; set; }
} 