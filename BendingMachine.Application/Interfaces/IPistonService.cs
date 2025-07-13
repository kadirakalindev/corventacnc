using BendingMachine.Application.DTOs;

namespace BendingMachine.Application.Interfaces;

/// <summary>
/// Piston operations için business logic interface
/// </summary>
public interface IPistonService
{
    // Piston Status
    Task<List<PistonStatusDto>> GetAllPistonsAsync();
    Task<PistonStatusDto> GetPistonAsync(string pistonType);
    
    // Piston Movement
    Task<bool> MovePistonAsync(PistonMoveRequestDto request);
    Task<bool> MovePistonToPositionAsync(PistonPositionRequestDto request);
    Task<bool> JogPistonAsync(PistonJogRequestDto request);
    Task<bool> StopPistonAsync(string pistonType);
    Task<bool> StopAllPistonsAsync();
    
    // Position Control
    Task<bool> SetPistonPositionAsync(string pistonType, double position);
    Task<double> GetPistonPositionAsync(string pistonType);
    Task<bool> ResetRulersAsync();
    Task<bool> ResetSpecificRulerAsync(string pistonType);
    
    // Safety & Validation
    Task<bool> ValidatePistonMovementAsync(string pistonType, double voltage);
    Task<bool> ValidatePistonPositionAsync(string pistonType, double position);
    Task<bool> IsPistonSafeToMoveAsync(string pistonType);
    
    // Events
    event EventHandler<PistonStatusDto> PistonMoved;
    event EventHandler<string> PistonError;
} 