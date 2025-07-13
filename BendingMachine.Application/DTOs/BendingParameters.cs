namespace BendingMachine.Application.DTOs;

public class BendingParameters
{
    // Top Çapları
    public double TopBallInnerDiameter { get; set; } = 220;
    public double BottomBallDiameter { get; set; } = 220;
    public double SideBallDiameter { get; set; } = 220;
    
    // Büküm Parametreleri
    public double BendingRadius { get; set; } = 500;
    public double ProfileHeight { get; set; } = 80;
    public double ProfileLength { get; set; } = 1000;
    public double ProfileThickness { get; set; } = 2;
    
    // Geometri Parametreleri
    public double TriangleWidth { get; set; } = 493;
    public double TriangleAngle { get; set; } = 27;
    
    // Kademe Ayarları
    public int StageValue { get; set; } = 0; // 0, 60, 120mm
    
    // İşlem Parametreleri
    public double StepSize { get; set; } = 20; // Adım büyüklüğü (mm)
    public double TargetPressure { get; set; } = 50; // Hedef basınç (bar)
    public double PressureTolerance { get; set; } = 5; // Basınç toleransı (+-bar)
    
    // Yeni Otomatik Büküm Parametreleri
    public double ProfileResetDistance { get; set; } = 670; // Parça sıfırlama mesafesi (mm)
    public int EvacuationTimeSeconds { get; set; } = 60; // Tahliye süresi (saniye)
    public double SideBallTravelDistance { get; set; } = 40.85; // Alt Ana pistonların hareket mesafesi (mm)
    public double RightReelPosition { get; set; } = 0; // Sağ yan top pozisyonu (devre dışı)
    public double LeftReelPosition { get; set; } = 0; // Sol yan top pozisyonu (devre dışı)
    public double BendingAngle { get; set; } = 45; // Büküm açısı (derece)
    
    // Malzeme Özelikleri
    public string MaterialType { get; set; } = "Aluminum";
    public string ProfileType { get; set; } = "Rectangular";
}

public class BendingCalculationResult
{
    // Hesaplama Sonuçları
    public double EffectiveBendingRadius { get; set; }
    public double TriangleHeight { get; set; }
    public double SideBallTravelDistance { get; set; }
    public double CalculatedRadius { get; set; }
    
    // Büküm Merkezi
    public double BendingCenterX { get; set; }
    public double BendingCenterY { get; set; }
    
    // Üçgen Boyutları
    public double TriangleWidth { get; set; }
    
    // Top Pozisyonları
    public BallPosition BottomBallPosition { get; set; } = new();
    public BallPosition TopBallPosition { get; set; } = new();
    public BallPosition LeftBallPosition { get; set; } = new();
    public BallPosition RightBallPosition { get; set; } = new();
    
    // Adım Hesaplamaları
    public int StepCount { get; set; }
    public double StepSize { get; set; }
    
    // İşlem Bilgileri
    public DateTime CalculationTime { get; set; } = DateTime.UtcNow;
    public bool IsValid { get; set; } = true;
    public string? ValidationMessage { get; set; }
}

public class BallPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class CompressPartRequest
{
    public double TargetPressure { get; set; } = 50;
    public double PressureTolerance { get; set; } = 5;
    public double TargetPosition { get; set; } // mm cinsinden hedef pozisyon
}

public class ResetPartRequest
{
    public double ResetDistance { get; set; } = 100; // mm - parça varlık sensöründen alt top merkezine mesafe
}

public class AutoBendingRequest
{
    public BendingParameters Parameters { get; set; } = new();
    public int SelectedStage { get; set; } = 0;
}

public class AutoBendingStatus
{
    public string Status { get; set; } = "Ready"; // Ready, Calculating, Compressing, Resetting, Bending, Completed, Error
    public string CurrentStep { get; set; } = "";
    public int ProgressPercent { get; set; } = 0;
    public int CurrentStepNumber { get; set; } = 0;
    public int TotalSteps { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
} 