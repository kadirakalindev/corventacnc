using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;
using BendingMachine.Application.DTOs;
using System.ComponentModel.DataAnnotations;
using BendingMachine.Application.Services;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Büküm işlemleri endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Bending Operations")]
public class BendingController : ControllerBase
{
    private readonly IBendingService _bendingService;
    private readonly ILogger<BendingController> _logger;
    private readonly BendingCalculatorService _calculatorService;
    private readonly IMachineService _machineService;

    public BendingController(
        IBendingService bendingService,
        ILogger<BendingController> logger,
        BendingCalculatorService calculatorService,
        IMachineService machineService)
    {
        _bendingService = bendingService;
        _logger = logger;
        _calculatorService = calculatorService;
        _machineService = machineService;
    }



    /// <summary>
    /// Büküm parametrelerini doğrular
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ValidateParameters([FromBody] BendingParametersRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var parameters = new BendingParametersDto
            {
                ProfileLength = request.ProfileLength,
                BendingAngle = request.BendingAngle,
                BendingRadius = request.BendingRadius,
                ProfileType = request.ProfileType,
                Material = request.Material,
                Thickness = request.Thickness
            };

            var result = await _bendingService.ValidateBendingParametersAsync(parameters);

            return Ok(new
            {
                success = true,
                isValid = result,
                message = result ? "Parametreler geçerli" : "Parametreler geçersiz",
                parameters = parameters,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm parametreleri doğrulanırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Validate parameters" });
        }
    }

    /// <summary>
    /// Otomatik büküm işlemi başlatır
    /// </summary>
    [HttpPost("auto-bend")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StartAutoBending([FromBody] AutoBendingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            _logger.LogInformation("🚀 Otomatik büküm başlatılıyor - Stage: {Stage}mm, Yan top mesafesi: {Distance}mm", 
                request.StageNumber, request.SideBallTravelDistance);

            // ✅ DÜZELTME: Parametre mapping'i makine-calisma.md'ye göre düzeltildi
            var bendingParameters = new BendingMachine.Application.DTOs.BendingParameters
                {
                // Kullanıcı parametreleri
                ProfileLength = request.ProfileLength,
                ProfileHeight = request.ProfileHeight,
                StageValue = request.StageNumber,
                StepSize = request.StepSize,
                TargetPressure = request.TargetPressure,
                
                // ✅ YAN DAYAMA PİSTONLARI KALDIRILDI: Kullanıcı talebi doğrultusunda 0 olarak set edildi  
                // Kullanıcı: "Yan dayama pistonlarını şu anlık dahil etmeyeceğiz"
                RightReelPosition = 0, // Kullanıcı talebi doğrultusunda devre dışı
                LeftReelPosition = 0,  // Kullanıcı talebi doğrultusunda devre dışı
                
                // ✅ ASIL BÜKÜM MESAFESİ: Alt Ana pistonların hareket mesafesi
                SideBallTravelDistance = request.SideBallTravelDistance, // Alt Ana pistonlar için gerçek büküm mesafesi
                ProfileResetDistance = request.ProfileResetDistance,
                EvacuationTimeSeconds = request.EvacuationTimeSeconds,
                
                // Sabit değerler - makine.md'den
                TopBallInnerDiameter = 220, // makine.md: 220mm top çapı
                BottomBallDiameter = 220,   // makine.md: 220mm top çapı
                SideBallDiameter = 220,     // makine.md: 220mm top çapı
                BendingRadius = 500,
                ProfileThickness = 2.0,
                BendingAngle = 45,
                PressureTolerance = 5,
                MaterialType = "Aluminum",
                ProfileType = "Custom"
            };

            var totalSteps = Math.Ceiling(request.SideBallTravelDistance / request.StepSize);
            _logger.LogInformation("📋 Büküm parametreleri hazırlandı - Toplam paso: {TotalSteps}, Adım: {StepSize}mm", 
                totalSteps, request.StepSize);

            // Otomatik büküm işlemini başlat
            var result = await _bendingService.ExecuteAutoBendingAsync(bendingParameters);

            return Ok(new
            {
                success = result,
                message = result ? "Otomatik büküm başarıyla tamamlandı" : "Otomatik büküm işlemi başarısız",
                Parameters = new
                {
                    sideBallTravelDistance = request.SideBallTravelDistance,
                    stageNumber = request.StageNumber,
                    profileLength = request.ProfileLength,
                    profileHeight = request.ProfileHeight,
                    profileResetDistance = request.ProfileResetDistance,
                    stepSize = request.StepSize,
                    targetPressure = request.TargetPressure,
                    evacuationTimeSeconds = request.EvacuationTimeSeconds,
                    totalSteps = totalSteps
                },
                Process = new
                {
                    Steps = new[]
                    {
                        "1. Stage ayarlama",
                        "2. Büküm hesabı",
                        "3. Parça sıkıştırma",
                        "4. Parça sıfırlama",
                        "5. Encoder bazlı paso-by-paso büküm",
                        "6. Tahliye süreci"
                    },
                    BendingAlgorithm = new
                    {
                        EncoderBased = "220mm top çapı ile encoder-mesafe dönüşümü",
                        PistonControl = "Sadece Alt Ana pistonlar (LeftPiston/RightPiston)",
                        SideSupportStatus = "Yan dayama pistonları kullanıcı talebi doğrultusunda dahil edilmiyor",
                        FirstBendDirection = "Sol sensör aktifse ilk büküm sol tarafta, sağ sensör aktifse sağ tarafta",
                        StepByStep = "Her paso'da encoder ile kontrollü adım büyüklüğü kadar rotasyon"
                    },
                    SafetyControls = new[]
                    {
                        "Encoder stuck kontrolü (10 ardışık stuck = hata)",
                        "Timeout kontrolü (30s max)",
                        "Ani basınç değişikliği kontrolü",
                        "Parça kırılması/deformasyon tespiti"
                    }
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Otomatik büküm başlatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Start auto bending" });
        }
    }

    /// <summary>
    /// Büküm adımlarını oluşturur
    /// </summary>
    [HttpPost("generate-steps")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GenerateSteps([FromBody] BendingParametersRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var parameters = new BendingParametersDto
            {
                ProfileLength = request.ProfileLength,
                BendingAngle = request.BendingAngle,
                BendingRadius = request.BendingRadius,
                ProfileType = request.ProfileType,
                Material = request.Material,
                Thickness = request.Thickness
            };

            var steps = await _bendingService.GenerateBendingStepsAsync(parameters);

            return Ok(new
            {
                success = true,
                data = steps,
                stepCount = steps.Count,
                message = $"{steps.Count} büküm adımı oluşturuldu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm adımları oluşturulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Generate bending steps" });
        }
    }

    /// <summary>
    /// Belirtilen adımı yürütür
    /// </summary>
    [HttpPost("execute-step")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ExecuteStep([FromBody] BendingStepRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var step = new BendingStepDto
            {
                StepNumber = request.StepNumber,
                LeftPosition = request.LeftPosition,
                RightPosition = request.RightPosition,
                TopPosition = request.TopPosition,
                BottomPosition = request.BottomPosition,
                SidePistons = request.SidePistons?.Select(sp => new SidePistonPositionDto
                {
                    PistonNumber = sp.PistonNumber,
                    Position = sp.Position
                }).ToList() ?? new List<SidePistonPositionDto>()
            };

            var result = await _bendingService.ExecuteBendingStepAsync(step);

            return Ok(new
            {
                success = result,
                message = result ? $"Adım {request.StepNumber} başarıyla yürütüldü" : $"Adım {request.StepNumber} yürütülemedi",
                step = step,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm adımı {StepNumber} yürütülürken hata oluştu", request.StepNumber);
            return StatusCode(500, new { Error = ex.Message, Operation = "Execute bending step" });
        }
    }

    /// <summary>
    /// Büküm işlemini durdurur
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopBending()
    {
        try
        {
            var result = await _bendingService.StopBendingAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Büküm işlemi durduruldu" : "Büküm işlemi durdurulamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm işlemi durdurulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop bending" });
        }
    }



    /// <summary>
    /// Büküm parametrelerini hesaplar
    /// </summary>
    [HttpPost("calculate-bending")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> CalculateBending([FromBody] BendingParameters parameters)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Controller'daki BendingParameters'ı Application DTO'ya çevir
            var applicationParameters = new BendingMachine.Application.DTOs.BendingParameters
            {
                TopBallInnerDiameter = parameters.TopBallInnerDiameter,
                BottomBallDiameter = parameters.BottomBallDiameter,
                SideBallDiameter = parameters.SideBallDiameter,
                BendingRadius = parameters.BendingRadius,
                ProfileHeight = parameters.ProfileHeight,
                ProfileLength = parameters.ProfileLength,
                ProfileThickness = parameters.ProfileThickness,
                TriangleWidth = parameters.TriangleWidth,
                TriangleAngle = parameters.TriangleAngle,
                StageValue = parameters.StageValue,
                StepSize = parameters.StepSize,
                TargetPressure = parameters.TargetPressure,
                PressureTolerance = parameters.PressureTolerance,
                MaterialType = parameters.MaterialType,
                ProfileType = parameters.ProfileType
            };

            var result = await _calculatorService.CalculateAsync(applicationParameters);

            return Ok(new
            {
                success = true,
                data = result,
                message = "Büküm hesaplaması başarıyla tamamlandı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm hesaplama sırasında hata oluştu");
            return Ok(new 
            { 
                success = false, 
                message = $"Hesaplama hatası: {ex.Message}",
                error = ex.Message,
                operation = "Calculate bending",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Parça sıkıştırma işlemi
    /// </summary>
    [HttpPost("compress-part")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> CompressPartWithPressure([FromBody] BendingCompressPartRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _bendingService.CompressProfileAsync(request.TargetPressure, request.PressureTolerance);

            // ✅ DÜZELTME: Gerçek basınç değerlerini de döndür (makine-calisma.md gereksinimi)
            var (s1Pressure, s2Pressure) = await _machineService.ReadActualPressureAsync();

            return Ok(new
            {
                success = result,
                message = result ? $"Parça {request.TargetPressure} bar basınçta sıkıştırıldı" 
                                : "Parça sıkıştırma başarısız",
                targetPressure = request.TargetPressure,
                tolerance = request.PressureTolerance,
                actualPressure = new
                {
                    s1Pressure = Math.Round(s1Pressure, 2),
                    s2Pressure = Math.Round(s2Pressure, 2),
                    maxPressure = Math.Round(Math.Max(s1Pressure, s2Pressure), 2)
                },
                requirements = new
                {
                    leftPartSensor = "Sol parça varlığı sensörü gerekli",
                    startupPressureBypass = "550ms kalkış basıncı görmezden gelinir",
                    pressureControl = "S1/S2 sensörlerinden gerçek basınç kontrolü"
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça sıkıştırma sırasında hata oluştu");
            return Ok(new 
            { 
                success = false, 
                message = $"Parça sıkıştırma hatası: {ex.Message}",
                error = ex.Message,
                operation = "Compress part",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Parça sıfırlama işlemi - Hassas konumlandırma ile
    /// </summary>
    [HttpPost("reset-part")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ResetPart([FromBody] BendingResetPartRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _bendingService.ResetProfilePositionAsync(request.ResetDistance);

            return Ok(new
            {
                success = result, // ✅ DÜZELTME: lowercase field standardı
                message = result ? $"Parça pozisyonu {request.ResetDistance}mm mesafe ile sıfırlandı (ULTRA HASSAS konumlandırma)" 
                                : "Parça sıfırlama başarısız",
                ResetDistance = request.ResetDistance,
                Algorithm = new
                {
                    Name = "5-aşamalı ultra hassas konumlandırma",
                    Phases = new[]
                    {
                        "Faz 1: Sol/Sağ parça sensöründen başlangıç yön belirleme (50ms okuma)",
                        "Faz 2: Normal hızda sensör görene/görmeyene kadar hareket (3x üst üste kontrol)",
                        "Faz 3: Orta hızda kaba konumlandırma (20% hız)",
                        "Faz 4: Hassas konumlandırma (10% hız, 5x üst üste kontrol)",
                        "Faz 5: Ultra hassas konumlandırma (5% hız, 8x üst üste kontrol)",
                        "Faz 6: Alt top merkezine çekilme (resetDistance kadar hassas encoder rotasyon)"
                    },
                    EncoderFormula = "mesafe = (encoderRaw * Math.PI * 220.0) / 1024.0",
                    NewFeatures = new[]
                    {
                        "Encoder okuma sıklığı: 20ms (50ms→20ms)",
                        "Encoder toleransı: 1.0mm (3.0mm→1.0mm)",
                        "6 kademeli hız kontrolü: 60%→40%→25%→15%→8%→4%",
                        "Encoder drift kontrolü: 0.5mm tolerans",
                        "Son yaklaşma kontrolü: 5mm'de özel kontrol",
                        "Sensör okuma sıklığı: 50ms (100ms→50ms)",
                        "Ultra hassas faz: 30ms okuma, 8x üst üste kontrol"
                    },
                    SafetyControls = new[]
                    {
                        "Encoder freeze timeout: 20 pulse (15→20)",
                        "Max encoder stuck count: 20 (15→20)",
                        "Encoder değişim eşiği: 2 pulse (1→2)",
                        "İlerleme kontrolü: 0.1mm hassasiyet (0.5→0.1)",
                        "Son yaklaşma timeout: 30 saniye",
                        "Drift kontrolü: 0.5mm tolerans"
                    }
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parça sıfırlama sırasında hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Reset part position" });
        }
    }

    /// <summary>
    /// Cetvel sıfırlama işlemi
    /// </summary>
    [HttpPost("reset-rulers")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ResetRulers()
    {
        try
        {
            var result = await _bendingService.ResetRulersAsync();

            // ✅ DÜZELTME: makine-calisma.md'ye göre reset durumu kontrolü
            var rulerStatus = await _machineService.GetRulerStatusAsync();

            return Ok(new
            {
                Success = result,
                Message = result ? "Cetveller başarıyla sıfırlandı" : "Cetvel sıfırlama başarısız",
                Process = new
                {
                    Steps = "9 adımlı süreç: Sistemler geri çekme → Reset protokolü → Gönye pozisyonları → Final reset",
                    PressureControl = "S1 VE S2 her ikisi de hedef basınca ulaşmalı",
                    ResetAddresses = "4 adet reset adresi (M13toM16, M17toM20, PneumaticValve, Rotation)"
                },
                ResetStatus = new
                {
                    M13toM16 = rulerStatus.RulerResetM13toM16,
                    M17toM20 = rulerStatus.RulerResetM17toM20,
                    PneumaticValve = rulerStatus.RulerResetPneumaticValve,
                    Rotation = rulerStatus.RulerResetRotation,
                    AllReset = rulerStatus.AllReset
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetvel sıfırlama sırasında hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Reset rulers" });
        }
    }

    /// <summary>
    /// Cetvel durumlarını kontrol et - Reset adreslerinin 2570 olup olmadığını kontrol eder
    /// </summary>
    [HttpGet("ruler-status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetRulerStatus()
    {
        try
        {
            // Gerçek modbus okuma - MachineService üzerinden
            var rulerStatus = await _machineService.GetRulerStatusAsync();

            return Ok(new
            {
                success = true,
                data = rulerStatus,
                message = rulerStatus.AllReset ? "Tüm cetveller sıfırlanmış" : "Bazı cetveller sıfırlama gerektirir",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cetvel durumu kontrolü sırasında hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get ruler status" });
        }
    }

    /// <summary>
    /// Stage ayarlama işlemi - Büküm sürecinin parçası olarak BendingService üzerinden
    /// </summary>
    [HttpPost("set-stage")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> SetStage([FromBody] SetStageRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // ✅ DÜZELTME: makine-calisma.md'ye göre stage değeri kontrolü
            var validStages = new[] { 0, 60, 120 }; // Default stage değerleri
            var isKnownStage = validStages.Contains(request.StageValue);
            
            if (!isKnownStage)
            {
                _logger.LogWarning("Bilinmeyen stage değeri: {StageValue}mm - Dinamik hesaplama kullanılacak", request.StageValue);
            }

            // ✅ DÜZELTME: Büküm operasyonları için BendingService kullanılmalı
            var result = await _bendingService.SetStageAsync(request.StageValue);

            return Ok(new
            {
                success = result,
                message = result ? $"Stage {request.StageValue}mm ayarlandı" : "Stage ayarlama başarısız",
                StageValue = request.StageValue,
                StageType = isKnownStage ? "Predefined" : "Dynamic",
                StagePositions = isKnownStage ? GetStagePositions(request.StageValue) : "Dinamik hesaplama",
                Algorithm = "3-adımlı: Cetvel sıfırlama → Pozisyonlama → Cetvel değer sıfırlama",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage ayarlama sırasında hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Set stage" });
        }
    }

    // ✅ HELPER METHOD: makine-calisma.md'ye göre stage pozisyonları - DÜZELTME
    private object GetStagePositions(int stageValue)
    {
        return stageValue switch
        {
            0 => new { BottomCenter = 10.5, BottomLeft = 3.75, BottomRight = 0.0 }, // Gönye pozisyonu
            60 => new { BottomCenter = 60.0, BottomLeft = 67.34, BottomRight = 67.34 },
            120 => new { BottomCenter = 120.0, BottomLeft = 134.68, BottomRight = 134.68 },
            _ => new { BottomCenter = stageValue, BottomLeft = stageValue * 1.1223, BottomRight = stageValue * 1.1223 }
        };
    }

    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut stage'leri listeler
    /// </summary>
    [HttpGet("stages")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetAvailableStages()
    {
        try
        {
            var stages = await _machineService.GetAvailableStagesAsync();
            var currentStage = await _machineService.GetCurrentStageAsync();

            // Mevcut aktif stage'i işaretle
            foreach (var stage in stages)
            {
                stage.IsActive = Math.Abs(stage.Value - currentStage) < 0.1;
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    Stages = stages,
                    CurrentStage = currentStage,
                    TotalStages = stages.Count
                },
                message = $"{stages.Count} stage konfigürasyonu okundu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage listesi okunurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get available stages" });
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Belirli bir stage bilgisini döndürür
    /// </summary>
    [HttpGet("stages/{stageValue}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetStageConfig(int stageValue)
    {
        try
        {
            var stageConfig = await _machineService.GetStageConfigAsync(stageValue);
            
            if (stageConfig == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Stage {stageValue}mm konfigürasyonu bulunamadı",
                    StageValue = stageValue,
                    Timestamp = DateTime.UtcNow
                });
            }

            return Ok(new
            {
                success = true,
                data = stageConfig,
                message = $"Stage {stageValue}mm konfigürasyonu okundu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stage konfigürasyonu okunurken hata oluştu - Stage: {Stage}", stageValue);
            return StatusCode(500, new { Error = ex.Message, Operation = "Get stage config" });
        }
    }

    /// <summary>
    /// Gerçek basınç sensörlerinden (S1/S2) anlık basınç okur
    /// </summary>
    [HttpGet("pressure/real-time")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetRealTimePressure()
    {
        try
        {
            var (s1Pressure, s2Pressure) = await _machineService.ReadActualPressureAsync();
            var maxPressure = Math.Max(s1Pressure, s2Pressure);

            return Ok(new
            {
                success = true,
                data = new
                {
                    s1Pressure = Math.Round(s1Pressure, 2),
                    s2Pressure = Math.Round(s2Pressure, 2),
                    maxPressure = Math.Round(maxPressure, 2),
                    pressureUnit = "bar",
                    sensorRange = "0-400 bar",
                    conversionMethod = "4-20mA to bar"
                },
                message = "Gerçek basınç değerleri okundu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gerçek basınç değerleri okunurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Read real-time pressure" });
        }
    }

    /// <summary>
    /// Encoder durumu ve pozisyon bilgilerini getirir
    /// </summary>
    [HttpGet("encoder/status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetEncoderStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    currentPosition = status.RotationEncoderRaw,
                    encoderType = "RV3100",
                    pulsesPerRevolution = 1024,
                    currentDistance = Math.Round((status.RotationEncoderRaw * Math.PI * 220.0) / 1024.0, 2), // mm
                    isHealthy = true, // TODO: Encoder sağlık kontrolü eklenebilir
                    lastUpdateTime = DateTime.UtcNow
                },
                message = "Encoder durumu okundu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encoder durumu okunurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get encoder status" });
        }
    }

    /// <summary>
    /// Rotasyon motorunu hassas hız kontrolü ile başlatır
    /// </summary>
    [HttpPost("rotation/start-precise")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StartPreciseRotation([FromBody] PreciseRotationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _machineService.StartRotationAsync(request.Direction, request.Speed);

            return Ok(new
            {
                success = result,
                message = result ? $"Hassas rotasyon başlatıldı - {request.Direction} yönde {request.Speed}% hızında" 
                                : "Hassas rotasyon başlatılamadı",
                direction = request.Direction,
                speed = request.Speed,
                mode = "Hassas Konumlandırma",
                encoderMonitoring = "Aktif",
                safetyChecks = "Encoder donması kontrolü aktif",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hassas rotasyon başlatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Start precise rotation" });
        }
    }

    /// <summary>
    /// Makine konfigürasyonunu getirir (RegisterCount, BallDiameter vb.)
    /// </summary>
    [HttpGet("configuration")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public IActionResult GetBendingConfiguration()
    {
        try
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    pistonRegisterCounts = new
                    {
                        topPiston = 7973,
                        bottomPiston = 9742,
                        leftPiston = 21082,
                        rightPiston = 21123,
                        leftReel = 17597,
                        leftBody = 6447,
                        leftJoin = 9350,
                        rightReel = 17576,
                        rightBody = 6439,
                        rightJoin = 9322
                    },
                    machineParameters = new
                    {
                        ballDiameter = 220.0, // mm
                        encoderPulsesPerRevolution = 1024,
                        pressureRange = "0-400 bar",
                        maxPistonVoltage = "±10V",
                        rotationSpeedRange = "0-100%"
                    },
                    precisionControl = new
                    {
                        fastSpeed = 70.0,   // %70 - İlk %80 mesafe
                        mediumSpeed = 40.0, // %40 - %80-95 mesafe
                        slowSpeed = 15.0,   // %15 - Son %5 mesafe
                        preciseSpeed = 20.0 // %20 - Hassas konumlandırma
                    }
                },
                message = "Büküm konfigürasyonu okundu",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Büküm konfigürasyonu okunurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get bending configuration" });
        }
    }

    /// <summary>
    /// PASO TEST - Sadece paso işlemini test eder
    /// Cetvel sıfırlama, stage ayarlama, parça sıkıştırma ve sıfırlama önceden yapılmış olmalı
    /// </summary>
    [HttpPost("test-paso")]
    public async Task<IActionResult> TestPaso([FromBody] PasoTestRequest request)
    {
        try
        {
            _logger.LogInformation("🧪 Paso test isteği alındı - Distance: {Distance}mm, Length: {Length}mm, Step: {Step}mm", 
                request.SideBallTravelDistance, request.ProfileLength, request.StepSize);

            // Parametre validasyonu
            if (request.SideBallTravelDistance <= 0 || request.SideBallTravelDistance > 200)
            {
                return BadRequest(new { success = false, message = "SideBallTravelDistance 0-200mm arasında olmalı" });
            }

            if (request.ProfileLength <= 0 || request.ProfileLength > 10000)
            {
                return BadRequest(new { success = false, message = "ProfileLength 0-10000mm arasında olmalı" });
            }

            if (request.StepSize <= 0 || request.StepSize > 100)
            {
                return BadRequest(new { success = false, message = "StepSize 0-100mm arasında olmalı" });
            }

            // Paso test çalıştır
            var result = await _bendingService.ExecutePasoTestAsync(
                request.SideBallTravelDistance, 
                request.ProfileLength, 
                request.StepSize, 
                request.EvacuationTimeSeconds);

            if (result)
            {
                _logger.LogInformation("✅ Paso test başarılı");
                return Ok(new 
                { 
                    success = true, 
                    message = "Paso test başarıyla tamamlandı (HASSAS ENCODER ROTASYON)",
                    data = new 
                    {
                        totalSteps = (int)Math.Ceiling(request.SideBallTravelDistance / request.StepSize),
                        totalLeftDistance = request.SideBallTravelDistance,
                        totalRightDistance = request.SideBallTravelDistance,
                        activeSensor = "Sol sensör (default)",
                        firstBendingSide = "Sağ (karşı) taraf",
                        initialReverseDistance = request.ProfileLength,
                        rotationDistance = request.ProfileLength,
                        rotationAlgorithm = new
                        {
                            name = "Hassas Encoder Bazlı Rotasyon",
                            encoderReadingFrequency = "20ms",
                            encoderTolerance = "1.0mm",
                            speedStages = "60%→40%→25%→15%→8%→4%",
                            timeout = "150 saniye",
                            driftControl = "0.5mm tolerans",
                            finalApproach = "5mm'de özel kontrol"
                        }
                    }
                });
            }
            else
            {
                _logger.LogWarning("⚠️ Paso test başarısız");
                return BadRequest(new 
                { 
                    success = false, 
                    message = "Paso test başarısız - Loglarda detay bilgi mevcuttur" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Paso test sırasında hata oluştu");
            return StatusCode(500, new 
            { 
                success = false, 
                message = $"Paso test hatası: {ex.Message}" 
            });
        }
    }
}

#region Request Models

public class BendingCalculationRequest
{
    [Required]
    [Range(0.1, 10000, ErrorMessage = "Profile length must be between 0.1 and 10000mm")]
    public double ProfileLength { get; set; }

    [Required]
    [Range(1, 180, ErrorMessage = "Bending angle must be between 1 and 180 degrees")]
    public double BendingAngle { get; set; }

    [Required]
    [Range(1, 1000, ErrorMessage = "Bending radius must be between 1 and 1000mm")]
    public double BendingRadius { get; set; }

    [Required]
    public string ProfileType { get; set; } = string.Empty;
}

public class BendingParametersRequest
{
    [Required]
    [Range(0.1, 10000, ErrorMessage = "Profile length must be between 0.1 and 10000mm")]
    public double ProfileLength { get; set; }

    [Required]
    [Range(1, 180, ErrorMessage = "Bending angle must be between 1 and 180 degrees")]
    public double BendingAngle { get; set; }

    [Required]
    [Range(1, 1000, ErrorMessage = "Bending radius must be between 1 and 1000mm")]
    public double BendingRadius { get; set; }

    [Required]
    public string ProfileType { get; set; } = string.Empty;

    public string Material { get; set; } = string.Empty;

    [Range(0.1, 50, ErrorMessage = "Thickness must be between 0.1 and 50mm")]
    public double Thickness { get; set; }
}

public class AutoBendingRequest
{
    [Required]
    [Range(1, 1000, ErrorMessage = "Side ball travel distance must be between 1 and 1000mm")]
    public double SideBallTravelDistance { get; set; } = 110; // Yan topların gideceği konum

    [Required]
    [Range(0, 500, ErrorMessage = "Stage number must be between 0 and 500mm")]
    public int StageNumber { get; set; } = 60; // Stage numarası (0, 60, 120)

    [Required]
    [Range(10, 10000, ErrorMessage = "Profile length must be between 10 and 10000mm")]
    public double ProfileLength { get; set; } = 2000; // Profil uzunluğu

    [Required]
    [Range(1, 500, ErrorMessage = "Profile height must be between 1 and 500mm")]
    public double ProfileHeight { get; set; } = 80; // Profil yüksekliği

    [Required]
    [Range(10, 2000, ErrorMessage = "Profile reset distance must be between 10 and 2000mm")]
    public double ProfileResetDistance { get; set; } = 670; // Parça sıfırlama mesafesi

    [Required]
    [Range(1, 100, ErrorMessage = "Step size must be between 1 and 100mm")]
    public double StepSize { get; set; } = 20; // Adım büyüklüğü

    [Required]
    [Range(1, 400, ErrorMessage = "Target pressure must be between 1 and 400 bar")]
    public double TargetPressure { get; set; } = 70; // Hedef basınç

    [Required]
    [Range(5, 300, ErrorMessage = "Evacuation time must be between 5 and 300 seconds")]
    public int EvacuationTimeSeconds { get; set; } = 60; // Tahliye süresi
}

public class BendingStepRequest
{
    [Required]
    [Range(1, 100, ErrorMessage = "Step number must be between 1 and 100")]
    public int StepNumber { get; set; }

    [Range(0, 1000, ErrorMessage = "Left position must be between 0 and 1000mm")]
    public double LeftPosition { get; set; }

    [Range(0, 1000, ErrorMessage = "Right position must be between 0 and 1000mm")]
    public double RightPosition { get; set; }

    [Range(0, 1000, ErrorMessage = "Top position must be between 0 and 1000mm")]
    public double TopPosition { get; set; }

    [Range(0, 1000, ErrorMessage = "Bottom position must be between 0 and 1000mm")]
    public double BottomPosition { get; set; }

    public List<SidePistonPositionRequest>? SidePistons { get; set; }
}

public class SidePistonPositionRequest
{
    [Required]
    [Range(1, 6, ErrorMessage = "Piston number must be between 1 and 6")]
    public int PistonNumber { get; set; }

    [Range(0, 1000, ErrorMessage = "Position must be between 0 and 1000mm")]
    public double Position { get; set; }
}

public class SetStageRequest
{
    [Required]
    [Range(0, 500, ErrorMessage = "Stage value must be between 0 and 500mm")]
    public int StageValue { get; set; }
    
    // ✅ DÜZELTME: makine-calisma.md'ye göre stage validasyonu
    // Default: 0mm, 60mm, 120mm stage'leri desteklenir
    // Diğer değerler dinamik hesaplama ile desteklenir
}

public class PreciseRotationRequest
{
    [Required]
    public string Direction { get; set; } = string.Empty; // "Clockwise" veya "CounterClockwise"
    
    [Required]
    [Range(1.0, 100.0, ErrorMessage = "Speed must be between 1% and 100%")]
    public double Speed { get; set; }
}

public class BendingCompressPartRequest
{
    [Required]
    [Range(1, 400, ErrorMessage = "Target pressure must be between 1 and 400 bar")]
    public double TargetPressure { get; set; } = 50;
    
    [Required] 
    [Range(0.1, 50, ErrorMessage = "Pressure tolerance must be between 0.1 and 50 bar")]
    public double PressureTolerance { get; set; } = 5;
    
    // ✅ DÜZELTME: TargetPosition kaldırıldı - makine-calisma.md'ye göre sadece basınç kontrolü yapılıyor
    // Parça sıkıştırma: Sol parça sensörü + hedef basınç + 550ms kalkış basıncı bypass
}

public class BendingResetPartRequest
{
    [Required]
    [Range(10, 2000, ErrorMessage = "Reset distance must be between 10 and 2000mm")] // ✅ DÜZELTME: makine-calisma.md'ye göre max 2000mm
    public double ResetDistance { get; set; } = 100; // mm - parça varlık sensöründen alt top merkezine mesafe
    
    // ✅ VALIDATION: makine-calisma.md'ye göre encoder bazlı hesaplama
    // Encoder formülü: mesafe = (encoderRaw * Math.PI * 220.0) / 1024.0
}

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
    
    // Malzeme Özelikleri
    public string MaterialType { get; set; } = "Aluminum";
    public string ProfileType { get; set; } = "Rectangular";
}

#endregion 