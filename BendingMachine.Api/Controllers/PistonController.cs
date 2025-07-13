using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;
using BendingMachine.Application.DTOs;
using System.ComponentModel.DataAnnotations;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Piston kontrol endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Piston Control")]
public class PistonController : ControllerBase
{
    private readonly IPistonService _pistonService;
    private readonly IMachineService _machineService;
    private readonly ILogger<PistonController> _logger;

    public PistonController(
        IPistonService pistonService, 
        IMachineService machineService,
        ILogger<PistonController> logger)
    {
        _pistonService = pistonService;
        _machineService = machineService;
        _logger = logger;
    }

    /// <summary>
    /// Belirtilen pistonu hareket ettirir
    /// </summary>
    [HttpPost("{pistonType}/move")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> MovePiston(
        [FromRoute] string pistonType,
        [FromBody] PistonMoveRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var moveRequest = new PistonMoveRequestDto
            {
                PistonType = pistonType,
                Voltage = request.Voltage
            };

            var result = await _pistonService.MovePistonAsync(moveRequest);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} piston hareket ettirildi" : "Piston hareket ettirilemedi",
                pistonType = pistonType,
                voltage = request.Voltage,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston hareket ettirilirken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Move piston" });
        }
    }

    /// <summary>
    /// Belirtilen pistonu pozisyona hareket ettirir
    /// </summary>
    [HttpPost("{pistonType}/move-to-position")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> MovePistonToPosition(
        [FromRoute] string pistonType,
        [FromBody] MovePistonToPositionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var positionRequest = new PistonPositionRequestDto
            {
                PistonType = pistonType,
                TargetPosition = request.TargetPosition,
                Speed = request.Speed ?? 5.0
            };

            var result = await _pistonService.MovePistonToPositionAsync(positionRequest);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} pozisyona hareket ettirildi" : "Piston pozisyona hareket ettirilemedi",
                pistonType = pistonType,
                targetPosition = request.TargetPosition,
                speed = positionRequest.Speed,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston pozisyona hareket ettirilirken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Move piston to position" });
        }
    }

    /// <summary>
    /// Belirtilen pistonu jog hareket ettirir
    /// </summary>
    [HttpPost("{pistonType}/jog")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> JogPiston(
        [FromRoute] string pistonType,
        [FromBody] JogPistonRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var jogRequest = new PistonJogRequestDto
            {
                PistonType = pistonType,
                Direction = request.Direction,
                Voltage = request.Voltage
            };

            var result = await _pistonService.JogPistonAsync(jogRequest);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} jog hareket ettirildi" : "Piston jog hareket ettirilemedi",
                pistonType = pistonType,
                direction = request.Direction,
                voltage = request.Voltage,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston jog hareket ettirilirken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Jog piston" });
        }
    }

    /// <summary>
    /// Belirtilen pistonu durdurur
    /// </summary>
    [HttpPost("{pistonType}/stop")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopPiston([FromRoute] string pistonType)
    {
        try
        {
            var result = await _pistonService.StopPistonAsync(pistonType);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} piston durduruldu" : "Piston durdurulamadı",
                pistonType = pistonType,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston durdurulurken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop piston" });
        }
    }

    /// <summary>
    /// Tüm pistonları durdurur
    /// </summary>
    [HttpPost("stop-all")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopAllPistons()
    {
        try
        {
            var result = await _pistonService.StopAllPistonsAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Tüm pistonlar durduruldu" : "Bazı pistonlar durdurulamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm pistonlar durdurulurken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop all pistons" });
        }
    }

    /// <summary>
    /// Belirtilen pistonun durumunu getirir
    /// </summary>
    [HttpGet("{pistonType}/status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> GetPistonStatus([FromRoute] string pistonType)
    {
        try
        {
            var pistonStatus = await _pistonService.GetPistonAsync(pistonType);

            return Ok(new
            {
                success = true,
                data = pistonStatus,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} piston durumu alınırken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Get piston status" });
        }
    }

    /// <summary>
    /// Tüm pistonların durumunu getirir
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetAllPistonsStatus()
    {
        try
        {
            var pistonsStatus = await _pistonService.GetAllPistonsAsync();

            return Ok(new
            {
                success = true,
                data = pistonsStatus,
                count = pistonsStatus.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm pistonların durumu alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get all pistons status" });
        }
    }

    #region Side Support Piston Endpoints

    /// <summary>
    /// Yan dayama pistonu jog hareket ettirir (Valve + Direction control)
    /// </summary>
    [HttpPost("{pistonType}/jog-side-support")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> JogSideSupportPiston(
        [FromRoute] string pistonType,
        [FromBody] SideSupportJogRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _machineService.JogSideSupportPistonAsync(pistonType, request.Direction);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} yan dayama hareket ettirildi - {request.Direction}" 
                                : "Yan dayama hareket ettirilemedi",
                pistonType = pistonType,
                direction = request.Direction,
                controlMethod = "Valve + Direction Coils",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} yan dayama hareket ettirilirken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Jog side support piston" });
        }
    }

    /// <summary>
    /// Yan dayama pistonu durdurur (Direction coils + Valve close)
    /// </summary>
    [HttpPost("{pistonType}/stop-side-support")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> StopSideSupportPiston([FromRoute] string pistonType)
    {
        try
        {
            var result = await _machineService.StopSideSupportPistonAsync(pistonType);

            return Ok(new
            {
                success = result,
                message = result ? $"{pistonType} yan dayama durduruldu" : "Yan dayama durdurulamadı",
                pistonType = pistonType,
                controlMethod = "Direction Coils OFF + Valve Close",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PistonType} yan dayama durdurulurken hata oluştu", pistonType);
            return StatusCode(500, new { Error = ex.Message, Operation = "Stop side support piston" });
        }
    }

    #endregion
}

#region Request Models

public class PistonMoveRequest
{
    [Required]
    [Range(-10.0, 10.0, ErrorMessage = "Voltage must be between -10V and +10V")]
    public double Voltage { get; set; }
}

public class MovePistonToPositionRequest
{
    [Required]
    [Range(0.0, double.MaxValue, ErrorMessage = "Target position must be greater than or equal to 0")]
    public double TargetPosition { get; set; }

    [Range(0.1, 10.0, ErrorMessage = "Speed must be between 0.1 and 10")]
    public double? Speed { get; set; }
}

public class JogPistonRequest
{
    [Required]
    public string Direction { get; set; } = string.Empty;

    [Required]
    [Range(0.1, 10.0, ErrorMessage = "Voltage must be between 0.1 and 10")]
    public double Voltage { get; set; }
}

public class SideSupportJogRequest
{
    [Required]
    public string Direction { get; set; } = string.Empty; // "forward" or "backward"
}

#endregion 