using AutoMapper;
using Microsoft.Extensions.Logging;
using BendingMachine.Application.DTOs;
using BendingMachine.Application.Interfaces;
using BendingMachine.Domain.Interfaces;
using BendingMachine.Domain.Enums;
using BendingMachine.Domain.Entities;
using DomainBendingParameters = BendingMachine.Domain.Entities.DomainBendingParameters;

namespace BendingMachine.Application.Services;

/// <summary>
/// Bending operations business logic service
/// </summary>
public class BendingService : IBendingService
{
    private readonly IMachineDriver _machineDriver;
    private readonly ILogger<BendingService> _logger;
    private readonly IMapper _mapper;
    private bool _isBending = false;
    private bool _isPaused = false;
    private string _currentStep = string.Empty;
    private int _currentStepIndex = 0;
    private readonly List<string> _bendingSteps = new()
    {
        "Initialize",
        "Safety Check",
        "Position Profile", 
        "Start Hydraulic",
        "Compress Part",
        "Execute Bending",
        "Release Part",
        "Return to Home",
        "Complete"
    };

    // Events
    public event EventHandler<BendingStepDto>? BendingStepCompleted;
    public event EventHandler<string>? BendingError;
    public event EventHandler<BendingParametersDto>? BendingStarted;
    public event EventHandler<BendingParametersDto>? BendingCompleted;
    
    // Event'i kullan ki uyarı vermesin
    protected virtual void OnBendingCompleted(BendingParametersDto parameters)
    {
        BendingCompleted?.Invoke(this, parameters);
    }

    public BendingService(
        IMachineDriver machineDriver,
        ILogger<BendingService> logger,
        IMapper mapper)
    {
        _machineDriver = machineDriver ?? throw new ArgumentNullException(nameof(machineDriver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    #region Bending Calculations

    public Task<BendingCalculationResultDto> CalculateBendingAsync(BendingCalculationRequestDto request)
    {
        try
        {
            _logger.LogInformation("Büküm hesaplaması başlatılıyor...");

            // HESAPLAMA_RAPORU.md'deki formüller
            var triangleHeightRad = Math.PI * request.TriangleAngle / 180.0;
            var triangleHeight = request.TriangleWidth / (2 * Math.Tan(triangleHeightRad));
            
            var effectiveBendingRadius = request.BendingRadius + (request.ProfileHeight / 2);
            
            var sideBallXPosition = (request.BottomBallDiameter / 2) + effectiveBendingRadius - triangleHeight;
            var sideBallYPosition = Math.Sqrt(Math.Pow(effectiveBendingRadius, 2) - Math.Pow(sideBallXPosition, 2));
            var sideBallTravelDistance = sideBallYPosition - (request.BottomBallDiameter / 2);
            
            var stepCount = (int)Math.Ceiling(sideBallTravelDistance / request.StepSize);

            var result = new BendingCalculationResultDto
            {
                EffectiveBendingRadius = effectiveBendingRadius,
                SideBallXPosition = sideBallXPosition,
                SideBallYPosition = sideBallYPosition,
                SideBallTravelDistance = sideBallTravelDistance,
                TriangleHeight = triangleHeight,
                StepCount = stepCount,
                StepDistance = stepCount > 0 ? sideBallTravelDistance / stepCount : 0
            };

            _logger.LogInformation("Büküm hesaplaması tamamlandı - Step Count: {StepCount}", stepCount);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm hesaplaması sırasında hata oluştu");
            throw;
        }
    }

    public Task<bool> ValidateBendingParametersAsync(BendingParametersDto parameters)
    {
        try
        {
            // Parametre validasyonu
            if (parameters.BendingRadius < 100 || parameters.BendingRadius > 5000)
            {
                throw new ArgumentException("Büküm yarıçapı 100-5000mm arasında olmalıdır");
            }

            if (parameters.ProfileHeight < 10 || parameters.ProfileHeight > 200)
            {
                throw new ArgumentException("Profil yüksekliği 10-200mm arasında olmalıdır");
            }

            _logger.LogInformation("Büküm parametreleri validasyonu başarılı");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm parametreleri validasyonu başarısız");
            throw;
        }
    }

    #endregion

    #region Automatic Bending Process

    public async Task<bool> StartAutoBendingAsync(AutoBendingRequestDto request)
    {
        try
        {
            if (_isBending)
            {
                _logger.LogWarning("Zaten bir büküm işlemi devam ediyor");
                return false;
            }

            _logger.LogInformation("Otomatik büküm işlemi başlatılıyor...");

            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - büküm başlatılamaz");
                return false;
            }

            // Parametreleri validate et
            var isValid = await ValidateBendingParametersAsync(request.BendingParameters);
            if (!isValid)
            {
                _logger.LogWarning("Büküm parametreleri geçersiz");
                return false;
            }

            _isBending = true;
            _currentStep = "Initialize";
            _currentStepIndex = 0;

            // Convert DTO to Domain entity
            var domainParameters = _mapper.Map<DomainBendingParameters>(request.BendingParameters);
            
            // Driver'a gönder
            var result = await _machineDriver.ExecuteAutoBendingAsync(domainParameters);

            if (result)
            {
                BendingStarted?.Invoke(this, request.BendingParameters);
                _logger.LogInformation("Otomatik büküm işlemi başarıyla başlatıldı");
            }
            else
            {
                _isBending = false;
                _currentStep = "Stopped";
                _currentStepIndex = 0;
                _logger.LogWarning("Otomatik büküm işlemi başlatılamadı");
            }

            return result;
        }
        catch (Exception ex)
        {
            _isBending = false;
            _currentStep = "Stopped";
            _currentStepIndex = 0;
            _logger.LogError(ex, "Otomatik büküm başlatma sırasında hata oluştu");
            BendingError?.Invoke(this, $"Büküm başlatma hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ExecuteAutoBendingAsync(BendingParameters parameters)
    {
        try
        {
            _logger.LogInformation("🚀 ExecuteAutoBendingAsync başlatılıyor...");
            
            // Application DTO'sunu Domain entity'ye çevir
            var domainParameters = new DomainBendingParameters
            {
                ProfileLength = parameters.ProfileLength,
                ProfileHeight = parameters.ProfileHeight,
                ProfileResetDistance = parameters.ProfileResetDistance,
                StageValue = parameters.StageValue,
                StepSize = parameters.StepSize,
                TargetPressure = parameters.TargetPressure,
                EvacuationTimeSeconds = parameters.EvacuationTimeSeconds,
                BendingAngle = parameters.BendingAngle,
                ProfileType = parameters.ProfileType,
                Material = parameters.MaterialType,
                Thickness = parameters.ProfileThickness,
                
                // ✅ BÜKÜM İÇİN: sideBallTravelDistance kullanılıyor
                SideBallTravelDistance = parameters.SideBallTravelDistance,
                
                // Yan dayama pozisyonları (devre dışı)
                RightReelPosition = parameters.RightReelPosition, // 0
                LeftReelPosition = parameters.LeftReelPosition,   // 0
                
                // Sabit değerler
                BendingRadius = parameters.BendingRadius,
                PressureTolerance = parameters.PressureTolerance
            };
            
            // Driver'a gönder
            var result = await _machineDriver.ExecuteAutoBendingAsync(domainParameters);
            
            _logger.LogInformation("✅ ExecuteAutoBendingAsync tamamlandı - Sonuç: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ExecuteAutoBendingAsync sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopAutoBendingAsync()
    {
        try
        {
            _logger.LogInformation("Otomatik büküm işlemi durduruluyor...");

            if (!_isBending)
            {
                _logger.LogWarning("Aktif büküm işlemi bulunamadı");
                return false;
            }
            
            var result = await _machineDriver.StopAllPistonsAsync();
            
            if (result)
            {
                _isBending = false;
                _isPaused = false;
                _currentStep = "Stopped";
                _currentStepIndex = 0;
                _logger.LogInformation("Otomatik büküm işlemi başarıyla durduruldu");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Otomatik büküm durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> PauseAutoBendingAsync()
    {
        try
        {
            _logger.LogInformation("Otomatik büküm işlemi duraklatılıyor...");
            // TODO: Pause logic implement edilecek
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm duraklatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResumeAutoBendingAsync()
    {
        try
        {
            _logger.LogInformation("Otomatik büküm işlemi devam ettiriliyor...");
            // TODO: Resume logic implement edilecek
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm devam ettirme sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Step-by-Step Bending

    public async Task<bool> ExecuteBendingStepAsync(BendingStepDto step)
    {
        try
        {
            _logger.LogInformation("Büküm adımı çalıştırılıyor - Step: {StepNumber}", step.StepNumber);

            // Güvenlik kontrolü
            if (!await _machineDriver.CheckSafetyAsync())
            {
                _logger.LogWarning("Güvenlik kontrolü başarısız - büküm adımı çalıştırılamaz");
                return false;
            }

            // Sol ve sağ pistonları hareket ettir
            var leftTask = _machineDriver.MovePistonToPositionAsync(PistonType.LeftPiston, step.LeftPosition);
            var rightTask = _machineDriver.MovePistonToPositionAsync(PistonType.RightPiston, step.RightPosition);

            var results = await Task.WhenAll(leftTask, rightTask);

            if (results.All(r => r))
            {
                BendingStepCompleted?.Invoke(this, step);
                _logger.LogInformation("Büküm adımı başarıyla tamamlandı - Step: {StepNumber}", step.StepNumber);
                return true;
            }
            else
            {
                _logger.LogWarning("Büküm adımı başarısız - Step: {StepNumber}", step.StepNumber);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm adımı çalıştırılırken hata oluştu - Step: {StepNumber}", step.StepNumber);
            BendingError?.Invoke(this, $"Büküm adımı hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<List<BendingStepDto>> GenerateBendingStepsAsync(BendingParametersDto parameters)
    {
        try
        {
            var calculation = new BendingCalculationRequestDto
            {
                BottomBallDiameter = 220, // Default
                BendingRadius = parameters.BendingRadius,
                ProfileHeight = parameters.ProfileHeight,
                TriangleWidth = parameters.TriangleWidth,
                TriangleAngle = parameters.TriangleAngle,
                StepSize = parameters.StepSize
            };

            var result = await CalculateBendingAsync(calculation);
            var steps = new List<BendingStepDto>();

            for (int i = 1; i <= result.StepCount; i++)
            {
                var stepDistance = (result.StepDistance * i);
                
                steps.Add(new BendingStepDto
                {
                    StepNumber = i,
                    LeftPosition = stepDistance,
                    RightPosition = stepDistance,
                    RotationAngle = 0,
                    RotationDirection = "Clockwise",
                    RotationSpeed = 10.0,
                    RotationDuration = 1.0
                });
            }

            _logger.LogInformation("{StepCount} büküm adımı oluşturuldu", steps.Count);
            return steps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm adımları oluşturulurken hata oluştu");
            throw;
        }
    }

    #endregion

    #region Stage Control

    public async Task<bool> SetStageAsync(int stageValue)
    {
        try
        {
            _logger.LogInformation("Büküm servisi: Stage ayarlanıyor - {Stage}mm", stageValue);
            return await _machineDriver.SetStageAsync(stageValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage ayarlama başarısız - Stage: {Stage}", stageValue);
            BendingError?.Invoke(this, $"Stage ayarlama hatası: {ex.Message}");
            return false;
        }
    }

    public async Task<int> GetCurrentStageAsync()
    {
        try
        {
            return await _machineDriver.GetCurrentStageAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mevcut stage okunamadı");
            return 0;
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut stage listesini döndürür
    /// </summary>
    public async Task<List<StageConfigDto>> GetAvailableStagesAsync()
    {
        try
        {
            // TODO: _machineDriver üzerinden stage listesi alınacak
            // Şu an için hardcoded döndürüyoruz
            var stages = new List<StageConfigDto>
            {
                new() { Name = "Stage 0", Value = 0, Description = "Sıfır pozisyon" },
                new() { Name = "Stage 60", Value = 60, Description = "60mm stage" },
                new() { Name = "Stage 120", Value = 120, Description = "120mm stage" }
            };
            return await Task.FromResult(stages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage listesi okunamadı");
            return new List<StageConfigDto>();
        }
    }

    #endregion

    #region Profile Operations

    public async Task<bool> CompressProfileAsync(double targetPressure, double tolerance)
    {
        try
        {
            _logger.LogInformation("Profil sıkıştırılıyor - Hedef Basınç: {Pressure} bar", targetPressure);
            return await _machineDriver.CompressPartAsync(targetPressure, tolerance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profil sıkıştırma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResetProfilePositionAsync(double resetDistance)
    {
        try
        {
            _logger.LogInformation("Profil pozisyonu sıfırlanıyor - Mesafe: {Distance}mm", resetDistance);
            return await _machineDriver.ResetPartPositionAsync(resetDistance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profil pozisyon sıfırlama sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResetRulersAsync()
    {
        try
        {
            _logger.LogInformation("Cetveller sıfırlanıyor...");
            return await _machineDriver.ResetRulersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetvel sıfırlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Side Support Positioning

    public async Task<bool> SetSideSupportPositionsAsync(
        double leftReelPosition, double leftBodyPosition, double leftJoinPosition,
        double rightReelPosition, double rightBodyPosition, double rightJoinPosition)
    {
        try
        {
            _logger.LogInformation("Yan dayama pozisyonları ayarlanıyor...");

            // Yan dayama pistonlarını pozisyona hareket ettir
            var tasks = new[]
            {
                _machineDriver.MovePistonToPositionAsync(PistonType.LeftReelPiston, leftReelPosition),
                _machineDriver.MovePistonToPositionAsync(PistonType.LeftBodyPiston, leftBodyPosition),
                _machineDriver.MovePistonToPositionAsync(PistonType.LeftJoinPiston, leftJoinPosition),
                _machineDriver.MovePistonToPositionAsync(PistonType.RightReelPiston, rightReelPosition),
                _machineDriver.MovePistonToPositionAsync(PistonType.RightBodyPiston, rightBodyPosition),
                _machineDriver.MovePistonToPositionAsync(PistonType.RightJoinPiston, rightJoinPosition)
            };

            var results = await Task.WhenAll(tasks);
            var success = results.All(r => r);

            if (success)
            {
                _logger.LogInformation("Yan dayama pozisyonları başarıyla ayarlandı");
            }
            else
            {
                _logger.LogWarning("Bazı yan dayama pozisyonları ayarlanamadı");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yan dayama pozisyonu ayarlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Rotation Control

    public async Task<bool> StartRotationAsync(string direction, double speed)
    {
        try
        {
            if (!Enum.TryParse<RotationDirection>(direction, out var rotationDirection))
            {
                throw new ArgumentException($"Geçersiz dönüş yönü: {direction}");
            }

            _logger.LogInformation("Rotasyon başlatılıyor - Yön: {Direction}, Hız: {Speed}", direction, speed);
            return await _machineDriver.StartRotationAsync(rotationDirection, speed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopRotationAsync()
    {
        try
        {
            _logger.LogInformation("Rotasyon durduruluyor...");
            return await _machineDriver.StopRotationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> SetRotationSpeedAsync(double speed)
    {
        try
        {
            _logger.LogInformation("Rotasyon hızı ayarlanıyor: {Speed}", speed);
            return await _machineDriver.SetRotationSpeedAsync(speed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon hızı ayarlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Status

    public async Task<bool> IsBendingInProgressAsync()
    {
        return await Task.FromResult(_isBending);
    }

    public async Task<BendingParametersDto?> GetCurrentBendingParametersAsync()
    {
        return await Task.FromResult<BendingParametersDto?>(null); // Şu anda aktif parametre takibi yok
    }

    #endregion

    public Task<BendingParametersDto> CalculateBendingParametersAsync(BendingCalculationRequestDto request)
    {
        try
        {
            _logger.LogInformation("Büküm parametreleri hesaplanıyor...");
            
            // Basic calculation - bu gerçek hesaplama algoritması ile değiştirilecek
            var parameters = new BendingParametersDto
            {
                Force = CalculateRequiredForce(request),
                Speed = CalculateOptimalSpeed(request),
                ProfileLength = request.ProfileLength,
                BendingAngle = request.BendingAngle,
                ProfileType = request.ProfileType,
                Material = request.Material,
                Thickness = request.Thickness
            };
            
            _logger.LogInformation("Büküm parametreleri hesaplandı - Force: {Force}N, Speed: {Speed}mm/s", 
                parameters.Force, parameters.Speed);
            
            return Task.FromResult(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm parametresi hesaplama sırasında hata oluştu");
            throw;
        }
    }



    public async Task<bool> StartBendingAsync(BendingParametersDto parameters)
    {
        try
        {
            _logger.LogInformation("Büküm işlemi başlatılıyor...");
            
            if (_isBending)
            {
                _logger.LogWarning("Büküm işlemi zaten aktif");
                return false;
            }
            
            var isValid = await ValidateBendingParametersAsync(parameters);
            if (!isValid)
            {
                _logger.LogWarning("Geçersiz büküm parametreleri");
                return false;
            }
            
            // Convert DTO to Domain entity
            var domainParameters = _mapper.Map<DomainBendingParameters>(parameters);
            
            var result = await _machineDriver.ExecuteAutoBendingAsync(domainParameters);
            
            if (result)
            {
                _isBending = true;
                _currentStep = "Initialize";
                _currentStepIndex = 0;
                _logger.LogInformation("Büküm işlemi başarıyla başlatıldı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopBendingAsync()
    {
        try
        {
            _logger.LogInformation("Büküm işlemi durduruluyor...");
            
            if (!_isBending)
            {
                _logger.LogWarning("Aktif büküm işlemi bulunamadı");
                return false;
            }
            
            var result = await _machineDriver.StopAllPistonsAsync();
            
            if (result)
            {
                _isBending = false;
                _isPaused = false;
                _currentStep = "Stopped";
                _logger.LogInformation("Büküm işlemi başarıyla durduruldu");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> PauseBendingAsync()
    {
        try
        {
            _logger.LogInformation("Büküm işlemi duraklatılıyor...");
            
            if (!_isBending || _isPaused)
            {
                _logger.LogWarning("Duraklatılacak aktif büküm işlemi bulunamadı");
                return false;
            }
            
            var result = await _machineDriver.StopAllPistonsAsync();
            
            if (result)
            {
                _isPaused = true;
                _logger.LogInformation("Büküm işlemi duraklatıldı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm duraklatma sırasında hata oluştu");
            return false;
        }
    }

    public Task<bool> ResumeBendingAsync()
    {
        try
        {
            _logger.LogInformation("Büküm işlemi devam ettiriliyor...");
            
            if (!_isBending || !_isPaused)
            {
                _logger.LogWarning("Devam ettirilecek duraklatılmış büküm işlemi bulunamadı");
                return Task.FromResult(false);
            }
            
            _isPaused = false;
            _logger.LogInformation("Büküm işlemi devam ettirildi");
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm devam ettirme sırasında hata oluştu");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> CompressPartAsync(double pressure)
    {
        try
        {
            _logger.LogInformation("Parça sıkıştırılıyor - Basınç: {Pressure}bar", pressure);
            
            // Side pistonları kullanarak parçayı sıkıştır
            var leftPosition = new SidePistonPositionDto { LeftPistonPosition = pressure * 10, RightPistonPosition = pressure * 10 };
            var result = await MoveSidePistonsAsync(leftPosition);
            
            if (result)
            {
                _logger.LogInformation("Parça başarıyla sıkıştırıldı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça sıkıştırma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ReleasePartAsync()
    {
        try
        {
            _logger.LogInformation("Parça bırakılıyor...");
            
            // Side pistonları home pozisyonuna getir
            var homePosition = new SidePistonPositionDto { LeftPistonPosition = 0, RightPistonPosition = 0 };
            var result = await MoveSidePistonsAsync(homePosition);
            
            if (result)
            {
                _logger.LogInformation("Parça başarıyla bırakıldı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça bırakma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> SetTopPositionAsync(double position)
    {
        try
        {
            _logger.LogInformation("Üst pozisyon ayarlanıyor: {Position}mm", position);
            
            var result = await _machineDriver.MovePistonAsync("TopCenter", position);
            
            if (result)
            {
                _logger.LogInformation("Üst pozisyon başarıyla ayarlandı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Üst pozisyon ayarlama sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> SetBottomPositionAsync(double position)
    {
        try
        {
            _logger.LogInformation("Alt pozisyon ayarlanıyor: {Position}mm", position);
            
            var result = await _machineDriver.MovePistonAsync("BottomCenter", position);
            
            if (result)
            {
                _logger.LogInformation("Alt pozisyon başarıyla ayarlandı");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alt pozisyon ayarlama sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> MoveSidePistonsAsync(SidePistonPositionDto position)
    {
        try
        {
            _logger.LogInformation("Yan pistonlar hareket ettiriliyor - Sol: {Left}mm, Sağ: {Right}mm", 
                position.LeftPistonPosition, position.RightPistonPosition);
            
            var leftResult = await _machineDriver.MovePistonAsync("LeftSide", position.LeftPistonPosition);
            var rightResult = await _machineDriver.MovePistonAsync("RightSide", position.RightPistonPosition);
            
            var result = leftResult && rightResult;
            
            if (result)
            {
                _logger.LogInformation("Yan pistonlar başarıyla hareket ettirildi");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yan piston hareketi sırasında hata oluştu");
            return false;
        }
    }

    public async Task<SidePistonPositionDto> GetSidePistonPositionsAsync()
    {
        try
        {
            var status = await _machineDriver.GetMachineStatusAsync();
            
            var leftPiston = status.Pistons.FirstOrDefault(p => p.Type == BendingMachine.Domain.Enums.PistonType.LeftSide);
            var rightPiston = status.Pistons.FirstOrDefault(p => p.Type == BendingMachine.Domain.Enums.PistonType.RightSide);
            
            return new SidePistonPositionDto
            {
                LeftPistonPosition = leftPiston?.CurrentPosition ?? 0,
                RightPistonPosition = rightPiston?.CurrentPosition ?? 0,
                IsLeftPistonAtTarget = leftPiston?.IsAtTarget ?? false,
                IsRightPistonAtTarget = rightPiston?.IsAtTarget ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yan piston pozisyonları alınırken hata oluştu");
            return new SidePistonPositionDto();
        }
    }

    public async Task<List<string>> GetStepsAsync()
    {
        return await Task.FromResult(_bendingSteps.ToList());
    }

    public async Task<bool> ExecuteStepAsync(string stepName)
    {
        try
        {
            _logger.LogInformation("Adım çalıştırılıyor: {StepName}", stepName);
            
            var stepIndex = _bendingSteps.IndexOf(stepName);
            if (stepIndex == -1)
            {
                _logger.LogWarning("Geçersiz adım adı: {StepName}", stepName);
                return false;
            }
            
            _currentStep = stepName;
            _currentStepIndex = stepIndex;
            
            // Adıma göre operasyon çalıştır
            var result = stepName switch
            {
                "Initialize" => await InitializeAsync(),
                "Safety Check" => await _machineDriver.CheckSafetyAsync(),
                "Position Profile" => true, // Manuel pozisyonlama
                "Start Hydraulic" => await _machineDriver.StartHydraulicMotorAsync(),
                "Compress Part" => await CompressPartAsync(5.0), // Varsayılan basınç
                "Execute Bending" => true, // Manuel büküm
                "Release Part" => await ReleasePartAsync(),
                "Return to Home" => await ReturnToHomeAsync(),
                "Complete" => await CompleteAsync(),
                _ => false
            };
            
            if (result)
            {
                _logger.LogInformation("Adım başarıyla tamamlandı: {StepName}", stepName);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adım çalıştırma sırasında hata oluştu: {StepName}", stepName);
            return false;
        }
    }

    public async Task<BendingStatusDto> GetBendingStatusAsync()
    {
        try
        {
            var sidePistons = await GetSidePistonPositionsAsync();
            var machineStatus = await _machineDriver.GetMachineStatusAsync();
            
            var topPiston = machineStatus.Pistons.FirstOrDefault(p => p.Type == BendingMachine.Domain.Enums.PistonType.TopCenter);
            var bottomPiston = machineStatus.Pistons.FirstOrDefault(p => p.Type == BendingMachine.Domain.Enums.PistonType.BottomCenter);
            
            return new BendingStatusDto
            {
                IsActive = _isBending,
                IsPaused = _isPaused,
                CurrentStep = _currentStep,
                CurrentStepIndex = _currentStepIndex,
                TotalSteps = _bendingSteps.Count,
                Progress = _bendingSteps.Count > 0 ? (double)_currentStepIndex / _bendingSteps.Count * 100 : 0,
                TopPosition = topPiston?.CurrentPosition ?? 0,
                BottomPosition = bottomPiston?.CurrentPosition ?? 0,
                SidePistons = sidePistons
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm durumu alınırken hata oluştu");
            return new BendingStatusDto();
        }
    }

    // Private helper methods
    private double CalculateRequiredForce(BendingCalculationRequestDto request)
    {
        // Basit force hesaplaması - gerçek algoritma ile değiştirilecek
        return request.Thickness * 100 + request.BendingAngle * 2;
    }

    private double CalculateOptimalSpeed(BendingCalculationRequestDto request)
    {
        // Basit speed hesaplaması - gerçek algoritma ile değiştirilecek
        return Math.Max(10, 50 - request.Thickness * 5);
    }

    private async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Sistem başlatılıyor...");
            return await _machineDriver.CheckSafetyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sistem başlatma sırasında hata oluştu");
            return false;
        }
    }

    private async Task<bool> ReturnToHomeAsync()
    {
        try
        {
            _logger.LogInformation("Ana pozisyona dönülüyor...");
            
            var homePosition = new SidePistonPositionDto { LeftPistonPosition = 0, RightPistonPosition = 0 };
            var sidePistonsResult = await MoveSidePistonsAsync(homePosition);
            var topResult = await SetTopPositionAsync(0);
            var bottomResult = await SetBottomPositionAsync(0);
            
            return sidePistonsResult && topResult && bottomResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ana pozisyona dönüş sırasında hata oluştu");
            return false;
        }
    }

    private Task<bool> CompleteAsync()
    {
        try
        {
            _logger.LogInformation("Büküm işlemi tamamlanıyor...");
            
            _isBending = false;
            _isPaused = false;
            _currentStep = "Complete";
            _currentStepIndex = _bendingSteps.Count - 1;
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm tamamlama sırasında hata oluştu");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> ExecutePasoTestAsync(double sideBallTravelDistance, double profileLength, double stepSize = 20.0, int evacuationTimeSeconds = 10)
    {
        try
        {
            _logger.LogInformation("🧪 BendingService: Paso test başlatılıyor - Distance: {Distance}mm, Length: {Length}mm", 
                sideBallTravelDistance, profileLength);
            
            var result = await _machineDriver.ExecutePasoTestAsync(sideBallTravelDistance, profileLength, stepSize, evacuationTimeSeconds);
            
            _logger.LogInformation("✅ BendingService: Paso test tamamlandı - Sonuç: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ BendingService: Paso test sırasında hata oluştu");
            return false;
        }
    }
}