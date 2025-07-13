using BendingMachine.Application.DTOs;
using BendingMachine.Domain.Entities;

namespace BendingMachine.Application.Interfaces;

/// <summary>
/// Automatic bending operations için business logic interface
/// </summary>
public interface IBendingService
{
    // Bending Calculations
    Task<BendingCalculationResultDto> CalculateBendingAsync(BendingCalculationRequestDto request);
    
    // Automatic Bending Process
    Task<bool> StartAutoBendingAsync(AutoBendingRequestDto request);
    Task<bool> ExecuteAutoBendingAsync(BendingParameters parameters);
    Task<bool> StopAutoBendingAsync();
    Task<bool> PauseAutoBendingAsync();
    Task<bool> ResumeAutoBendingAsync();
    
    // Step-by-Step Bending
    Task<bool> ExecuteBendingStepAsync(BendingStepDto step);
    Task<List<BendingStepDto>> GenerateBendingStepsAsync(BendingParametersDto parameters);
    
    // Stage Control
    Task<bool> SetStageAsync(int stageValue);
    Task<int> GetCurrentStageAsync();
    
    // Profile Operations
    Task<bool> CompressProfileAsync(double targetPressure, double tolerance);
    Task<bool> ResetProfilePositionAsync(double resetDistance);
    Task<bool> ResetRulersAsync();
    
    // Side Support Positioning
    Task<bool> SetSideSupportPositionsAsync(
        double leftReelPosition, 
        double leftBodyPosition, 
        double leftJoinPosition,
        double rightReelPosition, 
        double rightBodyPosition, 
        double rightJoinPosition);
        
    // Rotation Control
    Task<bool> StartRotationAsync(string direction, double speed);
    Task<bool> StopRotationAsync();
    Task<bool> SetRotationSpeedAsync(double speed);
    
    // Status
    Task<bool> IsBendingInProgressAsync();
    Task<BendingParametersDto?> GetCurrentBendingParametersAsync();
    
    // Additional Methods for Controllers  
    Task<BendingParametersDto> CalculateBendingParametersAsync(BendingCalculationRequestDto request);
    Task<bool> ValidateBendingParametersAsync(BendingParametersDto parameters);
    Task<bool> StartBendingAsync(BendingParametersDto parameters);
    Task<bool> StopBendingAsync();
    Task<bool> CompressPartAsync(double pressure);
    Task<bool> SetTopPositionAsync(double position);
    Task<bool> SetBottomPositionAsync(double position);
    Task<bool> MoveSidePistonsAsync(SidePistonPositionDto position);
    Task<SidePistonPositionDto> GetSidePistonPositionsAsync();
    Task<List<string>> GetStepsAsync();
    Task<bool> ExecuteStepAsync(string stepName);
    Task<BendingStatusDto> GetBendingStatusAsync();
    
    // Events
    event EventHandler<BendingStepDto> BendingStepCompleted;
    event EventHandler<string> BendingError;
    event EventHandler<BendingParametersDto> BendingStarted;
    event EventHandler<BendingParametersDto> BendingCompleted;
    
    /// <summary>
    /// PASO TEST: Sadece paso işlemini test eder
    /// </summary>
    Task<bool> ExecutePasoTestAsync(double sideBallTravelDistance, double profileLength, double stepSize = 20.0, int evacuationTimeSeconds = 10);
}

/// <summary>
/// Side piston position DTO
/// </summary>
public class SidePistonPositionDto
{
    public double LeftPistonPosition { get; set; }
    public double RightPistonPosition { get; set; }
    public bool IsLeftPistonAtTarget { get; set; }
    public bool IsRightPistonAtTarget { get; set; }
    
    // BendingController için ek property'ler
    public int PistonNumber { get; set; }
    public double Position { get; set; }
}

/// <summary>
/// Bending status DTO
/// </summary>
public class BendingStatusDto
{
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public double Progress { get; set; }
    public double TopPosition { get; set; }
    public double BottomPosition { get; set; }
    public SidePistonPositionDto SidePistons { get; set; } = new();
} 