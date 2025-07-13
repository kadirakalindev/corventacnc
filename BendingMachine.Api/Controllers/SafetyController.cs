using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Güvenlik kontrol endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Safety Control")]
public class SafetyController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(IMachineService machineService, ILogger<SafetyController> logger)
    {
        _machineService = machineService;
        _logger = logger;
    }

    /// <summary>
    /// Genel güvenlik durumunu getirir
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetSafetyStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();
            var isSafe = status.IsSafeToOperate();

            return Ok(new
            {
                success = true,
                data = new
                {
                    isSafe = isSafe,
                    emergencyStop = status.EmergencyStop,
                    hydraulicThermalError = status.HydraulicThermalError,
                    fanThermalError = status.FanThermalError,
                    phaseSequenceError = status.PhaseSequenceError,
                    alarmActive = status.AlarmActive,
                    overallSafetyStatus = isSafe ? "Güvenli" : "Güvensiz"
                },
                message = "Güvenlik durumu alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güvenlik durumu alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get safety status" });
        }
    }

    /// <summary>
    /// Acil durdurma yapar
    /// </summary>
    [HttpPost("emergency-stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> EmergencyStop()
    {
        try
        {
            var result = await _machineService.EmergencyStopAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Acil durdurma gerçekleştirildi" : "Acil durdurma başarısız",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acil durdurma sırasında hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Emergency stop" });
        }
    }

    /// <summary>
    /// Alarm durumunu sıfırlar
    /// </summary>
    [HttpPost("reset-alarm")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ResetAlarm()
    {
        try
        {
            var result = await _machineService.ResetAlarmAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Alarm sıfırlandı" : "Alarm sıfırlanamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alarm sıfırlanırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Reset alarm" });
        }
    }

    /// <summary>
    /// Güvenlik kontrolleri yapıp makine durumunu güvenli hale getirir
    /// </summary>
    [HttpPost("secure-machine")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> SecureMachine()
    {
        try
        {
            // Önce güvenlik durumunu kontrol et
            var status = await _machineService.GetMachineStatusAsync();
            var isSafe = status.IsSafeToOperate();

            if (!isSafe)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Güvenlik sistemi aktif değil - makine güvenli hale getirilemez",
                    SafetyIssues = new
                    {
                        EmergencyStop = status.EmergencyStop,
                        HydraulicThermalError = status.HydraulicThermalError,
                        FanThermalError = status.FanThermalError,
                        PhaseSequenceError = status.PhaseSequenceError,
                        AlarmActive = status.AlarmActive
                    },
                    Timestamp = DateTime.UtcNow
                });
            }

            // Güvenlik protokolünü uygula:
            // 1. Tüm pistonları durdur
            var stopAllResult = await _machineService.EmergencyStopAsync();
            
            // 2. Alarmı sıfırla
            var resetAlarmResult = await _machineService.ResetAlarmAsync();

            var allOperationsSuccessful = stopAllResult && resetAlarmResult;

            return Ok(new
            {
                success = allOperationsSuccessful,
                message = allOperationsSuccessful ? "Makine güvenli hale getirildi" : "Bazı güvenlik işlemleri başarısız",
                Operations = new
                {
                    EmergencyStop = stopAllResult,
                    AlarmReset = resetAlarmResult
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine güvenli hale getirilirken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Secure machine" });
        }
    }

    /// <summary>
    /// Güvenlik parametrelerini getirir
    /// </summary>
    [HttpGet("parameters")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public IActionResult GetSafetyParameters()
    {
        try
        {
            return Ok(new
            {
                Success = true,
                Data = new
                {
                    MaxOperatingPressure = "400 bar",
                    MinOperatingPressure = "10 bar",
                    MaxPistonVoltage = "10V",
                    MinPistonVoltage = "-10V",
                    SafetyCheckInterval = "100ms",
                    EmergencyStopTimeout = "500ms",
                    MaxOperationTime = "30 dakika"
                },
                message = "Güvenlik parametreleri alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güvenlik parametreleri alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get safety parameters" });
        }
    }

    /// <summary>
    /// Kirlilik sensörlerinin durumunu kontrol eder
    /// </summary>
    [HttpGet("pollution-sensors")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetPollutionSensors()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    PollutionSensor1 = status.PollutionSensor1,
                    PollutionSensor2 = status.PollutionSensor2,
                    PollutionSensor3 = status.PollutionSensor3,
                    AnyPollutionDetected = status.PollutionSensor1 || status.PollutionSensor2 || status.PollutionSensor3
                },
                message = "Kirlilik sensörü durumu alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kirlilik sensörü durumu alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get pollution sensors" });
        }
    }

    /// <summary>
    /// Termal hata durumlarını kontrol eder
    /// </summary>
    [HttpGet("thermal-status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetThermalStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    HydraulicThermalError = status.HydraulicThermalError,
                    FanThermalError = status.FanThermalError,
                    AnyThermalError = status.HydraulicThermalError || status.FanThermalError,
                    OilTemperature = status.OilTemperature,
                    IsOilSystemHealthy = status.IsOilSystemHealthy()
                },
                message = "Termal durum alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Termal durum alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get thermal status" });
        }
    }
} 