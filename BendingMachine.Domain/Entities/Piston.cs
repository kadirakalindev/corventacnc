using BendingMachine.Domain.Common;
using BendingMachine.Domain.Enums;

namespace BendingMachine.Domain.Entities;

public class Piston : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public PistonType Type { get; set; }
    public ValveGroup ValveGroup { get; set; }
    
    // Physical Properties
    public double StrokeLength { get; set; } // Kurs mesafesi (mm)
    public double CurrentPosition { get; set; } // Mevcut pozisyon (mm)
    public double TargetPosition { get; set; } // Hedef pozisyon (mm)
    public double CurrentVoltage { get; set; } // Mevcut voltaj (-10V to +10V)
    public double Speed { get; set; } // Hız (mm/s)
    
    // Motion State
    public MotionEnum Motion { get; set; } = MotionEnum.Closed;
    public bool IsMoving { get; set; }
    public bool IsAtTarget { get; set; }
    
    // Modbus Addresses
    public int VoltageAddress { get; set; } // Analog output address (for main pistons)
    public int RulerAddress { get; set; } // Ruler (linear potentiometer) address
    public int ResetAddress { get; set; } // Ruler reset address
    
    // Direction Coil Addresses (only for side support pistons - yan dayama pistonları)
    public int? ForwardCoilAddress { get; set; } // Optional - only for side support pistons
    public int? BackwardCoilAddress { get; set; } // Optional - only for side support pistons
    
    // Ruler Configuration
    public int RegisterCount { get; set; } // Register range (e.g., 32767, 4095)
    public double RulerValue { get; set; } // Raw ruler value
    public double ReferencePosition { get; set; } // Stage sıfırlama sonrası referans pozisyonu (mm)
    
    // Min/Max Register Configuration (only for side support pistons)
    public int? MinRegister { get; set; } // Minimum register value (for side support pistons)
    public int? MaxRegister { get; set; } // Maximum register value (for side support pistons)
    
    // Safety & Limits
    public double MinPosition { get; set; } = 0; // Usually 0mm
    public double MaxPosition { get; set; } // Usually equals StrokeLength
    public double PositionTolerance { get; set; } = 1.0; // mm tolerance
    
    // Control Type
    public bool IsVoltageControlled => VoltageAddress > 0; // Main pistons (Top, Bottom, Left, Right)
    public bool IsCoilControlled => ForwardCoilAddress.HasValue && BackwardCoilAddress.HasValue; // Side support pistons
    public bool UsesMinMaxRange => MinRegister.HasValue && MaxRegister.HasValue; // Side support pistons with min/max range
    
    // Methods for position calculation - 4-20mA ANALOG INPUT DOĞRU DÖNÜŞÜM
    public double CalculatePositionFromRuler(ushort rulerValue)
    {
        // ✅ TABLODA GÖRE: RULER SENSÖRLER 4-20mA ANALOG INPUT!
        // 4mA = 0mm (en alt pozisyon), 20mA = StrokeLength mm (en üst pozisyon)
        
        // ⚠️ OVERFLOW KONTROLÜ: rulerValue > RegisterCount durumunda 20mA'ya clamp et
        int clampedRulerValue = Math.Min(rulerValue, RegisterCount);
        
        // ✅ HESAPLAMA ORNEK.TXT'DEN TAM AYNI FORMÜL: RegisterToMilliamps
        // DİREKT HESAPLAMA - kendi RegisterCount'ımızı kullan
        const double minMA = 4.0;
        const double maxMA = 20.0;
        
        // mA hesaplama: mA = registerValue / registerCount * (maxMA - minMA) + minMA
        double currentMA = Math.Round((double)clampedRulerValue / RegisterCount * (maxMA - minMA) + minMA, 2);
        currentMA = Math.Clamp(currentMA, minMA, maxMA);
        
        // 4-20mA aralığını 0-StrokeLength pozisyonuna lineer map et
        // mA değerini 0-1 aralığına normalize et
        double normalizedValue = (currentMA - minMA) / (maxMA - minMA);
        
        // 0-1 aralığını 0-StrokeLength aralığına scale et
        double calculatedPosition = Math.Round(normalizedValue * StrokeLength, 2);
        
        // Güvenlik kontrolü
        return Math.Max(0, Math.Min(calculatedPosition, StrokeLength));
    }
    
    /// <summary>
    /// Yan dayama pistonları için Min/Max register aralığı kullanarak pozisyon hesaplar
    /// FORMÜL: normalized_value = (value - MinRegister) / (MaxRegister - MinRegister)
    /// mm_value = normalized_value * Stroke_L
    /// </summary>
    public double CalculatePositionFromRulerMinMax(ushort rulerValue)
    {
        // Min/Max değerleri kontrolü
        if (!MinRegister.HasValue || !MaxRegister.HasValue)
        {
            throw new InvalidOperationException("MinRegister ve MaxRegister değerleri tanımlanmamış - Min/Max hesaplama yapılamaz");
        }
        
        // ✅ VERDİĞİNİZ FORMÜL: protected override double value1Calculate()
        // double normalized_value = (_value - MinRegister) / (MaxRegister - MinRegister);
        // double mm_value = Math.Round(normalized_value * Stroke_L, 2);
        
        var minReg = MinRegister.Value;
        var maxReg = MaxRegister.Value;
        
        // Register değerini sınırla
        double clampedValue = Math.Clamp(rulerValue, minReg, maxReg);
        
        // Normalize et (0-1 aralığına getir)
        double normalizedValue = (clampedValue - minReg) / (double)(maxReg - minReg);
        
        // Stroke length ile çarp ve mm değerini hesapla
        double mmValue = Math.Round(normalizedValue * StrokeLength, 2);
        
        // Güvenlik kontrolü
        return Math.Clamp(mmValue, 0, StrokeLength);
    }
    
    /// <summary>
    /// Piston pozisyonunu signed (negatif değer destekleyen) cetvel değerinden hesaplar
    /// NEGATİF DEĞERLER desteklenir - referans noktasının altındaki pozisyonlar için
    /// REFERANS MANTIK: Stage ayarı sırasında kaydedilen ReferencePosition = 0mm kabul edilir
    /// </summary>
    public double CalculatePositionFromRulerSigned(short rulerValue)
    {
        // ✅ REFERANS NOKTA MANTIGI: Kaydedilen referans pozisyonuna göre signed hesaplama
        // Stage sıfırlama sırasında mevcut pozisyon ReferencePosition olarak kaydedilir
        // Bu pozisyondan sapma = Gerçek pozisyon (+ veya -)
        
        // Overflow kontrolü
        int clampedRulerValue = Math.Min(Math.Abs(rulerValue), RegisterCount);
        
        // 4-20mA hesaplama - MEVCUT pozisyonu bul
        const double minMA = 4.0;
        const double maxMA = 20.0;
        
        // mA hesaplama: mA = registerValue / registerCount * (maxMA - minMA) + minMA
        double currentMA = Math.Round((double)clampedRulerValue / RegisterCount * (maxMA - minMA) + minMA, 2);
        currentMA = Math.Clamp(currentMA, minMA, maxMA);
        
        // 4-20mA aralığını 0-StrokeLength pozisyonuna lineer map et
        double normalizedValue = (currentMA - minMA) / (maxMA - minMA);
        double currentAbsolutePosition = normalizedValue * StrokeLength;
        
        // ✅ SIGNED POZİSYON HESAPLAMA
        // Mevcut mutlak pozisyon - Referans pozisyon = Signed pozisyon
        double calculatedPosition = Math.Round(currentAbsolutePosition - ReferencePosition, 2);
        
        // Negatif değer koruması - rulerValue negatifse sonucu da negatif yap
        if (rulerValue < 0)
        {
            calculatedPosition = -Math.Abs(calculatedPosition);
        }
        
        // Stroke length sınırları içinde tut (-StrokeLength ile +StrokeLength)
        return Math.Clamp(calculatedPosition, -StrokeLength, StrokeLength);
    }
    
    public ushort CalculateRulerFromPosition(double position)
    {
        return (ushort)Math.Round(position * RegisterCount / StrokeLength);
    }
    
    public short CalculateVoltageRegister(double voltage)
    {
        // Convert voltage (-10V to +10V) to register value (-2048 to +2047)
        return (short)Math.Round(voltage * 204.7); // 2047 / 10
    }
    
    public double CalculateVoltageFromRegister(short registerValue)
    {
        // Convert register value to voltage
        return Math.Round(registerValue / 204.7, 1);
    }
    
    public bool IsForwardMotion(double voltage)
    {
        return voltage < 0; // Negative voltage = forward
    }
    
    public bool IsBackwardMotion(double voltage)
    {
        return voltage > 0; // Positive voltage = backward
    }
    
    public bool CanMoveBackward()
    {
        return CurrentPosition > MinPosition + PositionTolerance;
    }
    
    public bool CanMoveForward()
    {
        return CurrentPosition < MaxPosition - PositionTolerance;
    }
    
    public MotionEnum GetMotionFromVoltage(double voltage, bool valveOpen)
    {
        if (!valveOpen || Math.Abs(voltage) < 0.1)
            return MotionEnum.Closed;
            
        return voltage < 0 ? MotionEnum.Forward : MotionEnum.Backward;
    }
} 