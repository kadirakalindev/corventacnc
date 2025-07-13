using AutoMapper;
using Microsoft.Extensions.Logging;
using BendingMachine.Application.DTOs;
using BendingMachine.Application.Interfaces;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Application.Services;

public class PistonService : IPistonService
{
    private readonly IMachineDriver _machineDriver;
    private readonly IMapper _mapper;
    private readonly ILogger<PistonService> _logger;

    // Events
    public event EventHandler<PistonStatusDto>? PistonMoved;
    public event EventHandler<string>? PistonError;

    public PistonService(
        IMachineDriver machineDriver,
        IMapper mapper,
        ILogger<PistonService> logger)
    {
        _machineDriver = machineDriver;
        _mapper = mapper;
        _logger = logger;

        // Driver events'lerini service events'lerine yönlendir
        _machineDriver.PistonMoved += OnDriverPistonMoved;
    }

    #region Piston Status

    public async Task<List<PistonStatusDto>> GetAllPistonsAsync()
    {
        try
        {
            var pistons = await _machineDriver.GetAllPistonsAsync();
            return _mapper.Map<List<PistonStatusDto>>(pistons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm pistonların durumu alınırken hata oluştu");
            throw;
        }
    }

    public async Task<PistonStatusDto> GetPistonAsync(string pistonType)
    {
        try
        {
            if (!Enum.TryParse<PistonType>(pistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {pistonType}");
            }

            var piston = await _machineDriver.GetPistonAsync(type);
            return _mapper.Map<PistonStatusDto>(piston);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston durumu alınırken hata oluştu", pistonType);
            throw;
        }
    }

    #endregion

    #region Piston Movement

    public async Task<bool> MovePistonAsync(PistonMoveRequestDto request)
    {
        try
        {
            if (!await ValidatePistonMovementAsync(request.PistonType, request.Voltage))
            {
                return false;
            }

            if (!Enum.TryParse<PistonType>(request.PistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {request.PistonType}");
            }

            _logger.LogInformation("{PistonType} piston hareket ettiriliyor - Voltaj: {Voltage}V", 
                request.PistonType, request.Voltage);

            return await _machineDriver.MovePistonAsync(type, request.Voltage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston hareket ettirilirken hata oluştu", request.PistonType);
            PistonError?.Invoke(this, $"Piston hareket hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> MovePistonToPositionAsync(PistonPositionRequestDto request)
    {
        try
        {
            if (!await ValidatePistonPositionAsync(request.PistonType, request.TargetPosition))
            {
                return false;
            }

            if (!Enum.TryParse<PistonType>(request.PistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {request.PistonType}");
            }

            _logger.LogInformation("{PistonType} piston pozisyona hareket ettiriliyor - Hedef: {Position}mm", 
                request.PistonType, request.TargetPosition);

            return await _machineDriver.MovePistonToPositionAsync(type, request.TargetPosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston pozisyona hareket ettirilirken hata oluştu", request.PistonType);
            PistonError?.Invoke(this, $"Piston pozisyon hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> JogPistonAsync(PistonJogRequestDto request)
    {
        try
        {
            if (!await ValidatePistonMovementAsync(request.PistonType, request.Voltage))
            {
                return false;
            }

            if (!Enum.TryParse<PistonType>(request.PistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {request.PistonType}");
            }

            if (!Enum.TryParse<MotionEnum>(request.Direction, out var direction))
            {
                throw new ArgumentException($"Geçersiz hareket yönü: {request.Direction}");
            }

            _logger.LogInformation("{PistonType} piston jog hareket - Yön: {Direction}, Voltaj: {Voltage}V", 
                request.PistonType, request.Direction, request.Voltage);

            return await _machineDriver.JogPistonAsync(type, direction, request.Voltage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston jog hareket sırasında hata oluştu", request.PistonType);
            PistonError?.Invoke(this, $"Piston jog hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopPistonAsync(string pistonType)
    {
        try
        {
            if (!Enum.TryParse<PistonType>(pistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {pistonType}");
            }

            _logger.LogInformation("{PistonType} piston durduruluyor", pistonType);

            return await _machineDriver.StopPistonAsync(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston durdurulurken hata oluştu", pistonType);
            return false;
        }
    }

    public async Task<bool> StopAllPistonsAsync()
    {
        try
        {
            _logger.LogInformation("Tüm pistonlar durduruluyor");

            var pistonTypes = Enum.GetValues<PistonType>();
            var tasks = pistonTypes.Select(type => _machineDriver.StopPistonAsync(type));
            
            var results = await Task.WhenAll(tasks);
            return results.All(result => result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm pistonlar durdurulurken hata oluştu");
            return false;
        }
    }

    #endregion

    #region Position Control

    public async Task<bool> SetPistonPositionAsync(string pistonType, double position)
    {
        var request = new PistonPositionRequestDto
        {
            PistonType = pistonType,
            TargetPosition = position
        };
        return await MovePistonToPositionAsync(request);
    }

    public async Task<double> GetPistonPositionAsync(string pistonType)
    {
        try
        {
            var piston = await GetPistonAsync(pistonType);
            return piston.CurrentPosition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston pozisyonu alınırken hata oluştu", pistonType);
            return 0;
        }
    }

    public async Task<bool> ResetRulersAsync()
    {
        try
        {
            _logger.LogInformation("Tüm cetveller sıfırlanıyor");
            return await _machineDriver.ResetRulersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetveller sıfırlanırken hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResetSpecificRulerAsync(string pistonType)
    {
        try
        {
            if (!Enum.TryParse<PistonType>(pistonType, out var type))
            {
                throw new ArgumentException($"Geçersiz piston tipi: {pistonType}");
            }

            _logger.LogInformation("{PistonType} cetveli sıfırlanıyor", pistonType);
            return await _machineDriver.ResetSpecificRulerAsync(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} cetveli sıfırlanırken hata oluştu", pistonType);
            return false;
        }
    }

    #endregion

    #region Safety & Validation

    public async Task<bool> ValidatePistonMovementAsync(string pistonType, double voltage)
    {
        try
        {
            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - piston hareket edemez");
                return false;
            }

            // Voltaj limiti kontrolü
            if (Math.Abs(voltage) > 10)
            {
                _logger.LogWarning("Voltaj limiti aşıldı: {Voltage}V (Max: ±10V)", voltage);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piston hareket validasyonu sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ValidatePistonPositionAsync(string pistonType, double position)
    {
        try
        {
            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - piston pozisyona hareket edemez");
                return false;
            }

            // Pozisyon sınırları kontrolü
            var maxPosition = pistonType switch
            {
                "TopPiston" => 160.0,
                "BottomPiston" => 195.0,
                "LeftPiston" or "RightPiston" => 422.0,
                "LeftReelPiston" or "RightReelPiston" => 352.0,
                "LeftBodyPiston" or "RightBodyPiston" => 129.0,
                "LeftJoinPiston" or "RightJoinPiston" => 187.0,
                _ => 0.0
            };

            if (position < 0 || position > maxPosition)
            {
                _logger.LogWarning("{PistonType} pozisyon sınırları aşıldı: {Position}mm (Max: {MaxPosition}mm)", 
                    pistonType, position, maxPosition);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piston pozisyon validasyonu sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> IsPistonSafeToMoveAsync(string pistonType)
    {
        try
        {
            // Genel güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                return false;
            }

            // Piston özel kontrolü
            var piston = await GetPistonAsync(pistonType);
            return !piston.IsMoving; // Hareket halinde değilse güvenli
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} güvenlik kontrolü sırasında hata oluştu", pistonType);
            return false;
        }
    }

    #endregion

    #region Event Handlers

    private void OnDriverPistonMoved(object? sender, BendingMachine.Domain.Interfaces.PistonMovedEventArgs e)
    {
        try
        {
            // Event'i DTO'ya çevir ve yayınla
            var pistonDto = new PistonStatusDto
            {
                Type = e.PistonType.ToString(),
                CurrentPosition = e.CurrentPosition,
                TargetPosition = e.TargetPosition,
                Motion = e.Motion.ToString()
            };

            PistonMoved?.Invoke(this, pistonDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piston moved event işlenirken hata oluştu");
        }
    }

    #endregion
} 