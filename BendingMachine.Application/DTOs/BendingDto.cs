using BendingMachine.Application.Interfaces;

namespace BendingMachine.Application.DTOs;

public class BendingParametersDto
{
    // Profil Bilgileri (API'de kullanılan)
    public double ProfileLength { get; set; }
    public double BendingAngle { get; set; }
    public string ProfileType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double Thickness { get; set; }
    
    // Hesaplama Parametreleri
    public double BendingRadius { get; set; }
    public double ProfileHeight { get; set; }
    
    // Makine Parametreleri (BendingService için)
    public double Force { get; set; }
    public double Speed { get; set; }
    public double TriangleWidth { get; set; }
    public double TriangleAngle { get; set; }
    public int StepCount { get; set; }
    public double StepSize { get; set; }
    public double TargetPressure { get; set; }
    public double PressureTolerance { get; set; }
    public double ResetDistance { get; set; }
    public int StageValue { get; set; }
    
    // Hesaplanan değerler
    public double EffectiveBendingRadius { get; set; }
    public double SideBallXPosition { get; set; }
    public double SideBallYPosition { get; set; }
    public double SideBallTravelDistance { get; set; }
    public double TriangleHeight { get; set; }
    
    // Yan dayama pozisyonları
    public double LeftReelPosition { get; set; }
    public double LeftBodyPosition { get; set; }
    public double LeftJoinPosition { get; set; }
    public double RightReelPosition { get; set; }
    public double RightBodyPosition { get; set; }
    public double RightJoinPosition { get; set; }
}

public class BendingCalculationRequestDto
{
    // Mevcut property'lere ek olarak API'de kullanılan property'ler
    public double ProfileLength { get; set; }
    public double BendingAngle { get; set; }
    public string ProfileType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double Thickness { get; set; }
    
    public double BottomBallDiameter { get; set; }
    public double BendingRadius { get; set; }
    public double ProfileHeight { get; set; }
    public double TriangleWidth { get; set; }
    public double TriangleAngle { get; set; }
    public double StepSize { get; set; } = 5.0;
}

public class BendingCalculationResultDto
{
    public double EffectiveBendingRadius { get; set; }
    public double SideBallXPosition { get; set; }
    public double SideBallYPosition { get; set; }
    public double SideBallTravelDistance { get; set; }
    public double TriangleHeight { get; set; }
    public int StepCount { get; set; }
    public double StepDistance { get; set; }
}

public class AutoBendingRequestDto
{
    public BendingParametersDto BendingParameters { get; set; } = new();
    public List<BendingStepDto> Steps { get; set; } = new();
}

public class BendingStepDto
{
    public int StepNumber { get; set; }
    public double LeftPosition { get; set; }
    public double RightPosition { get; set; }
    public double TopPosition { get; set; }
    public double BottomPosition { get; set; }
    public List<SidePistonPositionDto> SidePistons { get; set; } = new();
    public double RotationAngle { get; set; }
    public string RotationDirection { get; set; } = "Clockwise";
    public double RotationSpeed { get; set; } = 10.0;
    public double RotationDuration { get; set; } = 1.0;
}

/// <summary>
/// ✅ YENİ - Hassas konumlandırma konfigürasyonu DTO'su
/// </summary>
public class PrecisionControlConfigDto
{
    public double FastSpeed { get; set; } = 70.0;    // %70 - İlk %80 mesafe
    public double MediumSpeed { get; set; } = 40.0;  // %40 - %80-95 mesafe  
    public double SlowSpeed { get; set; } = 15.0;    // %15 - Son %5 mesafe
    public double PreciseSpeed { get; set; } = 20.0; // %20 - Hassas konumlandırma
    public double BallDiameter { get; set; } = 220.0; // mm
    public int EncoderPulsesPerRevolution { get; set; } = 1024;
    public double EncoderFreezeTimeoutSeconds { get; set; } = 2.0;
    public int MaxEncoderStuckCount { get; set; } = 3;
}

public class PasoTestRequest
{
    public double SideBallTravelDistance { get; set; }
    public double ProfileLength { get; set; }
    public double StepSize { get; set; } = 20.0;
    public int EvacuationTimeSeconds { get; set; } = 10;
}

public class PasoTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PasoTestData? Data { get; set; }
}

public class PasoTestData
{
    public int TotalSteps { get; set; }
    public double TotalLeftDistance { get; set; }
    public double TotalRightDistance { get; set; }
    public string ActiveSensor { get; set; } = string.Empty;
    public string FirstBendingSide { get; set; } = string.Empty;
    public double InitialReverseDistance { get; set; }
    public double RotationDistance { get; set; }
    public List<PasoStepInfo> Steps { get; set; } = new();
}

public class PasoStepInfo
{
    public int StepNumber { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string PistonSide { get; set; } = string.Empty;
    public double FromPosition { get; set; }
    public double ToPosition { get; set; }
    public double Distance { get; set; }
    public string Description { get; set; } = string.Empty;
} 