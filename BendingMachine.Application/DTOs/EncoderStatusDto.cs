namespace BendingMachine.Application.DTOs;

public class EncoderStatusDto
{
    /// <summary>
    /// Encoder'ın mevcut pozisyonu (pulse cinsinden)
    /// </summary>
    public long CurrentPosition { get; set; }

    /// <summary>
    /// Encoder tipi (RV3100)
    /// </summary>
    public string EncoderType { get; set; } = string.Empty;

    /// <summary>
    /// Bir turdaki pulse sayısı (1024)
    /// </summary>
    public int PulsesPerRevolution { get; set; }

    /// <summary>
    /// Hesaplanan mesafe (mm cinsinden)
    /// </summary>
    public double CurrentDistance { get; set; }

    /// <summary>
    /// Encoder'ın sağlıklı çalışıp çalışmadığı
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Son güncelleme zamanı
    /// </summary>
    public DateTime LastUpdateTime { get; set; }
} 