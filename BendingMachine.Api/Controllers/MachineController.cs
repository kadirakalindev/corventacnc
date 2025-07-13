using Microsoft.AspNetCore.Mvc;
using BendingMachine.Application.Interfaces;
using BendingMachine.Application.DTOs;
using BendingMachine.Domain.Configuration;

namespace BendingMachine.Api.Controllers;

/// <summary>
/// Genel makine kontrolü endpointleri
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Machine Control")]
public class MachineController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly ILogger<MachineController> _logger;

    public MachineController(IMachineService machineService, ILogger<MachineController> logger)
    {
        _machineService = machineService;
        _logger = logger;
    }

    /// <summary>
    /// Makineye bağlanır
    /// </summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Connect()
    {
        try
        {
            var result = await _machineService.ConnectAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Makineye bağlanıldı" : "Makineye bağlanılamadı",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makineye bağlanılırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Connect to machine" });
        }
    }

    /// <summary>
    /// Makine bağlantısını keser
    /// </summary>
    [HttpPost("disconnect")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Disconnect()
    {
        try
        {
            var result = await _machineService.DisconnectAsync();

            return Ok(new
            {
                success = result,
                message = result ? "Makine bağlantısı kesildi" : "Makine bağlantısı kesilemedi",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine bağlantısı kesilirken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Disconnect from machine" });
        }
    }

    /// <summary>
    /// Makine durumunu getirir
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetMachineStatus()
    {
        try
        {
            var status = await _machineService.GetMachineStatusAsync();

            return Ok(new
            {
                success = true,
                data = status,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makine durumu alınırken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Get machine status" });
        }
    }

    /// <summary>
    /// Makine konfigürasyonunu getirir
    /// </summary>
    [HttpGet("configuration")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetMachineConfiguration()
    {
        try
        {
            var configuration = await _machineService.GetMachineConfigurationAsync();
            
            return Ok(new
            {
                success = true,
                data = configuration,
                message = "Machine configuration retrieved successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving machine configuration");
            return StatusCode(503, new { Error = ex.Message, Operation = "Get machine configuration" });
        }
    }

    /// <summary>
    /// Makine konfigürasyonunu günceller
    /// </summary>
    [HttpPost("configuration")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> UpdateMachineConfiguration([FromBody] MachineConfiguration configuration)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _machineService.UpdateMachineConfigurationAsync(configuration);
            
            if (result)
            {
                return Ok(new
                {
                    success = true,
                    message = "Machine configuration updated successfully",
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid configuration provided",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating machine configuration");
            return StatusCode(503, new { Error = ex.Message, Operation = "Update machine configuration" });
        }
    }

    /// <summary>
    /// Konfigürasyonu dosyaya kaydet
    /// </summary>
    [HttpPost("configuration/save")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> SaveConfiguration()
    {
        try
        {
            var result = await _machineService.SaveConfigurationToFileAsync();
            
            return Ok(new
            {
                Success = result,
                Message = result ? "Configuration saved successfully" : "Failed to save configuration",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return StatusCode(503, new { Error = ex.Message, Operation = "Save configuration" });
        }
    }

    /// <summary>
    /// Konfigürasyonu dosyadan yükle
    /// </summary>
    [HttpPost("configuration/load")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> LoadConfiguration()
    {
        try
        {
            var result = await _machineService.LoadConfigurationFromFileAsync();
            
            return Ok(new
            {
                Success = result,
                Message = result ? "Configuration loaded successfully" : "Failed to load configuration",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
            return StatusCode(503, new { Error = ex.Message, Operation = "Load configuration" });
        }
    }

    /// <summary>
    /// Acil durdurma
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
    /// Makine alarmını sıfırlar
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
    /// Bağlantı durumunu kontrol eder
    /// </summary>
    [HttpGet("connection-status")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetConnectionStatus()
    {
        try
        {
            var isConnected = await _machineService.IsConnectedAsync();

            return Ok(new
            {
                success = true,
                isConnected = isConnected,
                message = isConnected ? "Makine bağlı" : "Makine bağlı değil",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı durumu kontrol edilirken hata oluştu");
            return StatusCode(500, new { Error = ex.Message, Operation = "Check connection status" });
        }
    }
}

#region Request Models

public class UpdateConfigurationRequest
{
    public object Configuration { get; set; } = new();
}

#endregion

