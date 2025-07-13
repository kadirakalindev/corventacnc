using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Valf kontrol endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Valve Control")]
public class ValveController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly ILogger<ValveController> _logger;

    public ValveController(IMachineService machineService, ILogger<ValveController> logger)
    {
        _machineService = machineService;
        _logger = logger;
    }

    /// <summary>
    /// Belirtilen valf grubunu açar
    /// </summary>
    [HttpPost("{valveGroup}/open")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> OpenValve([FromRoute] string valveGroup)
    {
        try
        {
            var result = await _machineService.OpenValveAsync(valveGroup);

            return Ok(new
            {
                success = result,
                message = result ? $"{valveGroup} valfi açıldı" : $"{valveGroup} valfi açılamadı",
                valveGroup = valveGroup,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ValveGroup} valfi açılırken hata oluştu", valveGroup);
            return StatusCode(500, new { Error = ex.Message, Operation = "Open valve" });
        }
    }

    /// <summary>
    /// Belirtilen valf grubunu kapatır
    /// </summary>
    [HttpPost("{valveGroup}/close")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> CloseValve([FromRoute] string valveGroup)
    {
        try
        {
            var result = await _machineService.CloseValveAsync(valveGroup);

            return Ok(new
            {
                success = result,
                message = result ? $"{valveGroup} valfi kapatıldı" : $"{valveGroup} valfi kapatılamadı",
                valveGroup = valveGroup,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ValveGroup} valfi kapatılırken hata oluştu", valveGroup);
            return StatusCode(500, new { Error = ex.Message, Operation = "Close valve" });
        }
    }

    /// <summary>
    /// Pneumatik valf 1'i açar
    /// </summary>
    [HttpPost("pneumatic1/open")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> OpenPneumaticValve1()
    {
        try
        {
            var result = await _machineService.OpenPneumaticValve1Async();

            return Ok(new
            {
                success = result,
                message = result ? "Pneumatik valf 1 açıldı" : "Pneumatik valf 1 açılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pneumatik valf 1 açılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Open pneumatic valve 1" });
        }
    }

    /// <summary>
    /// Pneumatik valf 1'i kapatır
    /// </summary>
    [HttpPost("pneumatic1/close")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ClosePneumaticValve1()
    {
        try
        {
            var result = await _machineService.ClosePneumaticValve1Async();

            return Ok(new
            {
                success = result,
                message = result ? "Pneumatik valf 1 kapatıldı" : "Pneumatik valf 1 kapatılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pneumatik valf 1 kapatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Close pneumatic valve 1" });
        }
    }

    /// <summary>
    /// Pneumatik valf 2'yi açar
    /// </summary>
    [HttpPost("pneumatic2/open")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> OpenPneumaticValve2()
    {
        try
        {
            var result = await _machineService.OpenPneumaticValve2Async();

            return Ok(new
            {
                success = result,
                message = result ? "Pneumatik valf 2 açıldı" : "Pneumatik valf 2 açılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pneumatik valf 2 açılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Open pneumatic valve 2" });
        }
    }

    /// <summary>
    /// Pneumatik valf 2'yi kapatır
    /// </summary>
    [HttpPost("pneumatic2/close")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> ClosePneumaticValve2()
    {
        try
        {
            var result = await _machineService.ClosePneumaticValve2Async();

            return Ok(new
            {
                success = result,
                message = result ? "Pneumatik valf 2 kapatıldı" : "Pneumatik valf 2 kapatılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pneumatik valf 2 kapatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Close pneumatic valve 2" });
        }
    }

    /// <summary>
    /// Valf grubunun durumunu getirir
    /// </summary>
    [HttpGet("{valveGroup}/status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetValveStatus([FromRoute] string valveGroup)
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            // ValveGroup'a göre status döndür
            var valveStatus = valveGroup.ToLower() switch
            {
                "leftvalvegroup" => status.LeftValveGroupOpen,
                "rightvalvegroup" => status.RightValveGroupOpen,
                "topvalvegroup" => status.TopValveGroupOpen,
                "bottomvalvegroup" => status.BottomValveGroupOpen,
                _ => false
            };

            return Ok(new
            {
                success = true,
                data = new
                {
                    valveGroup = valveGroup,
                    isOpen = valveStatus,
                    status = valveStatus ? "Açık" : "Kapalı"
                },
                message = $"{valveGroup} valf durumu alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ValveGroup} valf durumu alınırken hata oluştu", valveGroup);
            return StatusCode(500, new { Error = ex.Message, Operation = "Get valve status" });
        }
    }

    /// <summary>
    /// Tüm valflerin durumunu getirir
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetAllValveStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    leftValveGroup = status.LeftValveGroupOpen,
                    rightValveGroup = status.RightValveGroupOpen,
                    topValveGroup = status.TopValveGroupOpen,
                    bottomValveGroup = status.BottomValveGroupOpen,
                    pneumaticValve1 = status.PneumaticValve1Open,
                    pneumaticValve2 = status.PneumaticValve2Open
                },
                message = "Tüm valf durumları alındı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm valf durumları alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get all valve status" });
        }
    }

    /// <summary>
    /// Tüm valfleri kapatır (acil durum)
    /// </summary>
    [HttpPost("close-all")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> CloseAllValves()
    {
        try
        {
            var results = new List<bool>();

            // Tüm valf gruplarını kapat
            results.Add(await _machineService.CloseValveAsync("LeftValveGroup"));
            results.Add(await _machineService.CloseValveAsync("RightValveGroup"));
            results.Add(await _machineService.CloseValveAsync("TopValveGroup"));
            results.Add(await _machineService.CloseValveAsync("BottomValveGroup"));
            results.Add(await _machineService.ClosePneumaticValve1Async());
            results.Add(await _machineService.ClosePneumaticValve2Async());

            var successCount = results.Count(r => r);
            var totalCount = results.Count;

            return Ok(new
            {
                success = successCount == totalCount,
                message = $"{successCount}/{totalCount} valf başarıyla kapatıldı",
                successCount = successCount,
                totalCount = totalCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tüm valfler kapatılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Close all valves" });
        }
    }
}

#region Request Models

public class CompressPartRequest
{
    [Required]
    [Range(0.1, 300.0, ErrorMessage = "Target pressure must be between 0.1 and 300 bar")]
    public double TargetPressure { get; set; }

    [Required]
    [Range(0.01, 10.0, ErrorMessage = "Tolerance must be between 0.01 and 10 bar")]
    public double Tolerance { get; set; }
}

public class ResetPartPositionRequest
{
    [Required]
    [Range(0.1, 1000.0, ErrorMessage = "Reset distance must be between 0.1 and 1000mm")]
    public double ResetDistance { get; set; }
}

#endregion 