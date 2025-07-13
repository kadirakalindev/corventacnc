using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Motor kontrol endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Motor Control")]
public class MotorController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly ILogger<MotorController> _logger;

    public MotorController(IMachineService machineService, ILogger<MotorController> logger)
    {
        _machineService = machineService;
        _logger = logger;
    }

    /// <summary>
    /// Hidrolik motoru başlatır
    /// </summary>
    [HttpPost("hydraulic/start")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StartHydraulicMotor()
    {
        try
        {
            var result = await _machineService.StartHydraulicMotorAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Hidrolik motor başlatıldı" : "Hidrolik motor başlatılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hidrolik motor başlatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Start hydraulic motor" });
        }
    }

    /// <summary>
    /// Hidrolik motoru durdurur
    /// </summary>
    [HttpPost("hydraulic/stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopHydraulicMotor()
    {
        try
        {
            var result = await _machineService.StopHydraulicMotorAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Hidrolik motor durduruldu" : "Hidrolik motor durdurulamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hidrolik motor durdurulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop hydraulic motor" });
        }
    }

    /// <summary>
    /// Fan motoru başlatır
    /// </summary>
    [HttpPost("fan/start")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StartFanMotor()
    {
        try
        {
            var result = await _machineService.StartFanMotorAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Fan motor başlatıldı" : "Fan motor başlatılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fan motor başlatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Start fan motor" });
        }
    }

    /// <summary>
    /// Fan motoru durdurur
    /// </summary>
    [HttpPost("fan/stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopFanMotor()
    {
        try
        {
            var result = await _machineService.StopFanMotorAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Fan motor durduruldu" : "Fan motor durdurulamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fan motor durdurulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop fan motor" });
        }
    }

    /// <summary>
    /// Rotasyon motorunu başlatır
    /// </summary>
    [HttpPost("rotation/start")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StartRotationMotor([FromBody] RotationMotorRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _machineService.StartRotationAsync(request.Direction, request.Speed);

            return Ok(new
            {
                success = result,
                message = result ? $"Rotasyon motoru başlatıldı - {request.Direction} yönde {request.Speed} hızında" 
                                : "Rotasyon motoru başlatılamadı",
                direction = request.Direction,
                speed = request.Speed,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon motoru başlatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Start rotation motor" });
        }
    }

    /// <summary>
    /// Rotasyon motorunu durdurur
    /// </summary>
    [HttpPost("rotation/stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopRotationMotor()
    {
        try
        {
            var result = await _machineService.StopRotationAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Rotasyon motoru durduruldu" : "Rotasyon motoru durdurulamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon motoru durdurulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop rotation motor" });
        }
    }

    /// <summary>
    /// Rotasyon hızını ayarlar
    /// </summary>
    [HttpPost("rotation/set-speed")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> SetRotationSpeed([FromBody] SetRotationSpeedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _machineService.SetRotationSpeedAsync(request.Speed);

            return Ok(new
            {
                success = result,
                message = result ? $"Rotasyon hızı {request.Speed} olarak ayarlandı" : "Rotasyon hızı ayarlanamadı",
                speed = request.Speed,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotasyon hızı ayarlanırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Set rotation speed" });
        }
    }

    /// <summary>
    /// Tüm motorların durumunu getirir
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetMotorStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    hydraulicMotor = status.HydraulicMotorRunning,
                    fanMotor = status.FanMotorRunning,
                    rotationMotor = status.RotationMotorRunning
                },
                message = "Motor durumları alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Motor durumları alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get motor status" });
        }
    }
}

#region Request Models

public class RotationMotorRequest
{
    [Required]
    public string Direction { get; set; } = string.Empty;

    [Required]
    [Range(0.1, 100.0, ErrorMessage = "Speed must be between 0.1 and 100")]
    public double Speed { get; set; }
}

public class SetRotationSpeedRequest
{
    [Required]
    [Range(0.1, 100.0, ErrorMessage = "Speed must be between 0.1 and 100")]
    public double Speed { get; set; }
}

#endregion 