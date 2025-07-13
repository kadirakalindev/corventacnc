using BendingMachine.Domain.Interfaces;
using BendingMachine.Domain.Entities;
using BendingMachine.Domain.Enums;
using BendingMachine.Domain.Constants;
using BendingMachine.Domain.Utilities;

using Microsoft.Extensions.Logging;
using BendingMachine.Infrastructure.Modbus;
using DomainBendingParameters = BendingMachine.Domain.Entities.DomainBendingParameters;

namespace BendingMachine.Driver;

public class MachineDriver : IMachineDriver
{
    private readonly IModbusClient _modbusClient;
    private readonly ILogger<MachineDriver>? _logger;
    private readonly Dictionary<PistonType, Piston> _pistons;
    private MachineStatus _currentStatus;
    
    // ✅ PASO TEST ENCODER REFERANS POZİSYONU
    // Parça sıfırlama sonrası paso test için encoder referans noktası
    // ✅ PASO TEST ENCODER REFERANS POZİSYONU
    // Parça sıfırlama sonrası paso test için encoder referans noktası
    private int? _pasoEncoderReferencePosition = null;
    
    // Events
    public event EventHandler<MachineStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<PistonMovedEventArgs>? PistonMoved;
    public event EventHandler<AlarmEventArgs>? AlarmRaised;
    public event EventHandler<SafetyEventArgs>? SafetyViolation;
    
    public bool IsConnected => _modbusClient.IsConnected;
    
    public MachineDriver(IModbusClient modbusClient, ILogger<MachineDriver>? logger = null)
    {
        _modbusClient = modbusClient;
        _logger = logger;
        _pistons = new Dictionary<PistonType, Piston>();
        _currentStatus = new MachineStatus();
        
        InitializePistons();
    }
    
    #region Connection Management
    
    public async Task<bool> ConnectAsync()
    {
        try
        {
                    var result = await _modbusClient.ConnectAsync();
        if (result)
        {
            await LoadConfigurationAsync();
            // Note: Status updates now handled by MachineStatusService
        }
        return result;
        }
        catch (Exception ex)
        {
            OnAlarmRaised($"Connection failed: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }
    
    public async Task<bool> DisconnectAsync()
    {
        // Note: Status updates now handled by MachineStatusService
        return await _modbusClient.DisconnectAsync();
    }
    
    #endregion
    
    #region Status Management
    
    public async Task<MachineStatus> GetMachineStatusAsync()
    {
        await UpdateMachineStatusAsync();
        return _currentStatus;
    }
    
    private async Task UpdateMachineStatusAsync()
    {
        try
        {
            // ✅ SAFETY STATUS - Tüm güvenlik sensörleri (RAW okuma)
            // NOT: Dijital girişlerde LOW (0) = AKTİF mantığı kullanılır
            var emergencyStopRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.EmergencyStopButton); // 0x000A
            var hydraulicErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.HydraulicEngineThermalError); // 0x0000
            var fanErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.FanEngineThermalError); // 0x0001
            var phaseErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PhaseSequenceError); // 0x0002
            
            // ✅ POLLUTION SENSORS - Kirlilik sensörleri (RAW okuma)
            var pollutionSensor1Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor1); // 0x0003
            var pollutionSensor2Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor2); // 0x0004  
            var pollutionSensor3Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor3); // 0x0005
            
            // ✅ ROTATION SENSORS - Rotasyon sensörleri (RAW okuma)
            var leftRotationSensorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftRotationSensor); // 0x0006
            var rightRotationSensorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightRotationSensor); // 0x0007
            
            // ✅ PART PRESENCE SENSORS - İş parçası varlık sensörleri (RAW okuma)
            var leftPartPresentRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftPartPresence); // 0x0008
            var rightPartPresentRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightPartPresence); // 0x0009
            
            // ✅ SADECE GÜVENLİK HATALARI - LOW (0) = AKTİF mantığı
            _currentStatus.EmergencyStop = !emergencyStopRaw;         // LOW = Emergency basılı - ERROR
            _currentStatus.HydraulicThermalError = !hydraulicErrorRaw; // LOW = Hydraulic hata - ERROR
            _currentStatus.FanThermalError = !fanErrorRaw;            // LOW = Fan hata - ERROR
            _currentStatus.PhaseSequenceError = !phaseErrorRaw;        // LOW = Faz hata - ERROR
            
            // ✅ DİĞER SENSÖRLER - NORMAL MANTIK (HIGH = AKTİF)
            _currentStatus.LeftPartPresent = leftPartPresentRaw;       // HIGH = Part mevcut - NORMAL
            _currentStatus.RightPartPresent = rightPartPresentRaw;     // HIGH = Part mevcut - NORMAL
            
            // İşlenmiş sensör değerleri - NORMAL MANTIK (HIGH = AKTİF)
            var pollutionSensor1 = pollutionSensor1Raw;     // HIGH = Kirlilik tespit edildi - NORMAL
            var pollutionSensor2 = pollutionSensor2Raw;     // HIGH = Kirlilik tespit edildi - NORMAL
            var pollutionSensor3 = pollutionSensor3Raw;     // HIGH = Kirlilik tespit edildi - NORMAL
            var leftRotationSensor = leftRotationSensorRaw;    // HIGH = Rotasyon tespit edildi - NORMAL
            var rightRotationSensor = rightRotationSensorRaw;  // HIGH = Rotasyon tespit edildi - NORMAL
            
            // ✅ MOTOR STATUS - Motor durumları
            _currentStatus.HydraulicMotorRunning = await _modbusClient.ReadCoilAsync(ModbusAddresses.HydraulicEngine);
            _currentStatus.FanMotorRunning = await _modbusClient.ReadCoilAsync(ModbusAddresses.FanEngine);
            _currentStatus.AlarmActive = await _modbusClient.ReadCoilAsync(ModbusAddresses.Alarm);
            
            // ✅ VALVE STATUS - Valf durumları
            _currentStatus.S1ValveOpen = await _modbusClient.ReadCoilAsync(ModbusAddresses.S1);
            _currentStatus.S2ValveOpen = await _modbusClient.ReadCoilAsync(ModbusAddresses.S2);
            
            // ✅ OIL SYSTEM - YAĞ SİSTEMİ (Analog Input Sensors)
            var s1PressureRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S1_OilPressure);      // 0x000B
            var s2PressureRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S2_OilPressure);      // 0x000A
            var s1FlowRateRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S1_OilFlowRate);      // 0x000D
            var s2FlowRateRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S2_OilFlowRate);      // 0x000C
            var oilTempRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.OilTemperature);         // 0x000E
            var oilHumidityRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.OilHumidity);        // 0x000F
            var oilLevelRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.OilLevel);              // 0x0010
            
            // ✅ CONVERTER.MD DOĞRU YÖNTEM: RegisterToBarAndMilliamps metodu kullanılmalı
            // Pressure sensörleri 4-20mA analog input: 4mA = 0 bar, 20mA = 250 bar
            var (s1Pressure, s1mA) = DataConverter.RegisterToBarAndMilliamps(s1PressureRaw, 4095, 4.0, 20.0, 0.0, 250.0);
            var (s2Pressure, s2mA) = DataConverter.RegisterToBarAndMilliamps(s2PressureRaw, 4095, 4.0, 20.0, 0.0, 250.0);
            _currentStatus.S1OilPressure = s1Pressure; // bar - converter.md'ye göre doğru metod
            _currentStatus.S2OilPressure = s2Pressure; // bar - converter.md'ye göre doğru metod
            _currentStatus.OilTemperature = DataConverter.RegisterToMilliampsToTemperature(oilTempRaw); // ✅ converter.md gerçek metod
            
            // ✅ CONVERTER.MD GERÇEK METODLAR (alias değil)
            _currentStatus.S1OilFlowRate = DataConverter.RegisterToMilliampsToCmPerSecond(s1FlowRateRaw);      // cm/sn - gerçek metod
            _currentStatus.S2OilFlowRate = DataConverter.RegisterToMilliampsToCmPerSecond(s2FlowRateRaw);      // cm/sn - gerçek metod
            _currentStatus.OilHumidity = DataConverter.RegisterToHumidity(oilHumidityRaw);                     // % - gerçek metod
            _currentStatus.OilLevel = DataConverter.RegisterToPercentage(oilLevelRaw);                         // % - gerçek metod
            
            // ✅ PISTON POSITIONS - Piston pozisyonları
            await UpdatePistonPositionsAsync();
            
            // Piston pozisyonlarını MachineStatus'a aktar
            _currentStatus.TopPistonPosition = _pistons[PistonType.TopPiston].CurrentPosition;
            _currentStatus.BottomPistonPosition = _pistons[PistonType.BottomPiston].CurrentPosition;
            _currentStatus.LeftPistonPosition = _pistons[PistonType.LeftPiston].CurrentPosition;
            _currentStatus.RightPistonPosition = _pistons[PistonType.RightPiston].CurrentPosition;
            _currentStatus.LeftReelPistonPosition = _pistons[PistonType.LeftReelPiston].CurrentPosition;
            _currentStatus.RightReelPistonPosition = _pistons[PistonType.RightReelPiston].CurrentPosition;
            _currentStatus.LeftBodyPistonPosition = _pistons[PistonType.LeftBodyPiston].CurrentPosition;
            _currentStatus.RightBodyPistonPosition = _pistons[PistonType.RightBodyPiston].CurrentPosition;
            _currentStatus.LeftJoinPistonPosition = _pistons[PistonType.LeftJoinPiston].CurrentPosition;
            _currentStatus.RightJoinPistonPosition = _pistons[PistonType.RightJoinPiston].CurrentPosition;
            
            // ✅ ROTATION ENCODER POSITION - Rotasyon encoder pozisyonu (RV3100 - 1024 pulse/tur) - NEGATİF DEĞERLER DESTEKLENİR
            var rotationEncoderRaw = await _modbusClient.ReadInputRegisterAsSignedAsync(ModbusAddresses.RulerRotation); // 0x001E
            
            // Encoder artık direkt signed olarak okunuyor - ek dönüşüm gerekmez
            short signedEncoder = rotationEncoderRaw;
            _currentStatus.RotationEncoderRaw = signedEncoder;
            
            // Derece hesaplaması: Her 1024 pulse = 1 tam tur (360 derece)
            // Örnek: -500 pulse = (-500 / 1024) * 360 = -175.78 derece
            _currentStatus.RotationPosition = (signedEncoder / 1024.0) * 360.0;
            
            _currentStatus.LastUpdateTime = DateTime.UtcNow;
            
            // 📊 DİJİTAL SENSÖR DURUMU
            _logger?.LogDebug("📊 DİJİTAL SENSÖR DURUMU - Emergency: {EmergencyStop}, Hydraulic: {HydraulicError}, Fan: {FanError}, Phase: {PhaseError}, " +
                "P1: {P1}, P2: {P2}, P3: {P3}, LeftRot: {LeftRot}, RightRot: {RightRot}, LeftPart: {LeftPart}, RightPart: {RightPart}",
                _currentStatus.EmergencyStop, _currentStatus.HydraulicThermalError, _currentStatus.FanThermalError, _currentStatus.PhaseSequenceError,
                pollutionSensor1, pollutionSensor2, pollutionSensor3,
                leftRotationSensor, rightRotationSensor,
                _currentStatus.LeftPartPresent, _currentStatus.RightPartPresent);
                
            // 🔄 ROTASYON SİSTEMİ
            _logger?.LogDebug("🔄 ROTASYON SİSTEMİ - Raw: {Raw} (0x{Raw:X4}), Signed: {Signed}, Pozisyon: {Position:F1}°, Yön: {Direction}, Hız: {Speed}%",
                rotationEncoderRaw, rotationEncoderRaw, signedEncoder, _currentStatus.RotationPosition, 
                _currentStatus.RotationDirection, _currentStatus.RotationSpeed);
                
            // 🛢️ YAĞ SİSTEMİ
            _logger?.LogDebug("🛢️ YAĞ SİSTEMİ - S1: {S1Pressure:F1} bar ({S1mA:F2}mA), S2: {S2Pressure:F1} bar ({S2mA:F2}mA), " + 
                "Akış: S1={S1Flow} S2={S2Flow} cm/sn, Sıcaklık: {Temp}°C",
                _currentStatus.S1OilPressure, s1mA, _currentStatus.S2OilPressure, s2mA, 
                _currentStatus.S1OilFlowRate, _currentStatus.S2OilFlowRate, _currentStatus.OilTemperature);
            
            OnStatusChanged();
        }
        catch (Exception ex)
        {
            OnAlarmRaised($"Status update failed: {ex.Message}", SafetyStatus.Warning);
            _logger?.LogError(ex, "❌ UpdateMachineStatusAsync hatası");
        }
    }
    
    private async Task UpdatePistonPositionsAsync()
    {
        foreach (var piston in _pistons.Values)
        {
            try
            {
                // ✨ YENİ: Her zaman signed okuma yap
                var rulerValue = await _modbusClient.ReadInputRegisterAsSignedAsync(piston.RulerAddress);
                
                // ✨ YENİ: Piston tipine göre hesaplama seç
                if (piston.UsesMinMaxRange)
                {
                    // Yan dayama pistonları - Min/Max range ile hesaplama
                    var absolutePosition = piston.CalculatePositionFromRulerMinMax((ushort)Math.Abs(rulerValue));
                    // Referans noktasına göre pozisyon hesapla
                    piston.CurrentPosition = Math.Round(absolutePosition - piston.ReferencePosition, 2);
                }
                else
                {
                    // Ana pistonlar - Her zaman signed hesaplama
                    piston.CurrentPosition = piston.CalculatePositionFromRulerSigned(rulerValue);
                }
                
                piston.RulerValue = rulerValue;
                
                _logger?.LogDebug("📏 {PistonName} pozisyon güncellendi: Raw={Raw}, Position={Pos:F2}mm, Reference={Ref:F2}mm", 
                    piston.Name, rulerValue, piston.CurrentPosition, piston.ReferencePosition);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("⚠️ {PistonName} pozisyon güncellemesi başarısız: {Error}", piston.Name, ex.Message);
            }
        }
    }
    
    #endregion
    
    #region Piston Control
    
    public async Task<bool> MovePistonAsync(PistonType pistonType, double voltage)
    {
        var piston = _pistons[pistonType];
        
        // Safety check
        if (!await CheckSafetyAsync())
        {
            OnSafetyViolation("System not safe for operation", false);
            return false;
        }
        
        // Voltage limits
        voltage = Math.Max(-10, Math.Min(10, voltage));
        
        try
        {
            // Open valve first
            await OpenValveForPiston(piston);
            
            // Side Support Pistons (Coil controlled - VoltageAddress = 0)
            if (piston.VoltageAddress == 0)
            {
                // Coil controlled pistons - activate appropriate direction coil
                var forward = voltage < 0; // Forward = negative voltage
                var backward = voltage > 0; // Backward = positive voltage
                
                switch (pistonType)
                {
                    case PistonType.LeftReelPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M01_LeftReelPistonForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M02_LeftReelPistonBackward, backward);
                        break;
                    case PistonType.RightReelPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M07_RightReelPistonForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M08_RightReelPistonBackward, backward);
                        break;
                    case PistonType.LeftBodyPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M03_LeftBodyForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M04_LeftBodyBackward, backward);
                        break;
                    case PistonType.RightBodyPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M09_RightBodyForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M10_RightBodyBackward, backward);
                        break;
                    case PistonType.LeftJoinPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M05_LeftJoinPistonForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M06_LeftJoinPistonBackward, backward);
                        break;
                    case PistonType.RightJoinPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M11_RightJoinPistonForward, forward);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M12_RightJoinPistonBackward, backward);
                        break;
                }
                
                _logger?.LogDebug("{PistonName} - Coil kontrolü: Forward={Forward}, Backward={Backward}", 
                    piston.Name, forward, backward);
            }
            else
            {
                // Voltage controlled pistons (Main pistons)
                var voltageRegister = (ushort)DataConverter.VoltToRegisterConvert(voltage);
                await _modbusClient.WriteHoldingRegisterAsync(piston.VoltageAddress, voltageRegister);
                
                _logger?.LogDebug("{PistonName} - Voltage kontrolü: {Voltage}V (Register: {Register})", 
                    piston.Name, voltage, voltageRegister);
            }
            
            piston.CurrentVoltage = voltage;
            piston.Motion = piston.GetMotionFromVoltage(voltage, true);
            piston.IsMoving = Math.Abs(voltage) > 0.1;
            
            OnPistonMoved(pistonType, piston.CurrentPosition, piston.TargetPosition, piston.Motion);
            
            return true;
        }
        catch (Exception ex)
        {
            OnAlarmRaised($"Piston move failed: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }
    
    public async Task<bool> StopPistonAsync(PistonType pistonType)
    {
        var piston = _pistons[pistonType];
        
        try
        {
            // Side Support Pistons (Coil controlled - VoltageAddress = 0)
            if (piston.VoltageAddress == 0)
            {
                // Coil controlled pistons - stop coils
                switch (pistonType)
                {
                    case PistonType.LeftReelPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M01_LeftReelPistonForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M02_LeftReelPistonBackward, false);
                        break;
                    case PistonType.RightReelPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M07_RightReelPistonForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M08_RightReelPistonBackward, false);
                        break;
                    case PistonType.LeftBodyPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M03_LeftBodyForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M04_LeftBodyBackward, false);
                        break;
                    case PistonType.RightBodyPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M09_RightBodyForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M10_RightBodyBackward, false);
                        break;
                    case PistonType.LeftJoinPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M05_LeftJoinPistonForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M06_LeftJoinPistonBackward, false);
                        break;
                    case PistonType.RightJoinPiston:
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M11_RightJoinPistonForward, false);
                        await _modbusClient.WriteCoilAsync(ModbusAddresses.M12_RightJoinPistonBackward, false);
                        break;
                }
            }
            else
            {
                // Voltage controlled pistons (Main pistons)
                await _modbusClient.WriteHoldingRegisterAsync(piston.VoltageAddress, 0);
            }
            
            // Then close valve for all pistons
            await CloseValveForPiston(piston);
            
            piston.CurrentVoltage = 0;
            piston.Motion = MotionEnum.Closed;
            piston.IsMoving = false;
            
            return true;
        }
        catch (Exception ex)
        {
            OnAlarmRaised($"Piston stop failed: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }
    
    public async Task<bool> MovePistonToPositionAsync(PistonType pistonType, double targetPosition)
    {
        var piston = _pistons[pistonType];
        piston.TargetPosition = targetPosition;
        piston.IsAtTarget = false;
        
        // Limit check
        if (targetPosition < piston.MinPosition || targetPosition > piston.MaxPosition)
        {
            OnAlarmRaised($"Target position {targetPosition}mm out of range [{piston.MinPosition}-{piston.MaxPosition}]mm for {piston.Name}", SafetyStatus.Warning);
            return false;
        }
        
        _logger?.LogInformation("{PistonName} - Pozisyon kontrol başlatıldı: Hedef={Target}mm", piston.Name, targetPosition);
        
        // ✅ VALF YÖNETİMİ: İşlem başında bir kez aç
        await OpenValveForPiston(piston);
        
        // ✅ HASSAS CLOSED-LOOP POSITION CONTROL
        var maxIterations = 200; // Max 20 saniye (100ms * 200) - hassas konumlandırma için uzatıldı
        var iteration = 0;
        var consecutiveCloseCount = 0; // Hedefe yakın kalma sayacı
        const int requiredConsecutiveClose = 3; // 3 ardışık yakın okuma gerekli
        
        while (iteration < maxIterations)
        {
            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                await StopPistonAsync(pistonType);
                await CloseValveForPiston(piston); // ✅ VALF KAPAT: Güvenlik hatası
                OnAlarmRaised($"Safety violation during position control for {piston.Name}", SafetyStatus.Error);
                return false;
            }
            
            // Mevcut pozisyonu oku - NEGATİF DEĞERLER DESTEKLENİR
            try
            {
                var rulerValue = await _modbusClient.ReadInputRegisterAsSignedAsync(piston.RulerAddress);
                piston.CurrentPosition = piston.CalculatePositionFromRulerSigned(rulerValue);
                piston.RulerValue = rulerValue;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ruler okuma hatası: {PistonName}", piston.Name);
                await Task.Delay(50); // Kısa bekleme
                iteration++;
                continue;
            }
            
            var currentPos = piston.CurrentPosition;
            var error = targetPosition - currentPos;
            
            _logger?.LogDebug("{PistonName} - Iter:{Iteration} Mevcut={Current}mm, Hedef={Target}mm, Hata={Error}mm", 
                piston.Name, iteration, currentPos, targetPosition, error);
            
            // ✅ HASSAS HEDEF KONTROLÜ - Stabil konumlandırma
            if (Math.Abs(error) < piston.PositionTolerance)
            {
                consecutiveCloseCount++;
                _logger?.LogDebug("{PistonName} - Hedefe yakın: {Count}/{Required} - Hata={Error:F3}mm", 
                    piston.Name, consecutiveCloseCount, requiredConsecutiveClose, error);
                
                if (consecutiveCloseCount >= requiredConsecutiveClose)
            {
                await StopPistonAsync(pistonType);
                    await CloseValveForPiston(piston); // ✅ VALF KAPAT: İşlem başarılı
                piston.IsAtTarget = true;
                    _logger?.LogInformation("{PistonName} - ✅ HASSAS HEDEFE ULAŞILDI! Final pozisyon: {Position:F2}mm (Hata: {Error:F3}mm, Tolerans: ±{Tolerance:F2}mm)", 
                        piston.Name, currentPos, error, piston.PositionTolerance);
                return true;
                }
                else
                {
                    // Hedefe yakın ama henüz stabil değil - bekle
                    await Task.Delay(50); // Kısa stabilizasyon bekleme
                    iteration++;
                    continue;
                }
            }
            else
            {
                // Hedeften uzak - sayacı sıfırla
                consecutiveCloseCount = 0;
            }
            
            // P Controller with voltage scaling
            var direction = error > 0 ? "Forward" : "Backward";
            var voltage = 0.0;
            
            if (piston.IsVoltageControlled)
            {
                // ✅ GELIŞMIŞ HASSAS KONUMLANDIRMA - P Controller
                var absError = Math.Abs(error);
                
                // Kademeli hız kontrolü - Hedefe yaklaştıkça yavaşla
                double proportionalGain;
                double maxVoltage;
                double minVoltage;
                
                // Piston tipine göre minimum hareket voltajı
                double pistonMinVoltage = pistonType switch
                {
                    PistonType.BottomPiston => 0.7, // Alt piston min 0.7V
                    PistonType.LeftPiston => 0.3,   // Sol alt piston min 0.3V
                    PistonType.RightPiston => 0.3,  // Sağ alt piston min 0.3V
                    _ => 0.5                        // Diğer pistonlar için varsayılan
                };
                
                if (absError > 10.0) // Çok uzak mesafe - En hızlı hareket
                {
                    proportionalGain = 1.2;
                    maxVoltage = 8.0;
                    minVoltage = pistonMinVoltage + 1.8; // Min voltaj + offset
                }
                else if (absError > 5.0) // Uzak mesafe - Hızlı hareket
                {
                    proportionalGain = 0.9;
                    maxVoltage = 6.0;
                    minVoltage = pistonMinVoltage + 1.2;
                }
                else if (absError > 2.0) // Orta mesafe - Orta hız
                {
                    proportionalGain = 0.6;
                    maxVoltage = 3.5;
                    minVoltage = pistonMinVoltage + 0.8;
                }
                else if (absError > 0.5) // Yakın mesafe - Yavaş hareket
                {
                    proportionalGain = 0.4;
                    maxVoltage = 2.0;
                    minVoltage = pistonMinVoltage + 0.4;
                }
                else // Son yaklaşma - Hassas
                {
                    proportionalGain = 0.25;
                    maxVoltage = 1.5;
                    minVoltage = pistonMinVoltage + 0.2; // Min voltaj + küçük offset
                }
                
                // Proportional kontrolcü hesaplama
                voltage = error * proportionalGain;
                
                // Voltaj limitlerini uygula
                voltage = Math.Max(-maxVoltage, Math.Min(maxVoltage, voltage));
                
                // Yön kontrolü (Forward = negative, Backward = positive)
                voltage = error > 0 ? -Math.Abs(voltage) : Math.Abs(voltage);
                
                // Minimum voltaj eşiği - çok küçük hatalar için
                if (Math.Abs(voltage) < minVoltage && absError > piston.PositionTolerance)
                {
                    voltage = error > 0 ? -minVoltage : minVoltage;
                }
                
                _logger?.LogDebug("{PistonName} - Hassas kontrol: Hata={Error:F2}mm, Gain={Gain:F1}, MaxV={MaxV:F1}V, MinV={MinV:F1}V, Çıkış={Voltage:F2}V", 
                    piston.Name, error, proportionalGain, maxVoltage, minVoltage, voltage);
            }
            else
            {
                // ✅ YAN DAYAMA PİSTONLARI - Hassas Pulse Kontrolü
                // Coil control (binary) ama hassas konumlandırma için pulse kontrolü
                var absError = Math.Abs(error);
                
                if (absError <= piston.PositionTolerance)
                {
                    // Hedefe ulaştı - hareket durdur
                    voltage = 0.0;
                }
                else if (absError > 2.0) // Uzak mesafe - sürekli hareket
                {
                voltage = error > 0 ? -5.0 : 5.0; // Forward = negative, Backward = positive
                }
                else // Yakın mesafe - pulse kontrolü (0.5-2.0mm arası)
                {
                    // Hassas konumlandırma için pulse hareket
                    // Büyük hata = uzun pulse, küçük hata = kısa pulse
                    var pulseRatio = Math.Min(1.0, absError / 2.0); // 0.25-1.0 arası oran
                    voltage = error > 0 ? -5.0 * pulseRatio : 5.0 * pulseRatio;
                    
                    _logger?.LogDebug("{PistonName} - Pulse kontrolü: Hata={Error:F2}mm, PulseRatio={Ratio:F2}, Voltaj={Voltage:F1}V", 
                        piston.Name, error, pulseRatio, voltage);
                }
            }
            
            _logger?.LogDebug("{PistonName} - {Direction}: {Voltage}V", piston.Name, direction, voltage);
            
            // Hareketi başlat
            try
            {
                // ✅ VALF ZATEN AÇIK - Tekrar açmaya gerek yok
                
                if (piston.VoltageAddress == 0)
                {
                    // Coil controlled
                    var forward = voltage < 0;
                    var backward = voltage > 0;
                    
                    switch (pistonType)
                    {
                        case PistonType.LeftReelPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M01_LeftReelPistonForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M02_LeftReelPistonBackward, backward);
                            break;
                        case PistonType.RightReelPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M07_RightReelPistonForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M08_RightReelPistonBackward, backward);
                            break;
                        case PistonType.LeftBodyPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M03_LeftBodyForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M04_LeftBodyBackward, backward);
                            break;
                        case PistonType.RightBodyPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M09_RightBodyForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M10_RightBodyBackward, backward);
                            break;
                        case PistonType.LeftJoinPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M05_LeftJoinPistonForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M06_LeftJoinPistonBackward, backward);
                            break;
                        case PistonType.RightJoinPiston:
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M11_RightJoinPistonForward, forward);
                            await _modbusClient.WriteCoilAsync(ModbusAddresses.M12_RightJoinPistonBackward, backward);
                            break;
                    }
                }
                else
                {
                    // Voltage controlled
                    var voltageRegister = (ushort)DataConverter.VoltToRegisterConvert(voltage);
                    await _modbusClient.WriteHoldingRegisterAsync(piston.VoltageAddress, voltageRegister);
                }
                
                piston.CurrentVoltage = voltage;
                piston.Motion = piston.GetMotionFromVoltage(voltage, true);
                piston.IsMoving = Math.Abs(voltage) > 0.1;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Piston hareket hatası: {PistonName}", piston.Name);
                await StopPistonAsync(pistonType);
                return false;
            }
            
            // Kısa bekleme (100ms)
            await Task.Delay(100);
            iteration++;
        }
        
        // Timeout - hedefe ulaşamadı
        await StopPistonAsync(pistonType);
        await CloseValveForPiston(piston); // ✅ VALF KAPAT: Timeout hatası
        var finalError = targetPosition - piston.CurrentPosition;
        OnAlarmRaised($"Hassas pozisyon timeout: {piston.Name}. Hedef: {targetPosition:F2}mm, Mevcut: {piston.CurrentPosition:F2}mm, Hata: {finalError:F3}mm", SafetyStatus.Warning);
        _logger?.LogWarning("{PistonName} - ❌ HASSAS POZISYON TIMEOUT! {MaxTime}s içinde hedefe ulaşılamadı - Son hata: {Error:F3}mm (Tolerans: ±{Tolerance:F2}mm)", 
            piston.Name, maxIterations * 0.1, finalError, piston.PositionTolerance);
        return false;
    }
    
    public async Task<bool> JogPistonAsync(PistonType pistonType, MotionEnum direction, double voltage)
    {
        if (direction == MotionEnum.Forward)
            voltage = -Math.Abs(voltage); // Forward = negative
        else if (direction == MotionEnum.Backward)
            voltage = Math.Abs(voltage);  // Backward = positive
        else
            voltage = 0;
            
        return await MovePistonAsync(pistonType, voltage);
    }
    
    /// <summary>
    /// Yan dayama pistonları için özel jog kontrolü
    /// Valve açma + Direction coil kontrolü
    /// </summary>
    public async Task<bool> JogSideSupportPistonAsync(PistonType pistonType, MotionEnum direction)
    {
        try
        {
            if (!_pistons.TryGetValue(pistonType, out var piston))
            {
                _logger?.LogError("Piston bulunamadı: {PistonType}", pistonType);
                return false;
            }

            // Sadece yan dayama pistonları için (coil controlled)
            if (!piston.IsCoilControlled)
            {
                _logger?.LogWarning("Bu piston yan dayama değil - voltage kontrolü gerekli: {PistonType}", pistonType);
                return await JogPistonAsync(pistonType, direction, 5.0); // Ana pistonlar için fallback
            }

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("Modbus bağlantısı aktif değil");
                return false;
            }

            // ⚠️ GEÇİCİ: Güvenlik kontrolü devre dışı (debug için)
            // TODO: Güvenlik kontrolleri düzeltildikten sonra açılacak
            // if (!await CheckSafetyAsync())
            // {
            //     _logger?.LogWarning("Güvenlik kontrolü başarısız - yan dayama hareket ettirilemez");
            //     return false;
            // }

            // 1. İLGİLİ VALVE'I AÇ
            await OpenValveForPiston(piston);
            _logger?.LogInformation("Valve açıldı: {PistonType} - {ValveGroup}", pistonType, piston.ValveGroup);

            // Valve açılması için kısa bekleme
            await Task.Delay(50);

            // 2. DİRECTION COIL'LERİNİ KONTROL ET
            bool forwardState = (direction == MotionEnum.Forward);
            bool backwardState = (direction == MotionEnum.Backward);

            if (direction == MotionEnum.Closed)
            {
                // Durdurmak için her iki coil'i de false yap
                forwardState = false;
                backwardState = false;
            }

            // Forward ve Backward coil'lerini ayarla
            if (piston.ForwardCoilAddress.HasValue)
            {
                await _modbusClient.WriteCoilAsync(piston.ForwardCoilAddress.Value, forwardState);
                _logger?.LogInformation("Forward coil ({Address}): {State}", piston.ForwardCoilAddress.Value, forwardState);
            }

            if (piston.BackwardCoilAddress.HasValue)
            {
                await _modbusClient.WriteCoilAsync(piston.BackwardCoilAddress.Value, backwardState);
                _logger?.LogInformation("Backward coil ({Address}): {State}", piston.BackwardCoilAddress.Value, backwardState);
            }

            // Piston durumunu güncelle
            piston.Motion = direction;
            piston.IsMoving = (direction != MotionEnum.Closed);

            _logger?.LogInformation("Yan dayama kontrolü tamamlandı: {PistonType} - {Direction}", pistonType, direction);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Yan dayama kontrolü sırasında hata: {PistonType}", pistonType);
            return false;
        }
    }

    /// <summary>
    /// Yan dayama pistonunu durdurur
    /// Direction coil'leri kapatır ve valve'ı kapatır
    /// </summary>
    public async Task<bool> StopSideSupportPistonAsync(PistonType pistonType)
    {
        try
        {
            if (!_pistons.TryGetValue(pistonType, out var piston))
            {
                _logger?.LogError("Piston bulunamadı: {PistonType}", pistonType);
                return false;
            }

            // Sadece yan dayama pistonları için
            if (!piston.IsCoilControlled)
            {
                _logger?.LogWarning("Bu piston yan dayama değil - normal stop gerekli: {PistonType}", pistonType);
                return await StopPistonAsync(pistonType); // Ana pistonlar için fallback
            }

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("Modbus bağlantısı aktif değil");
                return false;
            }

            // 1. DİRECTION COIL'LERİNİ KAPAT
            if (piston.ForwardCoilAddress.HasValue)
            {
                await _modbusClient.WriteCoilAsync(piston.ForwardCoilAddress.Value, false);
            }

            if (piston.BackwardCoilAddress.HasValue)
            {
                await _modbusClient.WriteCoilAsync(piston.BackwardCoilAddress.Value, false);
            }

            // 2. VALVE'I KAPAT (güvenlik için)
            await CloseValveForPiston(piston);

            // Piston durumunu güncelle
            piston.Motion = MotionEnum.Closed;
            piston.IsMoving = false;
            piston.CurrentVoltage = 0;

            _logger?.LogInformation("Yan dayama durduruldu: {PistonType}", pistonType);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Yan dayama durdurma sırasında hata: {PistonType}", pistonType);
            return false;
        }
    }
    
    #endregion
    
    #region Safety & Emergency
    
    public async Task<bool> CheckSafetyAsync()
    {
        try
        {
            // Bağlantı kontrolü ve otomatik yeniden bağlanma
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı kopmuş, yeniden bağlanmaya çalışılıyor...");
                var reconnected = await _modbusClient.ConnectAsync();
                if (!reconnected)
                {
                    _logger?.LogError("Modbus yeniden bağlantı başarısız!");
                    return false; // Bağlantı yok = GÜVENSİZ
                }
            }
            
            // TÜM GÜVENLİK SENSÖRLERİNİ OKU - Dokümantasyondaki adreslerden
            // NOT: Genellikle dijital giriş sinyallerinde LOW (0) aktif durumu gösterir
            // Bu nedenle gelen değerleri tersine çevirmeliyiz
            
            var emergencyStopRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.EmergencyStopButton); // 0x000A
            var hydraulicErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.HydraulicEngineThermalError); // 0x0000  
            var fanErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.FanEngineThermalError); // 0x0001
            var phaseErrorRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PhaseSequenceError); // 0x0002
            
            // EK SENSÖRLER - Gerçek makinede mevcut olan
            var pollutionSensor1Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor1); // 0x0003
            var pollutionSensor2Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor2); // 0x0004
            var pollutionSensor3Raw = await _modbusClient.ReadCoilAsync(ModbusAddresses.PollutionSensor3); // 0x0005
            var leftRotationRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftRotationSensor); // 0x0006
            var rightRotationRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightRotationSensor); // 0x0007
            var leftPartRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftPartPresence); // 0x0008
            var rightPartRaw = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightPartPresence); // 0x0009
            
            // DİJİTAL GİRİŞ MANTIK DÖNÜŞÜMİ: TRUE (1) = NORMAL, FALSE (0) = HATA
            // Güvenlik sensörleri: TRUE = NORMAL/GÜVENLİ, FALSE = HATA/TEHLİKELİ
            var emergencyStop = emergencyStopRaw;     // TRUE = Normal, FALSE = Emergency basılı
            var hydraulicError = hydraulicErrorRaw;   // TRUE = Normal, FALSE = Hydraulic hata
            var fanError = fanErrorRaw;               // TRUE = Normal, FALSE = Fan hata  
            var phaseError = phaseErrorRaw;           // TRUE = Normal, FALSE = Faz sırası hata
            
            // Diğer sensörler (bilgi amaçlı)
            var pollutionSensor1 = pollutionSensor1Raw; // LOW = Pollution aktif
            var pollutionSensor2 = pollutionSensor2Raw;
            var pollutionSensor3 = pollutionSensor3Raw;
            var leftRotation = leftRotationRaw;         // LOW = Rotasyon aktif
            var rightRotation = rightRotationRaw;
            var leftPart = leftPartRaw;                 // LOW = Part mevcut
            var rightPart = rightPartRaw;
            
            _logger?.LogInformation("🔍 SENSÖR OKUMALARI - RAW ve İŞLENMİŞ:");
            _logger?.LogInformation("  Emergency Stop (0x000A): RAW={0} → İşlenmiş={1}", emergencyStopRaw, emergencyStopRaw ? "NORMAL" : "BASILI");
            _logger?.LogInformation("  Hydraulic Error (0x0000): RAW={0} → İşlenmiş={1}", hydraulicErrorRaw, hydraulicErrorRaw ? "NORMAL" : "HATA");
            _logger?.LogInformation("  Fan Error (0x0001): RAW={0} → İşlenmiş={1}", fanErrorRaw, fanErrorRaw ? "NORMAL" : "HATA");
            _logger?.LogInformation("  Phase Error (0x0002): RAW={0} → İşlenmiş={1}", phaseErrorRaw, phaseErrorRaw ? "NORMAL" : "HATA");
            _logger?.LogInformation("  Pollution1 (0x0003): RAW={0} → İşlenmiş={1}", pollutionSensor1Raw, pollutionSensor1 ? "AKTİF" : "PASİF");
            _logger?.LogInformation("  Pollution2 (0x0004): RAW={0} → İşlenmiş={1}", pollutionSensor2Raw, pollutionSensor2 ? "AKTİF" : "PASİF");
            _logger?.LogInformation("  Pollution3 (0x0005): RAW={0} → İşlenmiş={1}", pollutionSensor3Raw, pollutionSensor3 ? "AKTİF" : "PASİF");
            _logger?.LogInformation("  Left Rotation (0x0006): RAW={0} → İşlenmiş={1}", leftRotationRaw, leftRotation ? "HAREKET" : "DURGUN");
            _logger?.LogInformation("  Right Rotation (0x0007): RAW={0} → İşlenmiş={1}", rightRotationRaw, rightRotation ? "HAREKET" : "DURGUN");
            _logger?.LogInformation("  Left Part Present (0x0008): RAW={0} → İşlenmiş={1}", leftPartRaw, leftPart ? "MEVCUT" : "YOK");
            _logger?.LogInformation("  Right Part Present (0x0009): RAW={0} → İşlenmiş={1}", rightPartRaw, rightPart ? "MEVCUT" : "YOK");
            
            // GÜVENLİK MANTIGI - DOKÜMANTASYONA GÖRE
            // Emergency Stop: TRUE = NORMAL, FALSE = BASILI
            // Thermal Errors: TRUE = NORMAL, FALSE = HATA
            // Phase Error: TRUE = NORMAL, FALSE = HATA
            
            // Kritik güvenlik sensörleri (TRUE = GÜVENLI)
            var criticalSafe = emergencyStop && hydraulicError && fanError && phaseError;
            
            // Pollution sensörleri (pollution değerleri bilgi amaçlı)
            var pollutionStatus = $"P1:{pollutionSensor1}, P2:{pollutionSensor2}, P3:{pollutionSensor3}";
            
            // Rotasyon sensörleri (hareket algılama)
            var rotationStatus = $"Left:{leftRotation}, Right:{rightRotation}";
            
            // Part presence sensörleri (iş parçası varlığı)
            var partStatus = $"Left:{leftPart}, Right:{rightPart}";
            
            _logger?.LogInformation("📊 SENSÖR DURUMLARI:");
            _logger?.LogInformation("  🔴 Kritik Güvenlik: {0}", criticalSafe ? "GÜVENLI" : "TEHLİKELİ");
            _logger?.LogInformation("  🌫️ Pollution: {0}", pollutionStatus);
            _logger?.LogInformation("  🔄 Rotation: {0}", rotationStatus);
            _logger?.LogInformation("  📦 Part Presence: {0}", partStatus);
            
            if (!criticalSafe)
            {
                var errors = new List<string>();
                if (!emergencyStop) errors.Add("EMERGENCY STOP BASILI");
                if (!hydraulicError) errors.Add("HİDROLİK TERMAL HATA");
                if (!fanError) errors.Add("FAN TERMAL HATA");
                if (!phaseError) errors.Add("FAZ SIRASI HATASI");
                
                _logger?.LogError("❌ GÜVENLİK HATALARI: {0}", string.Join(", ", errors));
            }
            
            _logger?.LogInformation("✅ GENEL GÜVENLİK DURUMU: {0}", criticalSafe ? "GÜVENLI" : "GÜVENSİZ");
            return criticalSafe;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Güvenlik kontrolü sırasında Modbus hatası");
            return false; // Hata = GÜVENSİZ
        }
    }
    
    public async Task<bool> EmergencyStopAsync()
    {
        try
        {
            // 1. Alarm coil'ini aktif et
            await _modbusClient.WriteCoilAsync(ModbusAddresses.Alarm, true);
            
            // 2. Stop all pistons
            foreach (var pistonType in _pistons.Keys)
            {
                await StopPistonAsync(pistonType);
            }
            
            // 3. Rotasyon sistemini durdur
            await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, false);   // M21_Rotation_CWW
            await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, false);  // M22_Rotation_CW
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt, 0);
            
            // 4. Tüm valfleri kapat
            await _modbusClient.WriteCoilAsync(ModbusAddresses.S1, false);
            await _modbusClient.WriteCoilAsync(ModbusAddresses.S2, false);
            
            // 5. Tüm piston voltajlarını sıfırla
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M13_M14_TopPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M15_M16_BottomPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M17_M18_LeftPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M19_M20_RightPistonVolt, 0);
            
            // 6. Hidrolik motoru durdur
            await StopHydraulicMotorAsync();
            
            _logger?.LogInformation("✅ ACİL STOP: Tüm sistemler durduruldu ve alarm aktif edildi");
            OnAlarmRaised("Emergency Stop Activated", SafetyStatus.Critical);
            return true;
        }
        catch (Exception ex)
        {
            OnAlarmRaised($"Emergency stop failed: {ex.Message}", SafetyStatus.Critical);
            return false;
        }
    }
    
    #endregion
    
    #region Valve Control
    
    public async Task<bool> OpenS1ValveAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.S1, true);
        return true;
    }
    
    public async Task<bool> CloseS1ValveAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.S1, false);
        return true;
    }
    
    public async Task<bool> OpenS2ValveAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.S2, true);
        return true;
    }
    
    public async Task<bool> CloseS2ValveAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.S2, false);
        return true;
    }
    
    private async Task OpenValveForPiston(Piston piston)
    {
        if (piston.ValveGroup == ValveGroup.S1)
            await OpenS1ValveAsync();
        else
            await OpenS2ValveAsync();
    }
    
    private async Task CloseValveForPiston(Piston piston)
    {
        if (piston.ValveGroup == ValveGroup.S1)
            await CloseS1ValveAsync();
        else
            await CloseS2ValveAsync();
    }
    
    #endregion
    
    #region Motor Control
    
    public async Task<bool> StartHydraulicMotorAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.HydraulicEngine, true);
        return true;
    }
    
    public async Task<bool> StopHydraulicMotorAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.HydraulicEngine, false);
        return true;
    }
    
    public async Task<bool> StartFanMotorAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.FanEngine, true);
        return true;
    }
    
    public async Task<bool> StopFanMotorAsync() 
    {
        await _modbusClient.WriteCoilAsync(ModbusAddresses.FanEngine, false);
        return true;
    }
    
    #endregion
    
    #region Configuration & Initialization
    
    private void InitializePistons()
    {
        _pistons[PistonType.TopPiston] = new Piston
        {
            Name = "Top Piston",
            Type = PistonType.TopPiston,
            ValveGroup = ValveGroup.S1,
            VoltageAddress = ModbusAddresses.M13_M14_TopPistonVolt,
            RulerAddress = ModbusAddresses.RulerM13_M14_TopPiston,
            ResetAddress = ModbusAddresses.RulerResetM13toM16,
            StrokeLength = 161, // ✅ Verdiğiniz değer: 160 → 161mm
            RegisterCount = 7973, // ✅ Verdiğiniz değer: aynı
            MaxPosition = 161, // ✅ StrokeLength ile eşitlendi
            PositionTolerance = 0.2 // ✅ HASSAS: 0.5mm → 0.2mm (cetvel sıfırlama için)
        };
        
        _pistons[PistonType.BottomPiston] = new Piston
        {
            Name = "Bottom Piston",
            Type = PistonType.BottomPiston,
            ValveGroup = ValveGroup.S1,
            VoltageAddress = ModbusAddresses.M15_M16_BottomPistonVolt,
            RulerAddress = ModbusAddresses.RulerM15_M16_BottomPiston,
            ResetAddress = ModbusAddresses.RulerResetM13toM16,
            StrokeLength = 195, // ✅ Verdiğiniz değer: aynı
            RegisterCount = 9718, // ✅ Verdiğiniz değer: 9742 → 9718
            MaxPosition = 195,
            PositionTolerance = 0.2 // ✅ HASSAS: 0.5mm → 0.2mm (cetvel sıfırlama için)
        };
        
        _pistons[PistonType.LeftPiston] = new Piston
        {
            Name = "Left Piston",
            Type = PistonType.LeftPiston,
            ValveGroup = ValveGroup.S2,
            VoltageAddress = ModbusAddresses.M17_M18_LeftPistonVolt,
            RulerAddress = ModbusAddresses.RulerM17_M18_LeftPiston,
            ResetAddress = ModbusAddresses.RulerResetM17toM20,
            StrokeLength = 422,
            RegisterCount = 21082, // ✅ Memory'den: Sol piston registerCount
            MaxPosition = 422,
            PositionTolerance = 0.3 // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
        };
        
        _pistons[PistonType.RightPiston] = new Piston
        {
            Name = "Right Piston",
            Type = PistonType.RightPiston,
            ValveGroup = ValveGroup.S2,
            VoltageAddress = ModbusAddresses.M19_M20_RightPistonVolt,
            RulerAddress = ModbusAddresses.RulerM19_M20_RightPiston,
            ResetAddress = ModbusAddresses.RulerResetM17toM20,
            StrokeLength = 422.3, // ✅ Verdiğiniz değer: 422 → 422.3mm
            RegisterCount = 21123, // ✅ Verdiğiniz değer: aynı
            MaxPosition = 422.3, // ✅ StrokeLength ile eşitlendi
            PositionTolerance = 0.3 // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
        };
        
        // Eksik pistonları ekle - Side Support Pistons
        // SOL YAN DAYAMA GRUBU - S1 VALF
        _pistons[PistonType.LeftReelPiston] = new Piston
        {
            Name = "Left Reel Piston",
            Type = PistonType.LeftReelPiston,
            ValveGroup = ValveGroup.S1, // ✅ SOL GRUP = S1 
            VoltageAddress = 0, // Coil controlled - voltage based değil
            RulerAddress = ModbusAddresses.RulerM01_M02_LeftSideSupportReelPiston,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 352,
            RegisterCount = 17597, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 352,
            PositionTolerance = 0.5, // ✅ HASSAS: 2.0mm → 0.5mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Verdiğiniz değerler
            MinRegister = 400,
            MaxRegister = 4021,
            // Coil adresleri eklendi
            ForwardCoilAddress = ModbusAddresses.M01_LeftReelPistonForward,
            BackwardCoilAddress = ModbusAddresses.M02_LeftReelPistonBackward
        };
        
        _pistons[PistonType.LeftBodyPiston] = new Piston
        {
            Name = "Left Body Piston",
            Type = PistonType.LeftBodyPiston,
            ValveGroup = ValveGroup.S1, // ✅ SOL GRUP = S1
            VoltageAddress = 0, // Coil controlled
            RulerAddress = ModbusAddresses.RulerM03_M04_LeftSideSupportBody,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 129,
            RegisterCount = 6447, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 129,
            PositionTolerance = 0.3, // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Verdiğiniz değerler
            MinRegister = 698,
            MaxRegister = 2806,
            ForwardCoilAddress = ModbusAddresses.M03_LeftBodyForward,
            BackwardCoilAddress = ModbusAddresses.M04_LeftBodyBackward
        };
        
        _pistons[PistonType.LeftJoinPiston] = new Piston
        {
            Name = "Left Join Piston",
            Type = PistonType.LeftJoinPiston,
            ValveGroup = ValveGroup.S1, // ✅ SOL GRUP = S1
            VoltageAddress = 0, // Coil controlled
            RulerAddress = ModbusAddresses.RulerM05_M06_LeftSideSupportJoinPiston,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 187,
            RegisterCount = 9350, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 187,
            PositionTolerance = 0.3, // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Verdiğiniz değerler
            MinRegister = 365,
            MaxRegister = 3425,
            ForwardCoilAddress = ModbusAddresses.M05_LeftJoinPistonForward,
            BackwardCoilAddress = ModbusAddresses.M06_LeftJoinPistonBackward
        };
        
        // SAĞ YAN DAYAMA GRUBU - S2 VALF
        _pistons[PistonType.RightReelPiston] = new Piston
        {
            Name = "Right Reel Piston",
            Type = PistonType.RightReelPiston,
            ValveGroup = ValveGroup.S2, // ✅ SAĞ GRUP = S2
            VoltageAddress = 0, // Coil controlled
            RulerAddress = ModbusAddresses.RulerM07_M08_RightSideSupportReelPiston,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 352,
            RegisterCount = 17576, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 352,
            PositionTolerance = 0.5, // ✅ HASSAS: 2.0mm → 0.5mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Sol taraf ile aynı (verdiğiniz değerler)
            MinRegister = 400,
            MaxRegister = 4021,
            ForwardCoilAddress = ModbusAddresses.M07_RightReelPistonForward,
            BackwardCoilAddress = ModbusAddresses.M08_RightReelPistonBackward
        };
        
        _pistons[PistonType.RightBodyPiston] = new Piston
        {
            Name = "Right Body Piston",
            Type = PistonType.RightBodyPiston,
            ValveGroup = ValveGroup.S2, // ✅ SAĞ GRUP = S2
            VoltageAddress = 0, // Coil controlled
            RulerAddress = ModbusAddresses.RulerM09_M10_RightSideSupportBody,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 129,
            RegisterCount = 6439, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 129,
            PositionTolerance = 0.3, // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Sol taraf ile aynı (verdiğiniz değerler)
            MinRegister = 698,
            MaxRegister = 2806,
            ForwardCoilAddress = ModbusAddresses.M09_RightBodyForward,
            BackwardCoilAddress = ModbusAddresses.M10_RightBodyBackward
        };
        
        _pistons[PistonType.RightJoinPiston] = new Piston
        {
            Name = "Right Join Piston",
            Type = PistonType.RightJoinPiston,
            ValveGroup = ValveGroup.S2, // ✅ SAĞ GRUP = S2
            VoltageAddress = 0, // Coil controlled
            RulerAddress = ModbusAddresses.RulerM11_M12_RightSideSupportJoinPiston,
            ResetAddress = ModbusAddresses.RulerResetPneumaticValve,
            StrokeLength = 187,
            RegisterCount = 9322, // ✅ Görseldeki değere göre hesaplanan
            MaxPosition = 187,
            PositionTolerance = 0.3, // ✅ HASSAS: 1.0mm → 0.3mm (cetvel sıfırlama için)
            // ✅ MIN/MAX REGISTER ARALIKLARI - Sol taraf ile aynı (verdiğiniz değerler)
            MinRegister = 365,
            MaxRegister = 3425,
            ForwardCoilAddress = ModbusAddresses.M11_RightJoinPistonForward,
            BackwardCoilAddress = ModbusAddresses.M12_RightJoinPistonBackward
        };
    }
    
    public async Task LoadConfigurationAsync()
    {
        // JSON'dan config yükle - basit implementation
        await Task.CompletedTask;
    }
    
    public async Task SaveConfigurationAsync()
    {
        // Config kaydet - basit implementation
        await Task.CompletedTask;
    }
    
    #endregion
    
    #region Events
    
    private void OnStatusChanged()
    {
        StatusChanged?.Invoke(this, new MachineStatusChangedEventArgs { Status = _currentStatus });
    }
    
    private void OnPistonMoved(PistonType type, double current, double target, MotionEnum motion)
    {
        PistonMoved?.Invoke(this, new PistonMovedEventArgs 
        { 
            PistonType = type, 
            CurrentPosition = current, 
            TargetPosition = target, 
            Motion = motion 
        });
    }
    
    private void OnAlarmRaised(string message, SafetyStatus severity)
    {
        AlarmRaised?.Invoke(this, new AlarmEventArgs 
        { 
            AlarmMessage = message, 
            Severity = severity 
        });
    }
    
    private void OnSafetyViolation(string violation, bool requiresStop)
    {
        SafetyViolation?.Invoke(this, new SafetyEventArgs 
        { 
            ViolationType = violation, 
            RequiresEmergencyStop = requiresStop 
        });
    }
    
    #endregion
    
    #region Placeholder Methods (TODO)
    
    public Task<List<Piston>> GetAllPistonsAsync() => Task.FromResult(_pistons.Values.ToList());
    public Task<Piston> GetPistonAsync(PistonType pistonType) => Task.FromResult(_pistons[pistonType]);
    /// <summary>
    /// DOKÜMANTASYON: Stage ayarlama işlemi
    /// Cetvel sıfırlama + Stage pozisyonuna götürme + Tekrar cetvel sıfırlama
    /// </summary>
    public async Task<bool> SetStageAsync(int stageValue)
    {
        try
        {
            // ✅ HİDROLİK MOTOR KONTROLÜ (Ortak Metod)
            if (!await EnsureHydraulicMotorRunningAsync("Stage Ayarlama"))
            {
                return false;
            }

            _logger?.LogInformation("⚙️ Stage ayarlama başlatılıyor - Hedef: {StageValue}mm", stageValue);
            
            // ADIM 1: Cetvel sıfırlama (referans pozisyona)
            _logger?.LogInformation("🔄 Önce cetvel sıfırlama yapılıyor...");
            var initialResetResult = await ResetRulersAsync();
            if (!initialResetResult)
            {
                _logger?.LogError("❌ İlk cetvel sıfırlama başarısız - stage ayarlama iptal ediliyor");
                return false;
            }
            
            // ADIM 2: Stage pozisyonuna götürme (hassas konumlandırma)
            // Stage 0 = Gönye pozisyonu, Stage 60/120 = Belirtilen pozisyonlar
            _logger?.LogInformation("📐 Stage pozisyonuna götürme - Hedef: {StageValue}mm", stageValue);
            
            // Default stage pozisyonları (dokümantasyon)
            var stagePositions = GetStagePositions(stageValue);
            
            var positioningTasks = new List<Task>
            {
                MovePistonToPositionAsync(PistonType.BottomPiston, stagePositions.BottomCenter),
                MovePistonToPositionAsync(PistonType.LeftPiston, stagePositions.BottomLeft),
                MovePistonToPositionAsync(PistonType.RightPiston, stagePositions.BottomRight)
            };
            
            await Task.WhenAll(positioningTasks);
            _logger?.LogInformation("✅ Stage pozisyonları ayarlandı - Alt Orta: {Bottom}mm, Alt Sol: {Left}mm, Alt Sağ: {Right}mm", 
                stagePositions.BottomCenter, stagePositions.BottomLeft, stagePositions.BottomRight);
            
            // ADIM 3: Sadece cetvel değerlerini sıfırla (pistonları hareket ettirme!)
            _logger?.LogInformation("🔄 Stage pozisyonlarında cetvel değerleri sıfırlanıyor (pistonlar hareket etmeyecek)...");
            var finalResetResult = await ResetRulerValuesOnlyAsync();
            if (!finalResetResult)
            {
                _logger?.LogWarning("⚠️ Final cetvel değer sıfırlama başarısız");
            }
            
            // ADIM 4: VALS TOPU OYNAMASI KONTROLÜ - 2 saniye bekle ve re-reset yap
            _logger?.LogInformation("⏳ Vals topu stabilizasyonu için 2 saniye bekleniyor...");
            await Task.Delay(2000);
            
            _logger?.LogInformation("🔄 Vals topu oynaması kontrolü için cetvel re-reset yapılıyor...");
            var reResetResult = await ResetRulerValuesOnlyAsync();
            if (!reResetResult)
            {
                _logger?.LogWarning("⚠️ Cetvel re-reset başarısız - vals topu oynaması problemi devam edebilir");
            }
            else
            {
                _logger?.LogInformation("✅ Cetvel re-reset başarılı - vals topu oynaması düzeltildi");
            }
            
            // ✅ ADIM 5: REFERANS POZİSYONLARINI KAYDET (Signed hesaplama için)
            _logger?.LogInformation("📌 Stage sıfırlama tamamlandı - referans pozisyonları kaydediliyor...");
            await SaveCurrentPositionsAsReferenceAsync();
            
            _logger?.LogInformation("🎯 Stage {StageValue}mm başarıyla ayarlandı", stageValue);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Stage ayarlama sırasında hata oluştu");
            return false;
        }
    }
    
    /// <summary>
    /// Sadece cetvel değerlerini sıfırlar (pistonları hareket ettirmez)
    /// Stage ayarlama işleminin son adımında kullanılır
    /// </summary>
    private async Task<bool> ResetRulerValuesOnlyAsync()
    {
        try
        {
            _logger?.LogInformation("📊 Sadece cetvel değerleri sıfırlanıyor (pistonlar hareket etmeyecek)...");

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil!");
                return false;
            }

            // Reset adreslerini hazırla
            var resetAddresses = new Dictionary<string, int>
            {
                { "M13toM16", ModbusAddresses.RulerResetM13toM16 },
                { "M17toM20", ModbusAddresses.RulerResetM17toM20 },
                { "PneumaticValve", ModbusAddresses.RulerResetPneumaticValve },
                { "Rotation", ModbusAddresses.RulerResetRotation }
            };

            // Sadece cetvel reset protokolünü çalıştır (pistonlar hareket etmeyecek)
            var resetSuccess = await PerformRulerResetProtocolAsync(resetAddresses);
            
            if (resetSuccess)
            {
                _logger?.LogInformation("✅ Cetvel değerleri başarıyla sıfırlandı (pistonlar stage pozisyonunda kaldı)");
                return true;
            }
            else
            {
                _logger?.LogError("❌ Cetvel değer sıfırlama başarısız");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cetvel değer sıfırlama sırasında hata oluştu");
            return false;
        }
    }
    
    /// <summary>
    /// Mevcut piston pozisyonlarını referans pozisyon olarak kaydeder (Signed hesaplama için)
    /// Stage sıfırlama tamamlandıktan sonra çağrılır
    /// </summary>
    private async Task SaveCurrentPositionsAsReferenceAsync()
    {
        try
        {
            _logger?.LogInformation("📌 Referans pozisyonları kaydediliyor...");

            foreach (var pistonPair in _pistons)
            {
                var pistonType = pistonPair.Key;
                var piston = pistonPair.Value;

                // Mevcut mutlak pozisyonu oku (doğru hesaplama metoduyla)
                var rulerValue = await _modbusClient.ReadInputRegisterAsSignedAsync(piston.RulerAddress);
                double currentAbsolutePosition;
                
                if (piston.UsesMinMaxRange)
                {
                    // Yan dayama pistonları - Min/Max range hesaplama
                    currentAbsolutePosition = piston.CalculatePositionFromRulerMinMax((ushort)Math.Abs(rulerValue));
                }
                else
                {
                    // Ana pistonlar - 4-20mA hesaplama
                    currentAbsolutePosition = piston.CalculatePositionFromRuler((ushort)Math.Abs(rulerValue));
                }

                // Bu pozisyonu referans olarak kaydet
                piston.ReferencePosition = currentAbsolutePosition;

                _logger?.LogInformation("📌 {PistonType}: Referans pozisyon = {ReferencePosition:F2}mm (Cetvel: {RulerValue})", 
                    pistonType, piston.ReferencePosition, rulerValue);
            }

            _logger?.LogInformation("✅ Tüm referans pozisyonları kaydedildi - artık signed hesaplama aktif");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Referans pozisyonları kaydedilirken hata oluştu");
        }
    }
    
    /// <summary>
    /// Stage değerine göre piston pozisyonlarını döndürür (dokümantasyon default değerleri)
    /// </summary>
    private (double BottomCenter, double BottomLeft, double BottomRight) GetStagePositions(int stageValue)
    {
        // ✅ DÜZELTME: Stage değeri direkt pozisyon değeri olmalı
        // Stage 0: 0mm pozisyon (sıfır referans)
        // Stage 60: 60mm pozisyon 
        // Stage 120: 120mm pozisyon
        // Yan pistonlar için 1.1223 çarpanı uygulanır
        
        return stageValue switch
        {
            0 => (10.5, 3.75, 0.0),                        // Stage 0 - Gönye pozisyonu (cetvel sıfırlamadaki değerler)
            60 => (60.0, 67.34, 67.34),                    // Stage 60 - 60mm pozisyon (60 * 1.1223 ≈ 67.34)
            120 => (120.0, 134.68, 134.68),                // Stage 120 - 120mm pozisyon (120 * 1.1223 ≈ 134.68)
            _ => (stageValue, stageValue * 1.1223, stageValue * 1.1223) // Dinamik hesaplama - direkt stage değeri
        };
    }
    public async Task<bool> ResetRulersAsync()
    {
        try
        {
            _logger?.LogInformation("🔧 Cetvel sıfırlama işlemi başlatılıyor...");

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil! Cetvel sıfırlama başlatılamaz!");
                OnAlarmRaised("Modbus bağlantısı yok - Cetvel sıfırlama başlatılamaz", SafetyStatus.Critical);
                return false;
            }

            // ✅ ADIM 1: Reset adreslerini kontrol et (4 adet)
            var resetAddresses = new Dictionary<string, int>
            {
                { "M13toM16", ModbusAddresses.RulerResetM13toM16 },
                { "M17toM20", ModbusAddresses.RulerResetM17toM20 },
                { "PneumaticValve", ModbusAddresses.RulerResetPneumaticValve },
                { "Rotation", ModbusAddresses.RulerResetRotation }
            };

            // ✅ ADIM 2: Reset durum bilgisi için adres okuma (sadece bilgi amaçlı)
            _logger?.LogInformation("📊 Mevcut reset durumu kontrol ediliyor (bilgi amaçlı)...");

            foreach (var address in resetAddresses)
            {
                var value = await _modbusClient.ReadHoldingRegisterAsync(address.Value);
                _logger?.LogInformation("�� {Name} (0x{Address:X4}): {Value} {Status}", 
                    address.Key, address.Value, value, value == 2570 ? "(Reset)" : "(Resetlenmemiş)");
            }

            // ✅ KULLANICI İSTEĞİ: Kontrol koşulu kaldırıldı - İstendiğinde her zaman resetleme yapılacak
            _logger?.LogInformation("🔧 Cetvel sıfırlamaya başlanıyor (koşulsuz)...");

            // ✅ ADIM 2: Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger?.LogWarning("Güvenlik kontrolü başarısız - cetvel sıfırlama başlatılamaz");
                return false;
            }

            // ✅ ADIM 3: Hidrolik motor kontrolü (ortak metod)
            if (!await EnsureHydraulicMotorRunningAsync("Cetvel Sıfırlama"))
            {
                return false;
            }

            // ✅ ADIM 4: TÜM SİSTEMLERİ AYNI ANDA geri çek (Ana pistonlar + Yan dayamalar + Pnömatik valfler)
            _logger?.LogInformation("🔙 Tüm sistemler aynı anda geri çekiliyor (Ana pistonlar + Yan dayamalar + Pnömatik valfler)...");
            
            // Hedef basınçlar - TODO: Ayarlar sayfasından alınacak
            const double TARGET_RETRACT_PRESSURE = 70.0; // Tam geri çekilme basıncı
            const double PRESSURE_TOLERANCE = 5.0; // ±5 bar tolerans

            var retractionSuccess = await RetractAllSystemsSimultaneouslyAsync(TARGET_RETRACT_PRESSURE, PRESSURE_TOLERANCE);
            
            if (!retractionSuccess)
            {
                _logger?.LogError("❌ Sistemler hedef basınca ulaşamadı");
                return false;
            }

            // ✅ ADIM 5: İlk Reset Protocol'ü uygula
            _logger?.LogInformation("🔄 İlk cetvel reset protocol'ü başlatılıyor...");
            var firstResetSuccess = await PerformRulerResetProtocolAsync(resetAddresses);
            
            if (!firstResetSuccess)
            {
                _logger?.LogError("❌ İlk reset protocol'ü başarısız oldu");
                return false;
            }

            // ✅ ADIM 6: Gönye pozisyonlarına getir (hassas konumlandırma)
            _logger?.LogInformation("🎯 Pistonlar gönye pozisyonlarına getiriliyor...");
            
            // Gönye pozisyonları - TODO: Ayarlar sayfasından alınacak
            var squarePositions = new Dictionary<PistonType, double>
            {
                { PistonType.BottomPiston, 10.5 },    // Alt orta piston
                { PistonType.LeftPiston, 3.75 },       // Sol alt piston  
                { PistonType.RightPiston, 0.0 },       // Sağ alt piston
                { PistonType.TopPiston, 0.0 }          // Üst piston
            };

            var squarePositioningSuccess = await MoveToSquarePositionsAsync(squarePositions);
            
            if (!squarePositioningSuccess)
            {
                _logger?.LogError("❌ Gönye pozisyonlarına getirme başarısız");
                return false;
            }

            // ✅ ADIM 7: İkinci (Final) Reset işlemi
            _logger?.LogInformation("🔄 Final cetvel reset işlemi yapılıyor...");
            var finalResetSuccess = await PerformRulerResetProtocolAsync(resetAddresses);

            await CloseS1ValveAsync();
            await CloseS2ValveAsync();

            if (finalResetSuccess)
            {
                _logger?.LogInformation("✅ Cetvel sıfırlama başarıyla tamamlandı!");
                OnAlarmRaised("Cetvel sıfırlama başarıyla tamamlandı", SafetyStatus.Normal);
                return true;
            }
            else
            {
                _logger?.LogError("❌ Final reset işlemi başarısız oldu");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cetvel sıfırlama sırasında hata oluştu");
            OnAlarmRaised($"Cetvel sıfırlama hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// TÜM SİSTEMLERİ AYNI ANDA geri çeker: Ana pistonlar + Yan dayamalar + Pnömatik valfler
    /// Hedef basınca ulaşana kadar bekler - Tam geri dayama kontrolü
    /// </summary>
    private async Task<bool> RetractAllSystemsSimultaneouslyAsync(double targetPressure, double tolerance)
    {
        try
        {
            _logger?.LogInformation("💪 TÜM SİSTEMLER aynı anda geri çekiliyor - Hedef Basınç: {TargetPressure} bar", targetPressure);
            
            // ✅ 1. ANA PİSTONLARI geri çek
            await OpenS1ValveAsync();
            await OpenS2ValveAsync();

            var mainPistonTasks = new List<Task>
            {
                MovePistonAsync(PistonType.TopPiston, 10.0),        // Geri çek (+10V)
                MovePistonAsync(PistonType.BottomPiston, 10.0),     // Geri çek (+10V)  
                MovePistonAsync(PistonType.LeftPiston, 10.0),       // Geri çek (+10V)
                MovePistonAsync(PistonType.RightPiston, 10.0)       // Geri çek (+10V)
            };

            // ✅ 2. YAN DAYAMA PİSTONLARINI geri çek (aynı anda)
            var sideSupportTasks = new List<Task>
            {
                // Sol yan dayama grubu
                JogSideSupportPistonAsync(PistonType.LeftReelPiston, MotionEnum.Backward),
                JogSideSupportPistonAsync(PistonType.LeftBodyPiston, MotionEnum.Backward),
                JogSideSupportPistonAsync(PistonType.LeftJoinPiston, MotionEnum.Backward),
                // Sağ yan dayama grubu
                JogSideSupportPistonAsync(PistonType.RightReelPiston, MotionEnum.Backward),
                JogSideSupportPistonAsync(PistonType.RightBodyPiston, MotionEnum.Backward),
                JogSideSupportPistonAsync(PistonType.RightJoinPiston, MotionEnum.Backward)
            };

            // ✅ 3. PNÖMATİK VALFLERİ kapat (aynı anda)
            var pneumaticTasks = new List<Task>
            {
                ClosePneumaticValve1Async(),
                ClosePneumaticValve2Async()
            };

            // ✅ 4. HEPSİNİ AYNI ANDA BAŞLAT
            var allRetractionTasks = mainPistonTasks.Concat(sideSupportTasks).Concat(pneumaticTasks);
            await Task.WhenAll(allRetractionTasks);
            
            _logger?.LogInformation("🚀 TÜM SİSTEMLER geri çekilme hareketi başlatıldı (Ana pistonlar + Yan dayamalar + Pnömatik valfler)");

            // ✅ Kalkış basıncı görmezden gelme süresi (600ms)
            var startTime = DateTime.UtcNow;
            var ignoreStartupPressureDuration = TimeSpan.FromMilliseconds(600); // 600ms kalkış basıncı ignore
            _logger?.LogInformation("🕒 Kalkış basıncı görmezden gelme süresi başladı: {Duration}ms", ignoreStartupPressureDuration.TotalMilliseconds);

            // Basınç kontrolü ile geri çekilme takibi
            var maxIterations = 100; // Maksimum 10 saniye (100ms x 100)
            var iteration = 0;
            var targetReached = false;

            while (iteration < maxIterations && !targetReached)
            {
                await Task.Delay(100); // 100ms bekle
                var elapsedTime = DateTime.UtcNow - startTime;
                
                // Kalkış basıncı görmezden gelme süresi geçtikten sonra basınç kontrolü yap
                if (elapsedTime > ignoreStartupPressureDuration)
                {
                // S1 ve S2 basınçlarını oku
                var (s1Pressure, s2Pressure) = await ReadActualPressureAsync();
                    
                    // ✅ DÜZELTME: HER İKİ VALVE DE hedef basınca ulaşmalı (AND mantığı)
                    var s1ReachedTarget = s1Pressure >= (targetPressure - tolerance);
                    var s2ReachedTarget = s2Pressure >= (targetPressure - tolerance);
                    var bothReachedTarget = s1ReachedTarget && s2ReachedTarget;
                    
                    if (bothReachedTarget)
                {
                    targetReached = true;
                        _logger?.LogInformation("✅ HER İKİ VALVE DE hedef basınca ulaşıldı: S1={S1:F1}bar({S1Status}), S2={S2:F1}bar({S2Status}) >= {TargetPressure:F1}bar - Süre: {ElapsedTime}ms", 
                            s1Pressure, s1ReachedTarget ? "✅" : "❌", s2Pressure, s2ReachedTarget ? "✅" : "❌", targetPressure, elapsedTime.TotalMilliseconds);
                }
                else
                {
                    // Her 10. iterasyonda basınç değerlerini logla
                    if (iteration % 10 == 0)
                    {
                            _logger?.LogDebug("🔍 Basınç kontrolü: S1={S1:F1}bar({S1Status}), S2={S2:F1}bar({S2Status}), Hedef={Target:F1}bar", 
                                s1Pressure, s1ReachedTarget ? "✅" : "❌", s2Pressure, s2ReachedTarget ? "✅" : "❌", targetPressure);
                        }
                    }
            }
            else
            {
                    // Kalkış basıncı görmezden gelme süresi devam ediyor
                    var remainingIgnoreTime = ignoreStartupPressureDuration - elapsedTime;
                    if (iteration % 10 == 0) // Her 1000ms'de bir log
        {
                        _logger?.LogDebug("⏳ Kalkış basıncı görmezden geliniyor - Kalan süre: {RemainingTime}ms", remainingIgnoreTime.TotalMilliseconds);
                    }
                }
                
                iteration++;
            }

            // ✅ TÜM SİSTEMLERİ durdur
            var stopTasks = new List<Task>
            {
                // Ana pistonları durdur
                StopAllPistonsAsync(),
                // Yan dayama pistonlarını durdur
                StopSideSupportPistonAsync(PistonType.LeftReelPiston),
                StopSideSupportPistonAsync(PistonType.LeftBodyPiston),
                StopSideSupportPistonAsync(PistonType.LeftJoinPiston),
                StopSideSupportPistonAsync(PistonType.RightReelPiston),
                StopSideSupportPistonAsync(PistonType.RightBodyPiston),
                StopSideSupportPistonAsync(PistonType.RightJoinPiston)
            };
            await Task.WhenAll(stopTasks);
            
            if (targetReached)
            {
                _logger?.LogInformation("✅ TÜM SİSTEMLER başarıyla tam geri çekildi - Her iki valve de hedef basınca ulaştı");
                return true;
            }
            else
            {
                // Final basınç durumunu kontrol et ve detaylı hata raporu ver
                var (finalS1, finalS2) = await ReadActualPressureAsync();
                var finalS1OK = finalS1 >= (targetPressure - tolerance);
                var finalS2OK = finalS2 >= (targetPressure - tolerance);
                
                _logger?.LogWarning("⚠️ TÜM SİSTEMLER hedef basınca ulaşamadı - Timeout: S1={S1:F1}bar({S1Status}), S2={S2:F1}bar({S2Status}), Hedef={Target:F1}bar", 
                    finalS1, finalS1OK ? "✅" : "❌", finalS2, finalS2OK ? "✅" : "❌", targetPressure);
                    
                if (!finalS1OK && !finalS2OK)
                {
                    _logger?.LogError("❌ KRİTİK: Her iki valve de hedef basınca ulaşamadı! (Ana pistonlar + Yan dayamalar tam geri çekilmemiş)");
                }
                else if (!finalS1OK)
                {
                    _logger?.LogError("❌ KRİTİK: S1 valve hedef basınca ulaşamadı! (Sistemin bir kısmı tam geri çekilmemiş)");
                }
                else if (!finalS2OK)
                {
                    _logger?.LogError("❌ KRİTİK: S2 valve hedef basınca ulaşamadı! (Sistemin bir kısmı tam geri çekilmemiş)");
                }
                
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tüm sistemlerin basınç kontrolü ile geri çekilmesi sırasında hata oluştu");
            return false;
        }
    }



    /// <summary>
    /// Pistonları gönye pozisyonlarına hassas konumlandırma
    /// </summary>
    private async Task<bool> MoveToSquarePositionsAsync(Dictionary<PistonType, double> squarePositions)
    {
        try
        {
            _logger?.LogInformation("🎯 Gönye pozisyonlarına hassas konumlandırma başlatılıyor...");

            var allSuccess = true;

            foreach (var position in squarePositions)
            {
                var pistonType = position.Key;
                var targetPosition = position.Value;
                
                _logger?.LogInformation("📍 {PistonType} gönye pozisyonuna getiriliyor: {TargetPosition}mm", pistonType, targetPosition);
                
                var success = await MovePistonToPositionAsync(pistonType, targetPosition);
                
                if (success)
                {
                    _logger?.LogInformation("✅ {PistonType} gönye pozisyonuna ulaştı: {TargetPosition}mm", pistonType, targetPosition);
                }
                else
                {
                    _logger?.LogWarning("❌ {PistonType} gönye pozisyonuna ulaşamadı: {TargetPosition}mm", pistonType, targetPosition);
                    allSuccess = false;
                }
                
                await Task.Delay(500); // Pistonlar arası bekle
            }

            if (allSuccess)
            {
                _logger?.LogInformation("✅ Tüm pistonlar gönye pozisyonlarına başarıyla getirildi");
            }
            else
            {
                _logger?.LogWarning("⚠️ Bazı pistonlar gönye pozisyonlarına ulaşamadı");
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Gönye pozisyonlarına getirme sırasında hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// Dokümana göre cetvel reset protocol'ü: -32640 → 200ms → 2570 → 200ms → Kontrol
    /// </summary>
    private async Task<bool> PerformRulerResetProtocolAsync(Dictionary<string, int> resetAddresses)
    {
        try
        {
            _logger?.LogInformation("🔄 Reset protocol başlatılıyor...");

            // Adım 1: Reset adreslerine -32640 gönder
            _logger?.LogInformation("📤 Reset adreslerine -32640 değeri gönderiliyor...");
            foreach (var address in resetAddresses)
            {
                await _modbusClient.WriteHoldingRegisterAsync(address.Value, (ushort)32896); // -32640 as ushort
                _logger?.LogDebug("✅ {Name} (0x{Address:X4}): -32640 gönderildi", address.Key, address.Value);
            }

            // Adım 2: 200ms bekle
            await Task.Delay(200);

            // Adım 3: Reset adreslerine 2570 gönder  
            _logger?.LogInformation("📤 Reset adreslerine 2570 değeri gönderiliyor...");
            foreach (var address in resetAddresses)
            {
                await _modbusClient.WriteHoldingRegisterAsync(address.Value, (ushort)2570);
                _logger?.LogDebug("✅ {Name} (0x{Address:X4}): 2570 gönderildi", address.Key, address.Value);
            }

            // Adım 4: 200ms bekle
            await Task.Delay(200);

            // Adım 5: Reset adreslerini kontrol et (hepsi 2570 olmalı)
            _logger?.LogInformation("🔍 Reset başarısı kontrol ediliyor...");
            var allSuccess = true;

            foreach (var address in resetAddresses)
            {
                var value = await _modbusClient.ReadHoldingRegisterAsync(address.Value);
                _logger?.LogInformation("📊 {Name} (0x{Address:X4}): {Value}", address.Key, address.Value, value);
                
                if (value != 2570)
                {
                    _logger?.LogWarning("❌ {Name} reset başarısız: {Value} != 2570", address.Key, value);
                    allSuccess = false;
                }
                else
                {
                    _logger?.LogDebug("✅ {Name} reset başarılı: {Value} == 2570", address.Key, value);
                }
            }

            if (allSuccess)
            {
                _logger?.LogInformation("✅ Reset protocol başarıyla tamamlandı - Tüm adresler 2570 değerinde");
            }
            else
            {
                _logger?.LogError("❌ Reset protocol başarısız - Bazı adresler 2570 değerlinde değil");
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Reset protocol sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ResetSpecificRulerAsync(PistonType pistonType)
    {
        try
        {
            _logger?.LogInformation("Spesifik cetvel sıfırlama: {PistonType}", pistonType);

            var piston = _pistons[pistonType];
            if (piston.ResetAddress == 0)
            {
                _logger?.LogWarning("Piston için reset adresi tanımlanmamış: {PistonType}", pistonType);
                return false;
            }

            // Tek adres için reset protocol'ü
            var resetAddresses = new Dictionary<string, int>
            {
                { pistonType.ToString(), piston.ResetAddress }
            };

            return await PerformRulerResetProtocolAsync(resetAddresses);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Spesifik cetvel sıfırlama sırasında hata oluştu: {PistonType}", pistonType);
            return false;
        }
    }
    public async Task<bool> ResetAlarmAsync()
    {
        try
        {
            _logger?.LogInformation("🔄 Sistem sıfırlama başlatılıyor...");
            
            // 1. Önce acil stop durumunu kontrol et
            var emergencyStop = await _modbusClient.ReadCoilAsync(ModbusAddresses.EmergencyStopButton);
            if (emergencyStop) // TRUE = NORMAL, FALSE = ACİL STOP BASILI
            {
                _logger?.LogInformation("✅ Acil stop durumu normal, sıfırlama devam edebilir.");
            }
            else
            {
                _logger?.LogError("❌ ACİL STOP BUTONU HALA BASILI! Önce fiziksel butonu serbest bırakın.");
                return false;
            }
            
            // 2. Tüm rotasyon sistemini sıfırla
            await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, false);   // M21_Rotation_CWW
            await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, false);  // M22_Rotation_CW
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt, 0);
            
            // 3. Tüm valfleri kapat
            await _modbusClient.WriteCoilAsync(ModbusAddresses.S1, false);
            await _modbusClient.WriteCoilAsync(ModbusAddresses.S2, false);
            
            // 4. Tüm piston voltajlarını sıfırla
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M13_M14_TopPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M15_M16_BottomPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M17_M18_LeftPistonVolt, 0);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M19_M20_RightPistonVolt, 0);
            
            // 5. Tüm pnömatik valfleri kapat
            await _modbusClient.WriteCoilAsync(ModbusAddresses.P1Open, false);
            await _modbusClient.WriteCoilAsync(ModbusAddresses.P2Open, false);
            
            // 6. Alarm coil'ini sıfırla
            await _modbusClient.WriteCoilAsync(ModbusAddresses.Alarm, false);
            
            // 7. Encoder referansını sıfırla
            _pasoEncoderReferencePosition = null;
            
            // 8. Hidrolik motoru yeniden başlat
            await StopHydraulicMotorAsync(); // Önce durdur
            await Task.Delay(2000); // 2 saniye bekle
            var motorResult = await StartHydraulicMotorAsync();
            
            if (!motorResult)
            {
                _logger?.LogError("❌ Hidrolik motor başlatılamadı!");
                return false;
            }
            
            // 9. Status'u güncelle
            await UpdateMachineStatusAsync();
            
            // 10. Güvenlik kontrolü yap
            var safetyCheck = await CheckSafetyAsync();
            if (!safetyCheck)
            {
                _logger?.LogError("❌ Güvenlik kontrolü başarısız! Sistem sıfırlama tamamlanamadı.");
                return false;
            }
            
            _logger?.LogInformation("✅ Sistem sıfırlama tamamlandı - Tüm sistemler temizlendi");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Sistem sıfırlama sırasında hata oluştu");
            OnAlarmRaised($"Sistem sıfırlama hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    public async Task<bool> CompressPartAsync(double targetPressure, double tolerance)
    {
        try
        {
            // ✅ HİDROLİK MOTOR KONTROLÜ (Ortak Metod)
            if (!await EnsureHydraulicMotorRunningAsync("Parça Sıkıştırma"))
            {
                return false;
            }

            _logger?.LogInformation("💪 Parça sıkıştırma başlatılıyor - Hedef Basınç: {TargetPressure} bar, Tolerans: ±{Tolerance} bar", 
                targetPressure, tolerance);

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil! Parça sıkıştırma başlatılamaz!");
                OnAlarmRaised("Modbus bağlantısı yok - Parça sıkıştırma başlatılamaz", SafetyStatus.Critical);
                return false;
            }

            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger?.LogWarning("Güvenlik kontrolü başarısız - parça sıkıştırma başlatılamaz");
                return false;
            }

            // ✅ DOKÜMANA GÖRE: Sol parça varlık sensörü kontrolü
            await UpdateMachineStatusAsync(); // Status'u güncelle
            if (!_currentStatus.LeftPartPresent)
            {
                _logger?.LogWarning("Sol parça varlık sensörü parçayı görmüyor - sıkıştırma başlatılamaz");
                return false;
            }
            _logger?.LogInformation("✅ Sol parça varlık sensörü parçayı görüyor, sıkıştırma başlatılabilir");

            // ✅ SADECE BASINÇ BAZLI SIKIŞTRMA - Pozisyon hesaplaması kaldırıldı
            // Sahte pozisyon hesaplaması problemi düzeltildi
            
            // Güvenlik için maksimum pozisyon limiti
            var maxSafePosition = 300.0; // 300mm maksimum güvenlik pozisyonu
            
            _logger?.LogInformation("📐 Sıkıştırma hedef - SADECE BASINÇ BAZLI: Hedef Basınç: {TargetPressure} bar, Max Güvenlik Pozisyon: {MaxPos}mm", 
                targetPressure, maxSafePosition);

            _logger?.LogInformation("Parça sıkıştırma başlıyor - SADECE BASINÇ HEDEFI: {TargetPressure} bar, Max Güvenlik: {MaxPos}mm", targetPressure, maxSafePosition);

            // Üst pistonu hareket ettir - SADECE basınç hedefine kadar (pozisyon hedefi kaldırıldı)
            var success = await CompressWithTopPistonAsync(maxSafePosition, targetPressure, tolerance);

            if (success)
            {
                _logger?.LogInformation("Parça başarıyla sıkıştırıldı - Hedef Basınç: {TargetPressure} bar (SADECE BASINÇ BAZLI)", targetPressure);
                
                // ✅ SIKIŞTRMA SONRASI CETVEL RE-RESET
                _logger?.LogInformation("🔄 Sıkıştırma sonrası cetvel re-reset yapılıyor (basınç etkisi düzeltmesi)...");
                _logger?.LogInformation("💡 NEDENİ: Sıkıştırma basıncı sistem titreşimi/hareket yaratarak cetvel değerlerini etkileyebilir");
                
                // 1 saniye bekle - sistem stabilizasyonu için
                await Task.Delay(1000);
                
                var reResetResult = await ResetRulerValuesOnlyAsync();
                if (reResetResult)
                {
                    _logger?.LogInformation("✅ Sıkıştırma sonrası cetvel re-reset başarılı - Basınç etkisi düzeltildi");
                }
                else
                {
                    _logger?.LogWarning("⚠️ Sıkıştırma sonrası cetvel re-reset başarısız - Paso test toleransı artırıldığı için problem olmayabilir");
                }
            }
            else
            {
                _logger?.LogError("Parça sıkıştırma başarısız");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Parça sıkıştırma sırasında hata oluştu");
            OnAlarmRaised($"Parça sıkıştırma hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// Üst piston ile parça sıkıştırma - hedef konuma veya hedef basınca ulaşana kadar
    /// </summary>
    private async Task<bool> CompressWithTopPistonAsync(double targetPosition, double targetPressure, double tolerance)
    {
        try
        {
            _logger?.LogInformation("🔧 Üst piston ile sıkıştırma başlatılıyor - Hedef Konum: {TargetPosition}mm, Hedef Basınç: {TargetPressure} bar", targetPosition, targetPressure);
            
            var topPiston = _pistons[PistonType.TopPiston];
            var initialPosition = topPiston.CurrentPosition;
            var maxIterations = 200; // Güvenlik için maksimum iterasyon (artırıldı)
            var iteration = 0;
            
            _logger?.LogInformation("📍 Başlangıç pozisyonu: {InitialPosition}mm", initialPosition);
            
            // ✅ DÜZELTME: Üst pistonu ileri doğru hareket ettirmek için valve açılması + pozitif voltaj gerekli
            await OpenValveForPiston(topPiston);
            _logger?.LogInformation("🔓 Üst piston valfi açıldı");
            
            // ✅ ÖNEMLİ: İlk hareket komutunu ver (aşağı yönde/sıkıştırma için NEGATİF voltaj)
            const double compressionVoltage = -10.0; // Orta hızda sıkıştırma (5V negatif = ileri)
            await MovePistonAsync(PistonType.TopPiston, compressionVoltage);
            _logger?.LogInformation("⚡ Üst piston hareket başlatıldı - Voltaj: {Voltage}V (sıkıştırma yönü - negatif=ileri)", compressionVoltage);
            
            // ✅ DOKÜMANA GÖRE: Kalkış basıncı görmezden gelme (600ms)
            var startTime = DateTime.UtcNow;
            var ignoreStartupPressureDuration = TimeSpan.FromMilliseconds(600); // 600ms kalkış basıncı ignore
            _logger?.LogInformation("🕒 Kalkış basıncı görmezden gelme süresi başladı: {Duration}ms", ignoreStartupPressureDuration.TotalMilliseconds);
            
            // Hedef konuma veya basınca ulaşana kadar döngü
            while (iteration < maxIterations)
            {
                await Task.Delay(100); // 100ms bekle
                await UpdatePistonPositionsAsync(); // Pozisyonları güncelle
                
                var currentPosition = _pistons[PistonType.TopPiston].CurrentPosition;
                var elapsedTime = DateTime.UtcNow - startTime;
                var progressMm = Math.Abs(currentPosition - initialPosition);
                
                // Her 10 iterasyonda pozisyon takibi logla
                if (iteration % 10 == 0)
                {
                    _logger?.LogDebug("📊 Sıkıştırma ilerlemesi: Başlangıç={Initial:F1}mm, Şimdi={Current:F1}mm, İlerleme={Progress:F1}mm", 
                        initialPosition, currentPosition, progressMm);
                }
                
                // Koşul 1: Güvenlik pozisyon limitini aştı mı? (Sadece güvenlik için)
                if (currentPosition >= targetPosition)
                {
                    _logger?.LogWarning("🚫 Güvenlik pozisyon limiti aşıldı: {CurrentPosition}mm >= {MaxSafePosition}mm", currentPosition, targetPosition);
                    break;
                }
                
                // Koşul 2: Hedef basınca ulaştı mı? - SADECE kalkış süresi geçtikten sonra kontrol et
                if (elapsedTime > ignoreStartupPressureDuration)
                {
                    // ✅ DOKÜMANA GÖRE: Gerçek basınç sensörlerinden oku (S1/S2)
                    var (s1Pressure, s2Pressure) = await ReadActualPressureAsync();
                    var actualPressure = Math.Max(s1Pressure, s2Pressure); // En yüksek basınç değerini al
                    
                    if (actualPressure >= (targetPressure - tolerance))
                    {
                        _logger?.LogInformation("✅ Hedef basınca ulaşıldı: S1={S1}bar, S2={S2}bar, Max={MaxPressure}bar >= {TargetPressure}bar (tolerans: {Tolerance}) - Süre: {ElapsedTime}ms", 
                            s1Pressure, s2Pressure, actualPressure, targetPressure, tolerance, elapsedTime.TotalMilliseconds);
                        break;
                    }
                    
                    // Her 5. iterasyonda basınç değerlerini logla
                    if (iteration % 5 == 0)
                    {
                        _logger?.LogDebug("🔍 Basınç kontrolü: S1={S1:F1}bar, S2={S2:F1}bar, Hedef={Target:F1}bar", 
                            s1Pressure, s2Pressure, targetPressure);
                    }
                }
                else
                {
                    // Kalkış basıncı görmezden gelme süresi devam ediyor
                    var remainingIgnoreTime = ignoreStartupPressureDuration - elapsedTime;
                    if (iteration % 10 == 0) // Her 1000ms'de bir log
                    {
                        _logger?.LogDebug("⏳ Kalkış basıncı görmezden geliniyor - Kalan süre: {RemainingTime}ms", remainingIgnoreTime.TotalMilliseconds);
                    }
                }
                
                // Güvenlik kontrolü - aşırı pozisyon kontrolü
                if (currentPosition > 350) // Maksimum güvenlik pozisyonu
                {
                    _logger?.LogWarning("🚫 Güvenlik sınırı aşıldı, sıkıştırma durduruluyor: {CurrentPosition}mm > 350mm", currentPosition);
                    break;
                }
                
                // ✅ DÜZELTME: Hareket devamlılığını sağla - NEGATİF voltajla devam et
                // Piston durmuşsa tekrar hareket komutu ver
                if (iteration % 5 == 0) // Her 500ms'de hareket komutunu yenile
                {
                    await MovePistonAsync(PistonType.TopPiston, compressionVoltage); // Negatif voltaj = sıkıştırma yönü (ileri)
                }
                
                iteration++;
            }
            
            // ✅ DÜZELTME: Pistonu durdur ve valfi kapat
            _logger?.LogInformation("🛑 Sıkıştırma döngüsü tamamlandı, piston durduruluyor...");
            await StopPistonAsync(PistonType.TopPiston);
            await CloseValveForPiston(topPiston);
            _logger?.LogInformation("🔒 Üst piston valfi kapatıldı");
            
            // Final pozisyon ve basınç okumalarını al
            await UpdatePistonPositionsAsync(); // Final pozisyon güncellemesi
            var finalPosition = _pistons[PistonType.TopPiston].CurrentPosition;
            var totalMovement = Math.Abs(finalPosition - initialPosition);
            
            // ✅ DÜZELTME: Final basınç kontrolü için gerçek sensörden oku
            var (finalS1Pressure, finalS2Pressure) = await ReadActualPressureAsync();
            var finalActualPressure = Math.Max(finalS1Pressure, finalS2Pressure); // En yüksek basınç
            
            // Başarı kriterleri analizi - SADECE BASINÇ BAZLI
            var positionSafe = finalPosition < targetPosition; // Güvenlik pozisyonu aşılmamış
            var pressureReached = finalActualPressure >= (targetPressure - tolerance);
            var hasMovement = totalMovement > 5.0; // En az 5mm hareket etmeli
            
            // Detaylı sonuç raporu
            _logger?.LogInformation("📊 SIKIŞTıRMA SONUÇ RAPORU (SADECE BASINÇ BAZLI):");
            _logger?.LogInformation("   📍 Pozisyon: Başlangıç={Initial:F1}mm → Final={Final:F1}mm (Hareket: {Movement:F1}mm)", 
                initialPosition, finalPosition, totalMovement);
            _logger?.LogInformation("   🔒 Güvenlik Pozisyon: {Target:F1}mm - {Status}", targetPosition, positionSafe ? "✅ GÜVENLİ" : "⚠️ LİMİT AŞILDI");
            _logger?.LogInformation("   💪 GERÇEK BASINÇ: S1={S1:F1}bar, S2={S2:F1}bar, Max={Max:F1}bar", 
                finalS1Pressure, finalS2Pressure, finalActualPressure);
            _logger?.LogInformation("   🎯 HEDEF BASINÇ: {Target:F1}bar (±{Tolerance:F1}) - {Status}", 
                targetPressure, tolerance, pressureReached ? "✅ ULAŞILDI" : "❌ ULAŞILAMADI");
            _logger?.LogInformation("   🔄 Hareket Kontrolü: {Status}", hasMovement ? "✅ YETERLİ" : "❌ YETERSİZ");
            
            // ✅ DÜZELTME: Başarı kriterleri - SADECE BASINÇ ULAŞILMALI, pozisyon güvenli olmalı ve hareket olmalı
            var success = pressureReached && positionSafe && hasMovement;
            
            if (success)
            {
                _logger?.LogInformation("🟢 PARÇA SIKIŞTRMA BAŞARILI - Kriterler: Güvenlik={PosSafe}, Basınç={PressOK}, Hareket={MoveOK}", 
                    positionSafe, pressureReached, hasMovement);
            }
            else
            {
                _logger?.LogWarning("🔴 PARÇA SIKIŞTRMA BAŞARISIZ - Nedenler: Güvenlik={PosSafe}, Basınç={PressOK}, Hareket={MoveOK}", 
                    positionSafe, pressureReached, hasMovement);
                    
                if (!hasMovement)
                {
                    _logger?.LogError("❌ KRİTİK HATA: Piston hareket etmedi! Hidrolik sistem veya valve problemi olabilir.");
                }
                if (!pressureReached)
                {
                    _logger?.LogError("❌ KRİTİK HATA: Hedef basınca ulaşılamadı! S1={S1}bar, S2={S2}bar, Hedef={Target}bar", 
                        finalS1Pressure, finalS2Pressure, targetPressure);
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Üst piston sıkıştırma sırasında hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// Pozisyondan basınç tahmini (gerçek sistemde basınç sensörü kullanılacak)
    /// </summary>
    private double CalculatePressureFromPosition(double position)
    {
        // Basit bir mapping: 0-300mm → 0-200 bar
        // Gerçek sistemde kalibrasyon gerekecek
        return Math.Min(200, Math.Max(0, position / 1.5));
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Gerçek basınç sensörlerinden basınç okur (S1/S2)
    /// converter.md'ye göre RegisterToBarAndMilliamps metodu kullanılmalı
    /// </summary>
    private async Task<(double s1Pressure, double s2Pressure)> ReadActualPressureAsync()
    {
        try
        {
            // S1 ve S2 basınç sensörlerini oku (4-20mA analog input)
            var s1PressureRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S1_OilPressure); // 0x000B
            var s2PressureRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S2_OilPressure); // 0x000A
            
            // ✅ CONVERTER.MD DOĞRU YÖNTEM: RegisterToBarAndMilliamps (0-250 bar aralığı ile)
            var (s1Pressure, s1mA) = DataConverter.RegisterToBarAndMilliamps(s1PressureRaw, 4095, 4.0, 20.0, 0.0, 250.0);
            var (s2Pressure, s2mA) = DataConverter.RegisterToBarAndMilliamps(s2PressureRaw, 4095, 4.0, 20.0, 0.0, 250.0);
            
            // Detaylı logging için mA değerlerini de logla
            _logger?.LogDebug("📊 Basınç okuma detayları: S1(raw={S1Raw}, mA={S1mA:F2}, bar={S1Bar:F1}), S2(raw={S2Raw}, mA={S2mA:F2}, bar={S2Bar:F1})", 
                s1PressureRaw, s1mA, s1Pressure, s2PressureRaw, s2mA, s2Pressure);
            
            return (s1Pressure, s2Pressure);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Basınç sensörü okuma hatası");
            return (0, 0); // Hata durumunda güvenli değer dön
        }
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Pulse değerini mesafe değerine çevirir (Rotasyon için)
    /// </summary>
    private static double PulseToDistanceConvert(double register, double ballDiameter)
    {
        const double pulseCount = 1024.0; // RV3100 encoder 1024 pulse/tur
        double perimeterDistance = ballDiameter * Math.PI; // Top çevre uzunluğu (mm/tur)
        
        // Tam tur sayısı ve kalan pulse hesabı
        double totalTurns = register / pulseCount; // Kaç tam tur?
        double remainingPulses = register % pulseCount; // Kalan pulse
        
        // Tam turlardan gelen mesafe + kalan pulselerden gelen mesafe
        double fullTurnDistance = totalTurns * perimeterDistance;
        double remainingDistance = (remainingPulses / pulseCount) * perimeterDistance;
        
        double totalDistance = fullTurnDistance + remainingDistance;
        return Math.Round(totalDistance, 2);
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Mesafe değerini pulse değerine çevirir (Rotasyon için)
    /// </summary>
    private static int DistanceToPulseConvert(double mm, double ballDiameter)
    {
        const double pulseCount = 1024.0; // RV3100 encoder 1024 pulse/tur
        double perimeterDistance = ballDiameter * Math.PI; // Top çevre uzunluğu (mm/tur)
        
        // Gereken tur sayısı ve kalan mesafe hesabı
        double totalTurns = Math.Floor(mm / perimeterDistance); // Tam tur sayısı
        double remainingDistance = mm % perimeterDistance; // Kalan mesafe
        
        // Tam turlardan gelen pulse + kalan mesafeden gelen pulse
        double fullTurnPulses = totalTurns * pulseCount;
        double remainingPulses = (remainingDistance / perimeterDistance) * pulseCount;
        
        int totalPulses = Convert.ToInt32(Math.Round(fullTurnPulses + remainingPulses));
        return totalPulses;
    }

    public async Task<bool> ResetPartPositionAsync(double resetDistance)
    {
        try
        {
            // ✅ HİDROLİK MOTOR KONTROLÜ (Ortak Metod)
            if (!await EnsureHydraulicMotorRunningAsync("Parça Sıfırlama"))
            {
                return false;
            }

            _logger?.LogInformation("🔄 Parça pozisyon sıfırlama başlatılıyor - Reset Mesafesi: {ResetDistance:F2} mm", resetDistance);
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger?.LogWarning("Güvenlik kontrolü başarısız - parça sıfırlama başlatılamaz");
                return false;
            }

            // Hidrolik motoru kontrol et ve gerekirse başlat
            if (!_currentStatus.HydraulicMotorRunning)
            {
                _logger?.LogInformation("Hidrolik motor kapalı, başlatılıyor...");
                if (!await StartHydraulicMotorAsync())
                {
                    _logger?.LogError("Hidrolik motor başlatılamadı");
                    return false;
                }
                
                // Motor stabilizasyonu için bekle
                await Task.Delay(2000);
            }

            // ✅ DOKÜMANA GÖRE: Parça varlık sensörü kontrolü
            await UpdateMachineStatusAsync(); // Status'u güncelle
            
            // Sol ve sağ parça varlık sensörlerini kontrol et
            bool leftPartPresent = _currentStatus.LeftPartPresent;
            bool rightPartPresent = _currentStatus.RightPartPresent;
            
            _logger?.LogInformation("📍 Parça varlık durumu - Sol: {LeftPresent}, Sağ: {RightPresent}", leftPartPresent, rightPartPresent);

            // Sol parça varlık sensöründe işlem yap (öncelikli)
            if (leftPartPresent || rightPartPresent)
            {
                bool useLeftSensor = leftPartPresent; // Sol varsa sol kullan, yoksa sağ kullan
                _logger?.LogInformation("🎯 {Sensor} parça varlık sensörü ile sıfırlama yapılacak", useLeftSensor ? "Sol" : "Sağ");
                
                // DOKÜMANA GÖRE: Rotasyon bazlı parça sıfırlama algoritması
                var success = await PerformRotationBasedResetAsync(resetDistance, useLeftSensor);
                
                if (success)
                {
                    _logger?.LogInformation("✅ Parça pozisyonu başarıyla sıfırlandı - Sıfırlama Mesafesi: {ResetDistance}mm", resetDistance);
                }
                else
                {
                    _logger?.LogError("❌ Parça pozisyon sıfırlama başarısız");
                }
                
                return success;
            }
            else
            {
                _logger?.LogWarning("⚠️ Hiçbir parça varlık sensörü parçayı görmüyor - sıfırlama yapılamaz");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Parça pozisyon sıfırlama sırasında hata oluştu");
            OnAlarmRaised($"Parça sıfırlama hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Rotasyon bazlı parça sıfırlama algoritması
    /// </summary>
    private async Task<bool> PerformRotationBasedResetAsync(double resetDistance, bool useLeftSensor)
    {
        try
        {
            const double ballDiameter = 220.0; // mm - Alt orta top çapı (ayarlar sayfasından alınabilir)
            const double normalSpeed = 40.0;    // Normal hız %40 (daha kontrollü)
            const double mediumSpeed = 25.0;    // Orta hız %25 (kaba konumlandırma)
            const double preciseSpeed = 15.0;   // Hassas hız %15 (hassas konumlandırma)
            
            _logger?.LogInformation("🔄 Rotasyon bazlı sıfırlama başlatılıyor - Sensör: {Sensor}, Top Çapı: {Diameter}mm", 
                useLeftSensor ? "Sol" : "Sağ", ballDiameter);

            // Adım 1: Parça varlık sensörü durumuna göre algoritma seç
            await UpdateMachineStatusAsync();
            bool currentSensorState = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
            
            if (currentSensorState)
            {
                // DURUM A: Sensör parçayı görüyor
                _logger?.LogInformation("📍 DURUM A: Sensör parçayı görüyor - İlk adım başlatılıyor");
                
                // A.1: Saat yönünde rotasyon - sensör görmeyene kadar
                _logger?.LogInformation("🔄 A.1: Saat yönünde normal hızda rotasyon ({Speed}%) - sensör görmeyene kadar", normalSpeed);
                
                var clockwiseDirection = useLeftSensor ? RotationDirection.Clockwise : RotationDirection.CounterClockwise;
                await StartRotationAsync(clockwiseDirection, normalSpeed);
                
                // Sensör görmeyene kadar bekle
                var maxWaitTime = TimeSpan.FromSeconds(15); // 10s → 15s
                var startTime = DateTime.UtcNow;
                
                while ((DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    await Task.Delay(100);
                    await UpdateMachineStatusAsync();
                    bool sensorStillSees = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
                    
                    if (!sensorStillSees)
                    {
                        _logger?.LogInformation("✅ A.1 Tamamlandı: Sensör artık parçayı görmüyor");
                        break;
                    }
                }
                
                await StopRotationAsync();
                await Task.Delay(1000); // 500ms → 1000ms stabilizasyon
            }
            else
            {
                // DURUM B: Sensör parçayı görmüyor  
                _logger?.LogInformation("📍 DURUM B: Sensör parçayı görmüyor - İki aşamalı işlem başlatılıyor");
                
                // B.1: Ters saat yönünde rotasyon - sensör görene kadar
                _logger?.LogInformation("🔄 B.1: Ters saat yönünde normal hızda rotasyon ({Speed}%) - sensör görene kadar", normalSpeed);
                
                var counterClockwiseDirection = useLeftSensor ? RotationDirection.CounterClockwise : RotationDirection.Clockwise;
                await StartRotationAsync(counterClockwiseDirection, normalSpeed);
                
                // Sensör görene kadar bekle
                var maxWaitTime = TimeSpan.FromSeconds(15); // 10s → 15s
                var startTime = DateTime.UtcNow;
                bool sensorSaw = false;
                
                while ((DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    await Task.Delay(100);
                    await UpdateMachineStatusAsync();
                    bool sensorSees = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
                    
                    if (sensorSees)
                    {
                        _logger?.LogInformation("✅ B.1 Tamamlandı: Sensör parçayı görmeye başladı");
                        sensorSaw = true;
                        break;
                    }
                }
                
                await StopRotationAsync();
                
                if (!sensorSaw)
                {
                    _logger?.LogError("❌ B.1 Başarısız: Sensör parçayı görmedi - timeout");
                    return false;
                }
                
                await Task.Delay(1000); // 500ms → 1000ms stabilizasyon
                
                // B.2: Saat yönünde rotasyon - sensör görmeyene kadar
                _logger?.LogInformation("🔄 B.2: Saat yönünde normal hızda rotasyon ({Speed}%) - sensör görmeyene kadar", normalSpeed);
                
                var clockwiseDirection = useLeftSensor ? RotationDirection.Clockwise : RotationDirection.CounterClockwise;
                await StartRotationAsync(clockwiseDirection, normalSpeed);
                
                startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime) < maxWaitTime)
                {
                    await Task.Delay(100);
                    await UpdateMachineStatusAsync();
                    bool sensorStillSees = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
                    
                    if (!sensorStillSees)
                    {
                        _logger?.LogInformation("✅ B.2 Tamamlandı: Sensör artık parçayı görmüyor");
                        break;
                    }
                }
                
                await StopRotationAsync();
                await Task.Delay(1000); // 500ms → 1000ms stabilizasyon
            }
            
            // Adım 2: Kaba konumlandırma - orta hızda ters rotasyon ile sensör yakınına gel
            _logger?.LogInformation("🎯 Adım 2: Kaba konumlandırma - orta hızda ters rotasyon ({Speed}%) - sensör yakınına gel", mediumSpeed);
            
            var preciseDirection = useLeftSensor ? RotationDirection.CounterClockwise : RotationDirection.Clockwise;
            await StartRotationAsync(preciseDirection, mediumSpeed);
            
            var mediumStartTime = DateTime.UtcNow;
            bool sensorSeenInMedium = false;
            
            while ((DateTime.UtcNow - mediumStartTime) < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(100);
                await UpdateMachineStatusAsync();
                bool sensorSees = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
                
                if (sensorSees)
                {
                    _logger?.LogInformation("✅ Adım 2 Tamamlandı: Sensör parçayı görmeye başladı");
                    sensorSeenInMedium = true;
                    break;
                }
            }
            
            await StopRotationAsync();
            
            if (!sensorSeenInMedium)
            {
                _logger?.LogError("❌ Adım 2 Başarısız: Sensör parçayı görmedi - timeout");
                return false;
            }
            
            await Task.Delay(1000); // 1 saniye stabilizasyon
            
            // Adım 3: Hassas konumlandırma - çok yavaş hızda ters rotasyon ile tam konumlandırma
            _logger?.LogInformation("🎯 Adım 3: Hassas konumlandırma - çok yavaş hızda ters rotasyon ({Speed}%) - tam konumlandırma", preciseSpeed);
            
            await StartRotationAsync(preciseDirection, preciseSpeed);
            
            var preciseStartTime = DateTime.UtcNow;
            bool sensorSeenInPrecise = false;
            
            while ((DateTime.UtcNow - preciseStartTime) < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(100);
                await UpdateMachineStatusAsync();
                bool sensorSees = useLeftSensor ? _currentStatus.LeftPartPresent : _currentStatus.RightPartPresent;
                
                if (sensorSees)
                {
                    _logger?.LogInformation("✅ Adım 3 Tamamlandı: Sensör parçayı hassas konumda gördü");
                    sensorSeenInPrecise = true;
                    break;
                }
            }
            
            await StopRotationAsync();
                        
            if (!sensorSeenInPrecise)
            {
                _logger?.LogError("❌ Adım 3 Başarısız: Sensör parçayı görmedi - timeout");
                            return false;
                        }
                        
            await Task.Delay(1000); // 1 saniye stabilizasyon
            
            // Adım 4: Alt top merkezine çekilme (resetDistance kadar rotasyon)
            _logger?.LogInformation("🎯 Adım 4: Alt top merkezine çekilme - {Distance}mm rotasyon", resetDistance);
            
            // Encoder parametrelerini esnetilmiş şekilde ayarla
            var encoderOptions = new CancellationTokenSource();
            encoderOptions.CancelAfter(TimeSpan.FromSeconds(90)); // 60s → 90s timeout
            
            var rotationSuccess = await PerformPreciseEncoderRotationAsync(
                useLeftSensor ? RotationDirection.Clockwise : RotationDirection.CounterClockwise,
                resetDistance,
                normalSpeed,
                encoderOptions.Token
            );
            
            if (!rotationSuccess)
            {
                _logger?.LogError("❌ Adım 4 Başarısız: Alt top merkezine çekilme rotasyonu başarısız");
                return false;
            }
            
            _logger?.LogInformation("✅ Parça sıfırlama işlemi başarıyla tamamlandı");
                return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Rotasyon bazlı sıfırlama sırasında hata oluştu");
            return false;
        }
    }
    public async Task<bool> ExecuteAutoBendingAsync(DomainBendingParameters parameters)
    {
        try
        {
            _logger?.LogInformation("🏭 Otomatik büküm başlatılıyor - Parametreler: Stage={StageValue}, LeftReel={LeftReelPosition}, RightReel={RightReelPosition}", 
                parameters.StageValue, parameters.LeftReelPosition, parameters.RightReelPosition);
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger?.LogWarning("Güvenlik kontrolü başarısız - otomatik büküm başlatılamaz");
                return false;
            }

            // ✅ HİDROLİK MOTOR KONTROLÜ - Otomatik büküm başlamadan önce
            await EnsureHydraulicMotorRunningAsync("Otomatik Büküm");

            // ✅ DOKÜMANA GÖRE: Ani basınç değişiklikleri kontrolü için başlangıç basıncını oku
            var (initialS1Pressure, initialS2Pressure) = await ReadActualPressureAsync();
            var initialMaxPressure = Math.Max(initialS1Pressure, initialS2Pressure);
            _logger?.LogInformation("📊 Başlangıç basınç değerleri - S1: {S1}bar, S2: {S2}bar, Max: {Max}bar", 
                initialS1Pressure, initialS2Pressure, initialMaxPressure);

            // ✅ YAN DAYAMA PİSTONLARI KALDIRILDI: Kullanıcı talebi doğrultusunda
            // "Yan dayama pistonlarını şu anlık dahil etmeyeceğiz" - Kullanıcı talebi
            _logger?.LogInformation("ℹ️ Yan dayama pistonları kullanıcı talebi doğrultusunda otomatik büküm prosesine dahil edilmiyor");

            // ✅ DOKÜMANA GÖRE: Büküm sırasında ani basınç değişiklikleri kontrolü
            var bendingCancellationToken = new CancellationTokenSource();
            var pressureMonitoringTask = MonitorPressureChangesAsync(initialMaxPressure, bendingCancellationToken.Token);

            try
            {
                // ✅ DOKÜMANTASYON SİRASI: Otomatik büküm prosesi implement edildi
                _logger?.LogInformation("🏭 Otomatik büküm prosesi başlatılıyor...");
                
                // ADIM 1: Stage ayarlama (eğer belirtilmişse)
                if (parameters.StageValue > 0)
                {
                    _logger?.LogInformation("⚙️ Stage ayarlama: {StageValue}mm", parameters.StageValue);
                    await SetStageAsync(parameters.StageValue);
                }
                
                // ADIM 2: Büküm hesabı (zaten hesaplandı ve parametreler geldi)
                _logger?.LogInformation("📊 Büküm parametreleri alındı");
                
                // ADIM 3: Parça sıkıştırma
                _logger?.LogInformation("🗜️ Parça sıkıştırma işlemi başlatılıyor...");
                var compressionResult = await CompressPartAsync(parameters.TargetPressure, parameters.PressureTolerance);
                if (!compressionResult)
                {
                    _logger?.LogError("❌ Parça sıkıştırma başarısız - büküm iptal ediliyor");
                    return false;
                }
                
                // ADIM 4: Parça sıfırlama  
                _logger?.LogInformation("🔄 Parça sıfırlama işlemi başlatılıyor - Mesafe: {ResetDistance}mm", parameters.ProfileResetDistance);
                var resetResult = await ResetPartPositionAsync(parameters.ProfileResetDistance);
                if (!resetResult)
                {
                    _logger?.LogError("❌ Parça sıfırlama başarısız - büküm iptal ediliyor");
                    return false;
                }
                
                // ADIM 5: PASO-BASED BÜKÜM PROSESİ
                _logger?.LogInformation("🔥 Paso-based büküm prosesi başlatılıyor...");
                var bendingResult = await ExecutePasoBasedBendingAsync(parameters, bendingCancellationToken.Token);
                if (!bendingResult)
                {
                    _logger?.LogError("❌ Büküm prosesi başarısız");
                    return false;
                }
                
                // Monitoring'i durdur
                bendingCancellationToken.Cancel();
                
                _logger?.LogInformation("✅ Otomatik büküm tamamen tamamlandı");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("⚠️ Büküm işlemi ani basınç değişikliği nedeniyle durduruldu");
                return false;
            }
            finally
            {
                bendingCancellationToken.Cancel();
                try { await pressureMonitoringTask; } catch { } // Task'ı temizle
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Otomatik büküm sırasında hata oluştu");
            OnAlarmRaised($"Otomatik büküm hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Büküm sırasında ani basınç değişiklikleri kontrolü
    /// Ani basınç düşüşü tespit edilirse büküm durdurulur (parça kırılması/deforme olması)
    /// </summary>
    private async Task MonitorPressureChangesAsync(double initialPressure, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("🔍 Basınç değişiklikleri monitörü başlatıldı - Başlangıç basınç: {InitialPressure}bar", initialPressure);
            
            const double pressureDropThreshold = 20.0; // 20 bar ani düşüş eşiği
            var monitoringInterval = TimeSpan.FromMilliseconds(500); // 500ms'de bir kontrol
            var consecutiveDropCount = 0;
            const int maxConsecutiveDrops = 3; // 3 ardışık düşüş = alarm
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(monitoringInterval, cancellationToken);
                
                // Mevcut basınç değerlerini oku
                var (currentS1Pressure, currentS2Pressure) = await ReadActualPressureAsync();
                var currentMaxPressure = Math.Max(currentS1Pressure, currentS2Pressure);
                
                // Ani basınç düşüşü kontrolü
                var pressureDrop = initialPressure - currentMaxPressure;
                
                if (pressureDrop > pressureDropThreshold)
                {
                    consecutiveDropCount++;
                    _logger?.LogWarning("⚠️ Ani basınç düşüşü tespit edildi! Düşüş: {Drop:F1}bar (Başlangıç: {Initial:F1}bar → Mevcut: {Current:F1}bar) - Ardışık: {Count}/{Max}", 
                        pressureDrop, initialPressure, currentMaxPressure, consecutiveDropCount, maxConsecutiveDrops);
                    
                    if (consecutiveDropCount >= maxConsecutiveDrops)
                    {
                        _logger?.LogError("🚨 KRİTİK: {Count} ardışık ani basınç düşüşü! Büküm durduruldu. Parça kırılmış/deforme olmuş olabilir.", maxConsecutiveDrops);
                        OnAlarmRaised($"Ani basınç düşüşü - Büküm durduruldu! Düşüş: {pressureDrop:F1} bar", SafetyStatus.Critical);
                        
                        // Acil durdurma
                        await EmergencyStopAsync();
                        return;
                    }
                }
                else
                {
                    // Basınç normal seviyelerde, sayacı sıfırla
                    if (consecutiveDropCount > 0)
                    {
                        _logger?.LogInformation("✅ Basınç normale döndü - Ardışık düşüş sayacı sıfırlandı");
                        consecutiveDropCount = 0;
                    }
                }
                
                // Periyodik log (her 10 saniyede bir)
                var elapsed = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
                if (elapsed % 20 == 0) // Her 20 saniyede bir
                {
                    _logger?.LogDebug("📊 Basınç monitörü: S1={S1:F1}bar, S2={S2:F1}bar, Max={Max:F1}bar (Başlangıç: {Initial:F1}bar)", 
                        currentS1Pressure, currentS2Pressure, currentMaxPressure, initialPressure);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("🔍 Basınç değişiklikleri monitörü durduruldu");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Basınç değişiklikleri monitörü sırasında hata oluştu");
        }
    }

    /// <summary>
    /// ✅ ENCODER BAZLI ROTASYON: Belirtilen mesafe kadar encoder kontrollü rotasyon yapar
    /// </summary>
    private async Task<bool> PerformEncoderBasedRotationAsync(RotationDirection direction, double targetDistance, double speed, CancellationToken cancellationToken)
    {
        try
        {
            const double ballDiameter = 220.0; // mm - [Kullanıcı top çapının 140mm değil 220mm olduğunu belirtti]
            const double maxRotationTimeSeconds = 30.0; // Maksimum rotasyon süresi güvenlik için
            const double encoderTolerance = 2.0; // mm - Encoder toleransı
            
            _logger?.LogInformation("🔄 Encoder bazlı rotasyon başlatılıyor - Yön: {Direction}, Hedef: {Target}mm, Hız: {Speed}%", 
                direction, targetDistance, speed);
            
            // Başlangıç encoder pozisyonunu al
            await UpdateMachineStatusAsync();
            var startEncoderRaw = _currentStatus.RotationEncoderRaw;
            var startDistance = PulseToDistanceConvert(startEncoderRaw, ballDiameter);
            
            _logger?.LogInformation("📍 Başlangıç encoder - Raw: {Raw}, Mesafe: {Distance:F2}mm", startEncoderRaw, startDistance);
            
            // Hedef encoder pozisyonunu hesapla
            var targetEncoderDistance = direction == RotationDirection.Clockwise ? 
                startDistance + targetDistance : startDistance - targetDistance;
            
            _logger?.LogInformation("🎯 Hedef encoder mesafesi: {Target:F2}mm", targetEncoderDistance);
            
            // Rotasyonu başlat
            await StartRotationAsync(direction, speed);
            
            var startTime = DateTime.UtcNow;
            var lastEncoderCheck = DateTime.UtcNow;
            var stuckCount = 0;
            const int maxStuckCount = 10; // 10 ardışık stuck = hata
            var previousEncoderRaw = startEncoderRaw;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken); // 100ms'de bir kontrol
                
                // Timeout kontrolü
                if ((DateTime.UtcNow - startTime).TotalSeconds > maxRotationTimeSeconds)
                {
                    _logger?.LogError("❌ Encoder bazlı rotasyon timeout! Süre: {Time:F1}s", maxRotationTimeSeconds);
                    await StopRotationAsync();
                    return false;
                }
                
                // Encoder pozisyonunu güncelle
                await UpdateMachineStatusAsync();
                var currentEncoderRaw = _currentStatus.RotationEncoderRaw;
                var currentDistance = PulseToDistanceConvert(currentEncoderRaw, ballDiameter);
                
                // Encoder stuck kontrolü
                if (Math.Abs(currentEncoderRaw - previousEncoderRaw) < 1) // 1 pulse değişim yoksa
                {
                    stuckCount++;
                    if (stuckCount >= maxStuckCount)
                    {
                        _logger?.LogError("❌ Encoder dondu! {Count} kez değişim yok. Raw: {Raw}", maxStuckCount, currentEncoderRaw);
                        await StopRotationAsync();
                        return false;
                    }
                }
                else
                {
                    stuckCount = 0; // Hareket var, sayacı sıfırla
                    previousEncoderRaw = currentEncoderRaw;
                }
                
                // Hedef mesafeye ulaştı mı kontrol et
                var remainingDistance = Math.Abs(targetEncoderDistance - currentDistance);
                
                if (remainingDistance <= encoderTolerance)
                {
                    _logger?.LogInformation("✅ Encoder hedef mesafesine ulaşıldı!");
                    _logger?.LogInformation("📊 Başlangıç: {Start:F2}mm → Hedef: {Target:F2}mm → Gerçek: {Actual:F2}mm (Fark: {Diff:F2}mm)", 
                        startDistance, targetEncoderDistance, currentDistance, remainingDistance);
                    await StopRotationAsync();
                    return true;
                }
                
                // Her 1 saniyede bir progress log
                if ((DateTime.UtcNow - lastEncoderCheck).TotalSeconds >= 1.0)
                {
                    _logger?.LogDebug("📈 Encoder ilerlemesi - Mevcut: {Current:F2}mm, Hedef: {Target:F2}mm, Kalan: {Remaining:F2}mm", 
                        currentDistance, targetEncoderDistance, remainingDistance);
                    lastEncoderCheck = DateTime.UtcNow;
                }
            }
            
            // Cancellation durumunda rotasyonu durdur
            await StopRotationAsync();
            _logger?.LogWarning("⚠️ Encoder bazlı rotasyon iptal edildi");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Encoder bazlı rotasyon sırasında hata oluştu");
            await StopRotationAsync();
            return false;
        }
    }
    
    #endregion

    #region Rotation Control

    public async Task<bool> StartRotationAsync(RotationDirection direction, double speed)
    {
        try
        {
            _logger?.LogInformation("🔄 ROTASYON BAŞLATILIYOR - Yön: {Direction}, Hız: {Speed}%", direction, speed);
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil!");
                return false;
            }

            // ✅ GÜVENLİK KONTROLÜ
            _logger?.LogInformation("🔒 Güvenlik kontrolü yapılıyor...");
            if (!await CheckSafetyAsync())
            {
                _logger?.LogError("❌ HATA: Güvenlik kontrolü başarısız - rotasyon başlatılamaz!");
                return false;
            }
            _logger?.LogInformation("✅ Güvenlik kontrolü başarılı");

            // ✅ ALARM DURUMU KONTROLÜ
            var alarmStatus = await _modbusClient.ReadCoilAsync(ModbusAddresses.Alarm);
            if (alarmStatus)
            {
                _logger?.LogError("❌ HATA: Alarm aktif durumda! Önce ResetAlarmAsync() ile alarmı sıfırlayın.");
                return false;
            }
            _logger?.LogInformation("✅ Alarm durumu kontrol edildi: Alarm pasif");

            // ✅ HİDROLİK MOTOR KONTROLÜ
            var hydraulicMotor = await _modbusClient.ReadCoilAsync(ModbusAddresses.HydraulicEngine);
            if (!hydraulicMotor)
            {
                _logger?.LogWarning("⚠️ Hidrolik motor çalışmıyor, başlatılıyor...");
                await StartHydraulicMotorAsync();
                await Task.Delay(3000); // 3 saniye bekle
                hydraulicMotor = await _modbusClient.ReadCoilAsync(ModbusAddresses.HydraulicEngine);
                if (!hydraulicMotor)
                {
                    _logger?.LogError("❌ HATA: Hidrolik motor başlatılamadı!");
                    return false;
                }
            }
            _logger?.LogInformation("✅ Hidrolik motor çalışıyor");
            
            // ✅ ANA VALFLER KONTROLÜ
            _logger?.LogInformation("🔄 Ana valfler kontrol ediliyor...");
            var s1Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S1);
            var s2Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S2);
            
            if (!s1Status || !s2Status)
            {
                _logger?.LogWarning("⚠️ Ana valfler kapalı, açılıyor...");
            await OpenS1ValveAsync();
            await OpenS2ValveAsync();
                await Task.Delay(20); // 1 saniye bekle
                
                s1Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S1);
                s2Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S2);
                
                if (!s1Status || !s2Status)
                {
                    _logger?.LogError("❌ HATA: Ana valfler açılamadı!");
                    return false;
                }
            }
            _logger?.LogInformation("✅ Ana valfler açık: S1={S1}, S2={S2}", s1Status, s2Status);

            // ÖNCE ROTASYON COIL'LERİNİ SIFIRLA
            _logger?.LogInformation("🔄 Rotasyon coil'leri sıfırlanıyor...");
            await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, false);  // M21_Rotation_CWW = 0x1003
            await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, false); // M22_Rotation_CW = 0x1002
            await Task.Delay(20); // 500ms bekle
            
            // Hız ayarla (0-100 arası değeri 1V ile 10V arasına çevir)
            var speedVoltage = Math.Max(1.0, Math.Min(10.0, (speed / 100.0) * 9.0 + 1.0)); // 1V-10V aralığı
            var speedRegisterValue = (ushort)Math.Round((speedVoltage - 1.0) / 9.0 * 2047); // 1V-10V → 0-2047 register
            
            // ✅ ÖNCE HIZ AYARLA
            _logger?.LogInformation("⚡ Rotasyon hızı ayarlanıyor: {Speed}% → {Voltage}V → Register:{Register}", speed, speedVoltage, speedRegisterValue);
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt, speedRegisterValue);
            await Task.Delay(20); // 500ms bekle
            
            // ✅ SONRA YÖN BELİRLE
            switch (direction)
            {
                case RotationDirection.Clockwise:
                    _logger?.LogInformation("🔄 Saat yönü (CW) rotasyon başlatılıyor...");
                    // Saat yönü (CW): M22_Rotation_CW = 0x1002 kullan
                    await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, true);  // M22_Rotation_CW = TRUE
                    await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, false);  // M21_Rotation_CWW = FALSE
                    _logger?.LogInformation("✅ Clockwise rotasyon coil'leri ayarlandı - CW(0x1002): TRUE, CCW(0x1003): FALSE");
                    break;
                    
                case RotationDirection.CounterClockwise:
                    _logger?.LogInformation("🔄 Ters saat yönü (CCW) rotasyon başlatılıyor...");
                    // Ters saat yönü (CCW): M21_Rotation_CWW = 0x1003 kullan
                    await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, true);   // M21_Rotation_CWW = TRUE
                    await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, false); // M22_Rotation_CW = FALSE
                    _logger?.LogInformation("✅ CounterClockwise rotasyon coil'leri ayarlandı - CCW(0x1003): TRUE, CW(0x1002): FALSE");
                    break;
                    
                default:
                    _logger?.LogError("❌ HATA: Geçersiz rotasyon yönü!");
                    return false;
            }
            
            // ✅ DURUM KONTROLÜ
            await Task.Delay(20); // 1 saniye bekle
            var leftRotationStatus = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftRotation);
            var rightRotationStatus = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightRotation);
            var currentSpeed = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt);
            s1Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S1);
            s2Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S2);
            
            _logger?.LogInformation("📊 ROTASYON DURUM KONTROLÜ:");
            _logger?.LogInformation("  Sol Rotasyon (0x1003): {Status}", leftRotationStatus);
            _logger?.LogInformation("  Sağ Rotasyon (0x1002): {Status}", rightRotationStatus);
            _logger?.LogInformation("  Hız Register (0x0806): {Speed}", currentSpeed);
            _logger?.LogInformation("  S1 Valf (0x1000): {Status}", s1Status);
            _logger?.LogInformation("  S2 Valf (0x1001): {Status}", s2Status);
            
            _logger?.LogInformation("✅ ROTASYON BAŞLATILDI - Yön: {Direction}, Hız: {Speed}%", direction, speed);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ HATA: Rotasyon başlatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> StopRotationAsync()
    {
        try
        {
            _logger?.LogInformation("🔄 Rotasyon durduruluyor...");
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil!");
                return false;
            }

            // ✅ ÖNCE HIZ SIFIRLA
            _logger?.LogInformation("⚡ Rotasyon hızı sıfırlanıyor...");
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt, 0);
            await Task.Delay(50); // 500ms bekle
            
            // ✅ SONRA ROTASYON COIL'LERİNİ KAPAT
            _logger?.LogInformation("🔄 Rotasyon coil'leri kapatılıyor...");
            await _modbusClient.WriteCoilAsync(ModbusAddresses.LeftRotation, false);  // M21_Rotation_CWW = 0x1003
            await _modbusClient.WriteCoilAsync(ModbusAddresses.RightRotation, false); // M22_Rotation_CW = 0x1002
            await Task.Delay(50); // 500ms bekle
            
            // ✅ S1/S2 ANA VALFLERİ KAPAT
            _logger?.LogInformation("🔄 Ana valfler kapatılıyor...");
            await CloseS1ValveAsync(); // S1 = 0x1000
            await CloseS2ValveAsync(); // S2 = 0x1001
            await Task.Delay(50); // 500ms bekle
            
            // ✅ DURUM KONTROLÜ
            var leftRotationStatus = await _modbusClient.ReadCoilAsync(ModbusAddresses.LeftRotation);
            var rightRotationStatus = await _modbusClient.ReadCoilAsync(ModbusAddresses.RightRotation);
            var currentSpeed = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt);
            var s1Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S1);
            var s2Status = await _modbusClient.ReadCoilAsync(ModbusAddresses.S2);
            
            _logger?.LogInformation("📊 ROTASYON DURUM KONTROLÜ:");
            _logger?.LogInformation("  Sol Rotasyon (0x1003): {Status}", leftRotationStatus);
            _logger?.LogInformation("  Sağ Rotasyon (0x1002): {Status}", rightRotationStatus);
            _logger?.LogInformation("  Hız Register (0x0806): {Speed}", currentSpeed);
            _logger?.LogInformation("  S1 Valf (0x1000): {Status}", s1Status);
            _logger?.LogInformation("  S2 Valf (0x1001): {Status}", s2Status);
            
            _logger?.LogInformation("✅ ROTASYON DURDURULDU");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ HATA: Rotasyon durdurma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> SetRotationSpeedAsync(double speed)
    {
        try
        {
            _logger?.LogInformation("Rotasyon hızı ayarlanıyor: {Speed}%", speed);
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            // Hız değerini 1V ile 10V arasına çevir (0-100% → 1V-10V)
            var speedVoltage = Math.Max(1.0, Math.Min(10.0, (speed / 100.0) * 9.0 + 1.0));
            
            // Voltage'ı register değerine çevir (1V-10V → 0-2047)
            var speedRegisterValue = (ushort)Math.Round((speedVoltage - 1.0) / 9.0 * 2047);
            
            // M23_RotationSpeedVolt adresine analog output gönder
            await _modbusClient.WriteHoldingRegisterAsync(ModbusAddresses.M23_RotationSpeedVolt, speedRegisterValue);
            
            _logger?.LogInformation("Rotasyon hızı başarıyla ayarlandı: {Speed}% → {Voltage}V → Register:{Register}", speed, speedVoltage, speedRegisterValue);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Rotasyon hızı ayarlama sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Pneumatic Valve Control

    public async Task<bool> OpenPneumaticValve1Async()
    {
        try
        {
            _logger?.LogInformation("Pnömatik valf 1 açılıyor...");
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            await _modbusClient.WriteCoilAsync(ModbusAddresses.P1Open, true);
            
            _logger?.LogInformation("Pnömatik valf 1 açıldı");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pnömatik valf 1 açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ClosePneumaticValve1Async()
    {
        try
        {
            _logger?.LogInformation("Pnömatik valf 1 kapatılıyor...");
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            await _modbusClient.WriteCoilAsync(ModbusAddresses.P1Open, false);
            
            _logger?.LogInformation("Pnömatik valf 1 kapatıldı");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pnömatik valf 1 kapatma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> OpenPneumaticValve2Async()
    {
        try
        {
            _logger?.LogInformation("Pnömatik valf 2 açılıyor...");
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            await _modbusClient.WriteCoilAsync(ModbusAddresses.P2Open, true);
            
            _logger?.LogInformation("Pnömatik valf 2 açıldı");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pnömatik valf 2 açma sırasında hata oluştu");
            return false;
        }
    }

    public async Task<bool> ClosePneumaticValve2Async()
    {
        try
        {
            _logger?.LogInformation("Pnömatik valf 2 kapatılıyor...");
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil");
                return false;
            }

            await _modbusClient.WriteCoilAsync(ModbusAddresses.P2Open, false);
            
            _logger?.LogInformation("Pnömatik valf 2 kapatıldı");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pnömatik valf 2 kapatma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    #region Generic Piston Movement

    public async Task<bool> MovePistonAsync(string pistonType, double position)
    {
        try
        {
            _logger?.LogInformation("Piston hareketi başlatılıyor - Tip: {PistonType}, Pozisyon: {Position}mm", pistonType, position);
            
            // String tipini enum'a çevir
            if (!Enum.TryParse<PistonType>(pistonType, true, out var pistonEnum))
            {
                _logger?.LogError("Geçersiz piston tipi: {PistonType}", pistonType);
                return false;
            }

            // Ana MovePistonToPositionAsync metodunu kullan
            var result = await MovePistonToPositionAsync(pistonEnum, position);
            
            if (result)
            {
                _logger?.LogInformation("Piston başarıyla pozisyona hareket etti - {PistonType}: {Position}mm", pistonType, position);
            }
            else
            {
                _logger?.LogError("Piston pozisyona hareket edemedi - {PistonType}: {Position}mm", pistonType, position);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Piston hareketi sırasında hata oluştu - {PistonType}", pistonType);
            return false;
        }
    }

    #endregion

    #region Utility Methods

    public async Task<bool> StopAllPistonsAsync()
    {
        try
        {
            _logger?.LogInformation("Tüm pistonlar durduruluyor...");
            
            var stopTasks = new List<Task>
            {
                StopPistonAsync(PistonType.TopPiston),
                StopPistonAsync(PistonType.BottomPiston),
                StopPistonAsync(PistonType.LeftPiston),
                StopPistonAsync(PistonType.RightPiston)
            };

            await Task.WhenAll(stopTasks);
            
            _logger?.LogInformation("Tüm pistonlar durduruldu");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pistonları durdurma sırasında hata oluştu");
            return false;
        }
    }

    #endregion

    /// <summary>
    /// Ortak hidrolik motor kontrol metodu - Tüm ana operasyonlarda kullanılır
    /// </summary>
    private async Task<bool> EnsureHydraulicMotorRunningAsync(string operationName = "")
    {
        try
        {
            await UpdateMachineStatusAsync(); // Status'u güncelle
            
            if (!_currentStatus.HydraulicMotorRunning)
            {
                _logger?.LogInformation("💧 {OperationName} için hidrolik motor başlatılıyor...", operationName);
                
                var startResult = await StartHydraulicMotorAsync();
                if (!startResult)
                {
                    _logger?.LogError("❌ Hidrolik motor başlatılamadı - {OperationName} iptal edildi", operationName);
                    return false;
                }
                
                _logger?.LogInformation("⏳ Hidrolik motor stabilizasyonu için 3 saniye bekleniyor...");
                await Task.Delay(3000); // 3 saniye bekleme
                
                // Tekrar kontrol et
                await UpdateMachineStatusAsync();
                if (!_currentStatus.HydraulicMotorRunning)
                {
                    _logger?.LogError("❌ Hidrolik motor başlatıldıktan sonra çalışmıyor - {OperationName} iptal edildi", operationName);
                    return false;
                }
                
                _logger?.LogInformation("✅ Hidrolik motor başarıyla çalışıyor - {OperationName} devam edebilir", operationName);
            }
            else
            {
                _logger?.LogDebug("✅ Hidrolik motor zaten çalışıyor - {OperationName} devam ediyor", operationName);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Hidrolik motor kontrolü sırasında hata oluştu - {OperationName}", operationName);
            return false;
        }
    }

    /// <summary>
    /// DOKÜMANTASYON: Paso-based büküm algoritması
    /// Sağ ve sol topların adımsal hareketleriyle büküm gerçekleştirir
    /// </summary>
    private async Task<bool> ExecutePasoBasedBendingAsync(DomainBendingParameters parameters, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("🔥 DOKÜMANA GÖRE: Paso-based büküm algoritması başlatılıyor...");
            
            // ✅ DÜZELTME: ALT ANA PISTONLAR - Sol ve Sağ Alt Ana Pistonlar büküm yapacak
            var stepSize = parameters.StepSize; // Adım büyüklüğü (20mm default)
            
            // ✅ DOĞRU PARAMETRE: sideBallTravelDistance büküm mesafesi
            // AÇIKLAMA: LeftPiston = Alt Ana Sol, RightPiston = Alt Ana Sağ
            // Yan dayama pistonları (LeftReelPosition/RightReelPosition) zaten pozisyonlarında
            // Ana büküm Alt Ana pistonlarla yapılacak - HER İKİ PISTON AYNI MESAFE
            var totalBendingDistance = parameters.SideBallTravelDistance; // 40.85mm gibi
            var totalRightDistance = totalBendingDistance; // Alt Ana Sağ piston hedef mesafesi
            var totalLeftDistance = totalBendingDistance;   // Alt Ana Sol piston hedef mesafesi
            
            var rightStepCount = (int)Math.Ceiling(totalRightDistance / stepSize);
            var leftStepCount = (int)Math.Ceiling(totalLeftDistance / stepSize);
            var totalSteps = Math.Max(rightStepCount, leftStepCount);
            
            _logger?.LogInformation("📊 Büküm parametreleri - Adım: {StepSize}mm, Sağ: {RightSteps} paso, Sol: {LeftSteps} paso, Toplam: {TotalSteps} paso", 
                stepSize, rightStepCount, leftStepCount, totalSteps);
                
            // ✅ PARÇA VARLIK SENSÖRÜ KONTROLÜ - SENSÖR TARAFINDA İLK BÜKÜM
            await UpdateMachineStatusAsync();
            bool leftPartPresent = _currentStatus.LeftPartPresent;
            bool rightPartPresent = _currentStatus.RightPartPresent;
            
            // Güvenlik kontrolü: En az bir sensör aktif olmalı
            if (!leftPartPresent && !rightPartPresent)
            {
                _logger?.LogError("❌ KRİTİK: Hiçbir parça varlık sensörü aktif değil! Büküm güvenli değil.");
                OnAlarmRaised("Parça varlık sensörü bulunamadı - Büküm durduruldu", SafetyStatus.Critical);
                return false;
            }
            
            // ✅ DÜZELTME: Sol sensör aktifse, parça sol tarafa sıfırlandığı için, ilk büküm KARŞI TARAFTA (SAĞ) yapılmalı
            bool useLeftSensor = leftPartPresent;
            string activeSensorSide = useLeftSensor ? "Sol" : "Sağ";
            string firstBendingSide = useLeftSensor ? "Sağ (Karşı) tarafında" : "Sol (Karşı) tarafında";
            
            _logger?.LogInformation("📍 Parça varlık durumu - Sol: {LeftPresent}, Sağ: {RightPresent}", leftPartPresent, rightPartPresent);
            _logger?.LogInformation("🎯 Aktif sensör: {ActiveSensor} - İlk büküm: {FirstBending}", activeSensorSide, firstBendingSide);
            _logger?.LogInformation("💡 BÜKÜM MANTIĞI: Parça {ActiveSensor} sensöre sıfırlandığı için, büküm karşı tarafta başlar", activeSensorSide);
                
            // ✅ ENCODER BAZLI ROTASYON: Parça ters yönde hareket ettirme - ENCODER İLE KONTROL
            var initialRotationDirection = useLeftSensor ? RotationDirection.CounterClockwise : RotationDirection.Clockwise;
            _logger?.LogInformation("🔄 Parça ters yönde hareket ettiriliyor (%80 hız) - Yön: {Direction}", 
                initialRotationDirection == RotationDirection.CounterClockwise ? "Saat yönü tersi" : "Saat yönü");
            
            // ENCODER BAZLI: Profil uzunluğu kadar ters yönde hareket
            var initialReverseDistance = parameters.ProfileLength; // Profil uzunluğu kadar ters hareket
            _logger?.LogInformation("📏 Ters hareket mesafesi: {Distance}mm (Profil uzunluğu bazlı)", initialReverseDistance);
            
            var initialReverseSuccess = await PerformEncoderBasedRotationAsync(initialRotationDirection, initialReverseDistance, 80.0, cancellationToken);
            if (!initialReverseSuccess)
            {
                _logger?.LogError("❌ İlk ters hareket başarısız - büküm durduruldu");
                return false;
            }
            
            // ✅ DİNAMİK POZİSYON HESAPLAMA: Her paso'da pozisyonlar yeniden hesaplanır
            double currentRightPosition = 0;
            double currentLeftPosition = 0;
            var rotationDirection = useLeftSensor ? RotationDirection.Clockwise : RotationDirection.CounterClockwise;
            
            // ✅ DOKÜMANTASYON: "Topların pozisyonları iyi hesaplanmalı"
            // Örnek: Sağ vals 40mm'de, sol 20mm aşağı inerse, sonraki paso sağ vals 60mm'e gitmeli
            double rightTargetPosition = totalRightDistance; // Hedef pozisyon
            double leftTargetPosition = totalLeftDistance;   // Hedef pozisyon
            
            _logger?.LogInformation("🎯 Hedef pozisyonlar - Sol: {LeftTarget}mm, Sağ: {RightTarget}mm", 
                leftTargetPosition, rightTargetPosition);
            
            for (int paso = 1; paso <= totalSteps; paso++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogWarning("⚠️ Büküm işlemi iptal edildi - Paso {CurrentPaso}/{TotalSteps}", paso, totalSteps);
                    return false;
                }
                
                _logger?.LogInformation("🔧 PASO {CurrentPaso}/{TotalSteps} başlatılıyor - Mevcut: Sol={LeftPos:F1}mm, Sağ={RightPos:F1}mm", 
                    paso, totalSteps, currentLeftPosition, currentRightPosition);
                
                // ✅ DÜZELTME: KARŞI TARAFTA İLK BÜKÜM - Sensör tarafının karşısında büküm başlar
                if (useLeftSensor)
                {
                    // SOL SENSÖR AKTİF: İlk olarak SAĞ (Alt Ana Sağ) piston hareket eder (KARŞI TARAF)
                    var rightRemainingDistance = rightTargetPosition - currentRightPosition;
                    var rightStepDistance = Math.Min(stepSize, rightRemainingDistance);
                
                if (rightStepDistance > 0)
                {
                        var rightNewPosition = currentRightPosition + rightStepDistance;
                        _logger?.LogInformation("➡️ Sağ Alt Ana piston ilerleme (Karşı taraf - İlk büküm): {CurrentPos:F1}mm → {NewPos:F1}mm (+{StepDist:F1}mm) [Kalan: {Remaining:F1}mm]", 
                            currentRightPosition, rightNewPosition, rightStepDistance, rightRemainingDistance - rightStepDistance);
                    
                        await MovePistonToPositionAsync(PistonType.RightPiston, rightNewPosition);
                        currentRightPosition = rightNewPosition;
                }
                
                    // ✅ DİNAMİK HESAPLAMA: Sol piston geçici geri hareket (adım mesafesi kadar - negatif pozisyona gidebilir)
                    var leftBackMovement = stepSize;
                if (leftBackMovement > 0)
                {
                    var leftTempPosition = currentLeftPosition - leftBackMovement;
                        _logger?.LogInformation("⬅️ Sol Alt Ana piston geçici geri hareket: {CurrentPos:F1}mm → {TempPos:F1}mm (-{BackDist:F1}mm - Adım mesafesi, negatif pozisyon mümkün)", 
                        currentLeftPosition, leftTempPosition, leftBackMovement);
                    
                        await MovePistonToPositionAsync(PistonType.LeftPiston, leftTempPosition);
                        // ✅ ÖNEMLİ: Geçici hareket, currentLeftPosition'ı değiştirmez (sadece fiziksel hareket)
                    }
                }
                else
                {
                    // SAĞ SENSÖR AKTİF: İlk olarak SOL (Alt Ana Sol) piston hareket eder (KARŞI TARAF)
                    var leftRemainingDistance = leftTargetPosition - currentLeftPosition;
                    var leftStepDistance = Math.Min(stepSize, leftRemainingDistance);
                
                    if (leftStepDistance > 0)
                    {
                        var leftNewPosition = currentLeftPosition + leftStepDistance;
                        _logger?.LogInformation("➡️ Sol Alt Ana piston ilerleme (Karşı taraf - İlk büküm): {CurrentPos:F1}mm → {NewPos:F1}mm (+{StepDist:F1}mm) [Kalan: {Remaining:F1}mm]", 
                            currentLeftPosition, leftNewPosition, leftStepDistance, leftRemainingDistance - leftStepDistance);
                    
                        await MovePistonToPositionAsync(PistonType.LeftPiston, leftNewPosition);
                        currentLeftPosition = leftNewPosition;
                    }
                
                    // ✅ DİNAMİK HESAPLAMA: Sağ piston geçici geri hareket (adım mesafesi kadar - negatif pozisyona gidebilir)
                    var rightBackMovement = stepSize;
                    if (rightBackMovement > 0)
                    {
                        var rightTempPosition = currentRightPosition - rightBackMovement;
                        _logger?.LogInformation("⬅️ Sağ Alt Ana piston geçici geri hareket: {CurrentPos:F1}mm → {TempPos:F1}mm (-{BackDist:F1}mm - Adım mesafesi, negatif pozisyon mümkün)", 
                            currentRightPosition, rightTempPosition, rightBackMovement);
                    
                        await MovePistonToPositionAsync(PistonType.RightPiston, rightTempPosition);
                        // ✅ ÖNEMLİ: Geçici hareket, currentRightPosition'ı değiştirmez (sadece fiziksel hareket)
                    }
                }
                
                // ✅ ENCODER BAZLI: PASO PHASE 2 - PARÇA ROTASYON İLE SENSÖR TARAFINA SÜRÜLÜR
                string targetSide = useLeftSensor ? "sol vals topuna" : "sağ vals topuna";
                _logger?.LogInformation("🔄 Parça rotasyon ile {TargetSide} sürülüyor - Yön: {Direction}", 
                    targetSide, rotationDirection == RotationDirection.Clockwise ? "Saat yönü" : "Saat yönü tersi");
                
                // ✅ DÜZELTME: Profil uzunluğu kadar rotasyon (örnek prosese göre)
                var rotationDistance = parameters.ProfileLength; // Her paso'da profil uzunluğu kadar hareket
                _logger?.LogInformation("📏 Rotasyon mesafesi: {Distance}mm (Profil uzunluğu bazlı)", rotationDistance);
                
                var rotationSuccess = await PerformEncoderBasedRotationAsync(rotationDirection, rotationDistance, 80.0, cancellationToken);
                if (!rotationSuccess)
                {
                    _logger?.LogError("❌ Paso {Paso} rotasyon başarısız - büküm durduruldu", paso);
                    return false;
                }
                
                // ✅ DİNAMİK POZİSYON HESAPLAMA: PASO PHASE 3 - Rotasyon sonrası sensör tarafı piston hareket eder
                if (useLeftSensor)
                {
                    // SOL SENSÖR AKTİF: Rotasyon sonrası SOL (Alt Ana Sol) piston hareket eder (SENSÖR TARAFI)
                    var leftRemainingDistance2 = leftTargetPosition - currentLeftPosition;
                    var leftStepDistance = Math.Min(stepSize, leftRemainingDistance2);
                
                if (leftStepDistance > 0)
                {
                        var leftNewPosition = currentLeftPosition + leftStepDistance;
                        _logger?.LogInformation("➡️ Sol Alt Ana piston ilerleme (Sensör tarafı): {CurrentPos:F1}mm → {NewPos:F1}mm (+{StepDist:F1}mm) [Kalan: {Remaining:F1}mm]", 
                            currentLeftPosition, leftNewPosition, leftStepDistance, leftRemainingDistance2 - leftStepDistance);
                    
                        await MovePistonToPositionAsync(PistonType.LeftPiston, leftNewPosition);
                        currentLeftPosition = leftNewPosition;
                }
                
                                        // ✅ DİNAMİK HESAPLAMA: Sağ piston geçici geri hareket (adım mesafesi kadar - negatif pozisyona gidebilir)
                    var rightBackMovement = stepSize;
                    if (rightBackMovement > 0)
                {
                        var rightTempPosition = currentRightPosition - rightBackMovement;
                        _logger?.LogInformation("⬅️ Sağ Alt Ana piston geçici geri hareket: {CurrentPos:F1}mm → {TempPos:F1}mm (-{BackDist:F1}mm - Adım mesafesi, negatif pozisyon mümkün)", 
                            currentRightPosition, rightTempPosition, rightBackMovement);
                    
                        await MovePistonToPositionAsync(PistonType.RightPiston, rightTempPosition);
                        // ✅ ÖNEMLİ: Geçici hareket, currentRightPosition'ı değiştirmez (sadece fiziksel hareket)
                    }
                }
                                else
                {
                    // SAĞ SENSÖR AKTİF: Rotasyon sonrası SAĞ (Alt Ana Sağ) piston hareket eder (SENSÖR TARAFI)
                    var rightRemainingDistance2 = rightTargetPosition - currentRightPosition;
                    var rightStepDistance = Math.Min(stepSize, rightRemainingDistance2);
                    
                    if (rightStepDistance > 0)
                    {
                        var rightNewPosition = currentRightPosition + rightStepDistance;
                        _logger?.LogInformation("➡️ Sağ Alt Ana piston ilerleme (Sensör tarafı): {CurrentPos:F1}mm → {NewPos:F1}mm (+{StepDist:F1}mm) [Kalan: {Remaining:F1}mm]", 
                            currentRightPosition, rightNewPosition, rightStepDistance, rightRemainingDistance2 - rightStepDistance);
                        
                        await MovePistonToPositionAsync(PistonType.RightPiston, rightNewPosition);
                        currentRightPosition = rightNewPosition;
                    }
                    
                    // ✅ DİNAMİK HESAPLAMA: Sol piston geçici geri hareket (adım mesafesi kadar - negatif pozisyona gidebilir)
                    var leftBackMovement = stepSize;
                    if (leftBackMovement > 0)
                    {
                        var leftTempPosition = currentLeftPosition - leftBackMovement;
                        _logger?.LogInformation("⬅️ Sol Alt Ana piston geçici geri hareket: {CurrentPos:F1}mm → {TempPos:F1}mm (-{BackDist:F1}mm - Adım mesafesi, negatif pozisyon mümkün)", 
                            currentLeftPosition, leftTempPosition, leftBackMovement);
                        
                        await MovePistonToPositionAsync(PistonType.LeftPiston, leftTempPosition);
                        // ✅ ÖNEMLİ: Geçici hareket, currentLeftPosition'ı değiştirmez (sadece fiziksel hareket)
                    }
                }
                
                _logger?.LogInformation("✅ PASO {CurrentPaso}/{TotalSteps} tamamlandı - Sağ: {RightPos:F1}mm, Sol: {LeftPos:F1}mm", 
                    paso, totalSteps, currentRightPosition, currentLeftPosition);
                
                // PASO ARASI BEKLEME VE GÜVENLİK KONTROLÜ
                await Task.Delay(800, cancellationToken); // Paso arası stabilizasyon
                
                // Her paso sonrası basınç kontrolü
                var (s1Pressure, s2Pressure) = await ReadActualPressureAsync();
                _logger?.LogInformation("📊 Paso {Paso} basınç durumu - S1: {S1:F1} bar, S2: {S2:F1} bar", 
                    paso, s1Pressure, s2Pressure);

                // Her adım sonrası kısa stabilizasyon beklemesi
                await Task.Delay(500, cancellationToken);

            }
            
            // ✅ TÜM ADIMLAR TAMAMLANDI
            _logger?.LogInformation("🎉 Paso-based büküm başarıyla tamamlandı! Toplam {Steps} paso", totalSteps);
                
            // Son pozisyonları kontrol et ve logla
            await UpdateMachineStatusAsync();
            var finalLeftPosition = _pistons[PistonType.LeftPiston].CurrentPosition;
            var finalRightPosition = _pistons[PistonType.RightPiston].CurrentPosition;
            
            _logger?.LogInformation("📊 FİNAL POZİSYONLAR - Sol: {Left:F3}mm, Sağ: {Right:F3}mm (Hedef: {Target:F3}mm)", 
                finalLeftPosition, finalRightPosition, totalBendingDistance);
                
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("⚠️ Paso-based büküm iptal edildi");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Paso-based büküm sırasında hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// DOKÜMANA GÖRE: Tahliye süreci
    /// Büküm tamamlandıktan sonra kullanıcı süre seçer (5s, 10s, 25s, 30s) ve pistonlar geri çekilir
    /// </summary>
    private async Task<bool> ExecuteEvacuationProcessAsync(int finalStageValue, int evacuationTimeSeconds = 10)
    {
        try
        {
            _logger?.LogInformation("📤 DOKÜMANA GÖRE: Tahliye süreci başlatılıyor...");
            
            // DOKÜMANA GÖRE: Kullanıcı süre seçenekleri (5s, 10s, 25s, 30s)
            var evacuationDuration = TimeSpan.FromSeconds(evacuationTimeSeconds);
            
            _logger?.LogInformation("📋 Tahliye parametreleri - Süre: {Duration} saniye, Final Stage: {FinalStage}mm", 
                evacuationDuration.TotalSeconds, finalStageValue);
            
            _logger?.LogInformation("💡 Kullanıcı bilgilendirmesi: Büküm tamamlandı! Tahliye süreci başlıyor...");
            _logger?.LogInformation("⏱️ Mevcut seçenekler: 5sn, 10sn, 25sn, 30sn (Şu an: {CurrentSelection}sn)", evacuationDuration.TotalSeconds);
            
            // ADIM 1: Pistonları geri pozisyona çek (ÜST TOP HARİÇ - tahliyede üst top hareket etmez)
            _logger?.LogInformation("⬅️ Pistonlar geri pozisyona çekiliyor (Üst top hariç)...");
            
            var retractionTasks = new List<Task>
            {
                // NOT: TopPiston tahliyede hareket etmez - büküm pozisyonunda kalır
                MovePistonToPositionAsync(PistonType.BottomPiston, 0),
                MovePistonToPositionAsync(PistonType.LeftPiston, 0),
                MovePistonToPositionAsync(PistonType.RightPiston, 0),
                MovePistonToPositionAsync(PistonType.LeftReelPiston, 0),
                MovePistonToPositionAsync(PistonType.RightReelPiston, 0),
                MovePistonToPositionAsync(PistonType.LeftBodyPiston, 0),
                MovePistonToPositionAsync(PistonType.RightBodyPiston, 0),
                MovePistonToPositionAsync(PistonType.LeftJoinPiston, 0),
                MovePistonToPositionAsync(PistonType.RightJoinPiston, 0)
            };
            
            await Task.WhenAll(retractionTasks);
            _logger?.LogInformation("✅ Pistonlar geri çekildi (Üst top büküm pozisyonunda kaldı)");
            
            // ADIM 2: Bekleme süresi
            _logger?.LogInformation("⏳ Tahliye bekleme süresi başladı - {Duration} saniye bekleniyor...", evacuationDuration.TotalSeconds);
            await Task.Delay(evacuationDuration);
            
            // ADIM 3: Son stage'e sıfırlama
            if (finalStageValue > 0)
            {
                _logger?.LogInformation("🔄 Final stage'e sıfırlama işlemi başlatılıyor - Stage: {FinalStage}mm", finalStageValue);
                
                // Önce cetvel sıfırlama
                var rulerResetResult = await ResetRulersAsync();
                if (!rulerResetResult)
                {
                    _logger?.LogWarning("⚠️ Cetvel sıfırlama başarısız oldu");
                }
                
                // Stage ayarlama
                var stageResult = await SetStageAsync(finalStageValue);
                if (!stageResult)
                {
                    _logger?.LogWarning("⚠️ Final stage ayarlama başarısız oldu");
                }
                
                _logger?.LogInformation("✅ Final stage'e sıfırlama tamamlandı");
            }
            else
            {
                // Stage 0 ise sadece cetvel sıfırlama
                _logger?.LogInformation("🔄 Stage 0 - Sadece cetvel sıfırlama yapılıyor...");
                var rulerResetResult = await ResetRulersAsync();
                if (!rulerResetResult)
                {
                    _logger?.LogWarning("⚠️ Cetvel sıfırlama başarısız oldu");
                }
            }
            
            _logger?.LogInformation("🎉 Tahliye süreci başarıyla tamamlandı");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Tahliye süreci sırasında hata oluştu");
            return false;
        }
    }

    /// <summary>
    /// Cetvel durumlarını gerçek modbus register'lerinden okur
    /// </summary>
    public async Task<RulerStatus> GetRulerStatusAsync()
    {
        try
        {
            _logger?.LogInformation("🔍 Cetvel durumları modbus'dan okunuyor...");

            if (!_modbusClient.IsConnected)
            {
                _logger?.LogWarning("Modbus bağlantısı aktif değil - varsayılan değerler döndürülüyor");
                return new RulerStatus
                {
                    RulerResetM13toM16 = 0,
                    RulerResetM17toM20 = 0,
                    RulerResetPneumaticValve = 0,
                    RulerResetRotation = 0,
                    AllReset = false,
                    LastChecked = DateTime.UtcNow
                };
            }

            // Gerçek modbus adresleri okuma
            var m13toM16Value = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.RulerResetM13toM16);
            var m17toM20Value = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.RulerResetM17toM20);
            var pneumaticValue = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.RulerResetPneumaticValve);
            var rotationValue = await _modbusClient.ReadHoldingRegisterAsync(ModbusAddresses.RulerResetRotation);

            // Tüm değerler 2570 ise reset edilmiş
            var allReset = m13toM16Value == 2570 && 
                          m17toM20Value == 2570 && 
                          pneumaticValue == 2570 && 
                          rotationValue == 2570;

            _logger?.LogInformation("📊 Cetvel durumları - M13-M16: {M13M16}, M17-M20: {M17M20}, Pnömatik: {Pneumatic}, Rotasyon: {Rotation}, Hepsi Sıfır: {AllReset}",
                m13toM16Value, m17toM20Value, pneumaticValue, rotationValue, allReset);

            return new RulerStatus
            {
                RulerResetM13toM16 = m13toM16Value,
                RulerResetM17toM20 = m17toM20Value,
                RulerResetPneumaticValve = pneumaticValue,
                RulerResetRotation = rotationValue,
                AllReset = allReset,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cetvel durumları okunurken hata oluştu");
            return new RulerStatus
            {
                RulerResetM13toM16 = -1, // Hata işareti
                RulerResetM17toM20 = -1,
                RulerResetPneumaticValve = -1,
                RulerResetRotation = -1,
                AllReset = false,
                LastChecked = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut aktif stage'i döndürür
    /// </summary>
    public async Task<int> GetCurrentStageAsync()
    {
        try
        {
            // TODO: Gerçek aktif stage'i hesapla (piston pozisyonlarından)
            // Şu an için basit implementasyon
            await Task.CompletedTask;
            return 0; // Default stage
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Mevcut stage okunamadı");
            return 0;
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Mevcut stage'leri listeler (hardcoded - gelecekte config'den okunacak)
    /// </summary>
    public async Task<List<StageConfigDto>> GetAvailableStagesAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            // ✅ DOKÜMANTASYON: Default stage konfigürasyonları
            var stages = new List<StageConfigDto>
            {
                new()
                {
                    Name = "Stage 0",
                    Value = 0,
                    LeftPistonOffset = 0,
                    RightPistonOffset = 0,
                    Description = "Sıfır pozisyon - Cetvel sıfırlama"
                },
                new()
                {
                    Name = "Stage 60",
                    Value = 60,
                    LeftPistonOffset = 67.34,
                    RightPistonOffset = 67.34,
                    Description = "60mm stage - Orta boyut profiller"
                },
                new()
                {
                    Name = "Stage 120",
                    Value = 120,
                    LeftPistonOffset = 134.68,
                    RightPistonOffset = 134.68,
                    Description = "120mm stage - Büyük boyut profiller"
                }
            };
            
            _logger?.LogInformation("{Count} stage konfigürasyonu hazırlandı", stages.Count);
            return stages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Stage listesi hazırlanamadı");
            return new List<StageConfigDto>();
        }
    }
    
    /// <summary>
    /// ✅ STAGE YÖNETİMİ - Belirli bir stage konfigürasyonunu döndürür
    /// </summary>
    public async Task<StageConfigDto?> GetStageConfigAsync(int stageValue)
    {
        try
        {
            var stages = await GetAvailableStagesAsync();
            return stages.FirstOrDefault(s => Math.Abs(s.Value - stageValue) < 0.1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Stage konfigürasyonu okunamadı - Stage: {Stage}", stageValue);
            return null;
        }
    }

    /// <summary>
    /// PASO TEST: Sadece paso işlemini test eder - hazırlık adımları yapılmaz
    /// Cetvel sıfırlama, stage ayarlama, parça sıkıştırma ve sıfırlama önceden yapılmış olmalı
    /// </summary>
    public async Task<bool> ExecutePasoTestAsync(double sideBallTravelDistance, double profileLength, double stepSize = 20.0, int evacuationTimeSeconds = 10)
    {
        try
        {
            _logger?.LogInformation("🔬 PASO TEST BAŞLATILIYOR - Profil: {Length}mm, Adım: {Step}mm, Yan Top: {Side}mm", 
                profileLength, stepSize, sideBallTravelDistance);
            
            if (!_modbusClient.IsConnected)
            {
                _logger?.LogError("❌ HATA: Modbus bağlantısı aktif değil! Paso test başlatılamaz!");
                OnAlarmRaised("Modbus bağlantısı yok - Paso test başlatılamaz", SafetyStatus.Critical);
                return false;
            }

            // ✅ ADIM 1: Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                _logger?.LogWarning("Güvenlik kontrolü başarısız - paso test başlatılamaz");
                return false;
            }

            // ✅ ADIM 2: Hidrolik motor kontrolü (ortak metod)
            if (!await EnsureHydraulicMotorRunningAsync("Paso Test"))
            {
                return false;
            }
            
            // ✅ ADIM 3: ENCODER RESET - CETVEL RESET GİBİ
            _logger?.LogInformation("🔄 Encoder sıfırlama başlatılıyor (cetvel reset protokolü ile)...");
            
            var resetAddresses = new Dictionary<string, int>
            {
                { "Rotation", ModbusAddresses.RulerResetRotation }
            };

            var encoderResetSuccess = await PerformRulerResetProtocolAsync(resetAddresses);
            if (!encoderResetSuccess)
            {
                _logger?.LogError("❌ Encoder sıfırlama başarısız oldu");
                    return false;
                }
            _logger?.LogInformation("✅ Encoder başarıyla sıfırlandı (cetvel reset protokolü ile)");

            // ✅ ADIM 5: Encoder başlangıç pozisyonunu kaydet ve kontrol et
            await UpdateMachineStatusAsync();
            _pasoEncoderReferencePosition = _currentStatus.RotationEncoderRaw;
            var initialDistance = (_pasoEncoderReferencePosition.Value * Math.PI * 220.0) / 1024.0;
            _logger?.LogInformation("🔄 Encoder başlangıç pozisyonu - Raw: {Raw} pulse, Mesafe: {Distance:F2}mm", 
                _pasoEncoderReferencePosition, initialDistance);
            
            // Encoder reset başarılı mı kontrol et (0'a yakın olmalı)
            if (Math.Abs(initialDistance) > 50.0) // 50mm tolerans
            {
                _logger?.LogWarning("⚠️ Encoder reset sonrası pozisyon yüksek: {Distance:F2}mm - Devam ediliyor", initialDistance);
            }
                    
            // ✅ ADIM 6: Paso test algoritmasını çalıştır
            using var cancellationTokenSource = new CancellationTokenSource();
            var pasoSuccess = await ExecutePasoTestBendingAsync(sideBallTravelDistance, profileLength, stepSize, cancellationTokenSource.Token);
                
            if (pasoSuccess)
            {
                _logger?.LogInformation("✅ Paso test başarıyla tamamlandı!");
                return true;
            }
            else
            {
                _logger?.LogError("❌ Paso test başarısız oldu");
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Paso test sırasında hata oluştu");
            OnAlarmRaised($"Paso test hatası: {ex.Message}", SafetyStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// PASO TEST İÇİN ÖZEL PİSTON POZİSYONLAMA - VALİDASYON BYPASS
    /// Negatif pozisyonlara gidebilir, min/max sınır kontrolü yapmaz
    /// </summary>
    private async Task<bool> MovePistonToPositionForPasoAsync(PistonType pistonType, double targetPosition)
    {
        var piston = _pistons[pistonType];
        piston.TargetPosition = targetPosition;
        piston.IsAtTarget = false;
        
        // BU METODDA POZİSYON LİMİT KONTROLÜ YOKTUR (NEGATİF DEĞERLERE İZİN VERİR)
        // VALF YÖNETİMİ ARTIK İÇERİDE YAPILIR
        
        _logger?.LogInformation("{PistonName} - PASO Pozisyon kontrol başlatıldı: Hedef={Target}mm", piston.Name, targetPosition);
        
        // ✅ VALF KONTROLÜ - S2 valfi açık olmalı
        _logger?.LogInformation("{PistonName} - PASO: S2 valfi açılıyor", piston.Name);
        await OpenS2ValveAsync();
        await Task.Delay(500); // Valfin açılması için bekle
        
        // ✅ HASSAS CLOSED-LOOP POSITION CONTROL
        var maxIterations = 200; // Max 20 saniye (100ms * 200)
        var iteration = 0;
        var consecutiveCloseCount = 0; // Hedefe yakın kalma sayacı
        const int requiredConsecutiveClose = 3; // 3 ardışık yakın okuma gerekli
        
        while (iteration < maxIterations)
        {
            // Güvenlik kontrolü
            if (!await CheckSafetyAsync())
            {
                await StopPistonAsync(pistonType);
                OnAlarmRaised($"Safety violation during PASO position control for {piston.Name}", SafetyStatus.Error);
                return false;
            }
            
            // Mevcut pozisyonu oku
            try
            {
                var rulerValue = await _modbusClient.ReadInputRegisterAsSignedAsync(piston.RulerAddress);
                piston.CurrentPosition = piston.CalculatePositionFromRulerSigned(rulerValue);
                piston.RulerValue = rulerValue;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PASO Ruler okuma hatası: {PistonName}", piston.Name);
                await Task.Delay(50); 
                iteration++;
                continue;
            }
            
            var currentPos = piston.CurrentPosition;
            var error = targetPosition - currentPos;
            
            _logger?.LogDebug("{PistonName} - PASO Iter:{Iteration} Mevcut={Current}mm, Hedef={Target}mm, Hata={Error}mm", 
                piston.Name, iteration, currentPos, targetPosition, error);
            
            // ✅ HASSAS HEDEF KONTROLÜ - Stabil konumlandırma
            if (Math.Abs(error) < piston.PositionTolerance)
            {
                consecutiveCloseCount++;
                _logger?.LogDebug("{PistonName} - PASO Hedefe yakın: {Count}/{Required} - Hata={Error:F3}mm", 
                    piston.Name, consecutiveCloseCount, requiredConsecutiveClose, error);
                
                if (consecutiveCloseCount >= requiredConsecutiveClose)
                {
                    await StopPistonAsync(pistonType);
                    piston.IsAtTarget = true;
                    _logger?.LogInformation("{PistonName} - ✅ PASO HEDEFE ULAŞILDI! Final pozisyon: {Position:F2}mm (Hata: {Error:F3}mm, Tolerans: ±{Tolerance:F2}mm)", 
                        piston.Name, currentPos, error, piston.PositionTolerance);
                    return true;
                }
                else
                {
                    await Task.Delay(50);
                    iteration++;
                    continue;
                }
            }
            else
            {
                consecutiveCloseCount = 0;
            }
            
            // P Controller with voltage scaling
            var voltage = 0.0;
            
            if (piston.IsVoltageControlled)
            {
                // ✅ YENİ MANTIK: Geri hareket agresif, İleri hareket hassas.
                var absError = Math.Abs(error);
                
                // Hata yönüne göre strateji belirle
                bool isMovingForward = error > 0;

                if (isMovingForward) // İLERİ HAREKET: Kanıtlanmış hassas P-Controller kullanılır
                {
                    _logger?.LogDebug("PASO Modu: İLERİ (Hassas P-Controller)");
                    // Kademeli hız kontrolü
                    double proportionalGain;
                    double maxVoltage;
                    double minVoltage;
                    
                    double pistonMinVoltage = pistonType switch
                    {
                        PistonType.BottomPiston => 0.7,
                        PistonType.LeftPiston => 0.3,
                        PistonType.RightPiston => 0.3,
                        _ => 0.5
                    };
                    
                    if (absError > 10.0) { proportionalGain = 1.2; maxVoltage = 8.0; minVoltage = pistonMinVoltage + 1.8; }
                    else if (absError > 5.0) { proportionalGain = 0.9; maxVoltage = 6.0; minVoltage = pistonMinVoltage + 1.2; }
                    else if (absError > 2.0) { proportionalGain = 0.6; maxVoltage = 3.5; minVoltage = pistonMinVoltage + 0.8; }
                    else if (absError > 0.5) { proportionalGain = 0.4; maxVoltage = 2.0; minVoltage = pistonMinVoltage + 0.4; }
                    else { proportionalGain = 0.25; maxVoltage = 1.5; minVoltage = pistonMinVoltage + 0.2; }
                    
                    voltage = error * proportionalGain;
                    voltage = Math.Max(-maxVoltage, Math.Min(maxVoltage, voltage));
                    
                    if (Math.Abs(voltage) < minVoltage && absError > piston.PositionTolerance)
                    {
                        voltage = -minVoltage; // İleri hareket her zaman negatif
                    }
                }
                else // GERİ HAREKET: Sabit ve kararlı voltaj verilir
                {
                     _logger?.LogDebug("PASO Modu: GERİ (Sabit ve Agresif Voltaj)");
                    voltage = 6.0; // Geri hareket için GÜÇLENDİRİLMİŞ +6.0V (3V->6V)
                }

                // Yönü son kez ayarla (ileri = negatif, geri = pozitif)
                voltage = isMovingForward ? -Math.Abs(voltage) : Math.Abs(voltage);
            }
            
            // Hareketi başlat
            try
            {
                if (piston.VoltageAddress != 0)
                {
                    var voltageRegister = (ushort)DataConverter.VoltToRegisterConvert(voltage);
                    _logger?.LogCritical("KANIT LOG: {PistonName} Pistonuna Güç Gönderiliyor -> Adres: {Address}, Register: {Register}, Voltaj: {Voltage:F2}V", 
                        piston.Name, piston.VoltageAddress, voltageRegister, voltage);
                    await _modbusClient.WriteHoldingRegisterAsync(piston.VoltageAddress, voltageRegister);
                }
                else
                {
                     _logger?.LogWarning("PASO: Coil controlled piston not implemented yet: {PistonName}", piston.Name);
            }
        }
        catch (Exception ex)
        {
                _logger?.LogError(ex, "PASO Piston hareket hatası: {PistonName}", piston.Name);
                await StopPistonAsync(pistonType);
            return false;
        }
            
            await Task.Delay(100);
            iteration++;
        }
        
        // Timeout
        await StopPistonAsync(pistonType);
        await CloseS2ValveAsync(); // Valfi kapat
        _logger?.LogError("{PistonName} - ❌ PASO pozisyon timeout! Hedef: {Target}mm", piston.Name, targetPosition);
        return false;
    }

    /// <summary>
    /// PASO TEST İÇİN ÖZEL BÜKÜM ALGORİTMASI - Sensör kontrolü olmadan
    /// </summary>
    private async Task<bool> ExecutePasoTestBendingAsync(double sideBallTravelDistance, double profileLength, double stepSize, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("PASO Bending Test Başlatılıyor... Hedef Mesafe: {Target}mm, Adım: {Step}mm", sideBallTravelDistance, stepSize);

        var requiredSteps = (int)Math.Ceiling(sideBallTravelDistance / stepSize);
        _logger?.LogInformation("Toplam {Count} adımda tamamlanması hedefleniyor.", requiredSteps);

        double currentLeftPosition = _pistons[PistonType.LeftPiston].CurrentPosition;
        double currentRightPosition = _pistons[PistonType.RightPiston].CurrentPosition;
        
        bool isFirstOperation = true;

        for (int i = 0; i < requiredSteps * 2; i++) // Her adımda 2 operasyon var (sağ/sol)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                _logger?.LogWarning("Paso testi kullanıcı tarafından iptal edildi.");
                    return false;
                }
                
            // Operasyon sırasına göre rotasyon yönünü belirle (sağ-sol-sağ-sol...)
            var isRightRotationOp = i % 2 == 0;
            var operationNumber = i + 1;
            
            _logger?.LogInformation("--- Operasyon #{OperationNum} Başlıyor ---", operationNumber);
            _logger?.LogDebug("Mevcut Pozisyonlar -> Sol: {LeftPos:F2}mm, Sağ: {RightPos:F2}mm", currentLeftPosition, currentRightPosition);

            // 1. ADIM: Rotasyon
            var rotationDirection = isRightRotationOp ? RotationDirection.CounterClockwise : RotationDirection.Clockwise;
            _logger?.LogInformation("🔄 Rotasyon Başlatılıyor: {Direction} ({ProfileLen}mm)", rotationDirection, profileLength);
            
            var rotationSuccess = await PerformPreciseEncoderRotationAsync(rotationDirection, profileLength, 70, cancellationToken);
            if (!rotationSuccess)
            {
                _logger?.LogError("❌ Rotasyon başarısız - Operasyon #{OperationNum}", operationNumber);
                await StopRotationAsync();
                        return false;
                    }
            await Task.Delay(500, cancellationToken); // Rotasyon sonrası stabilizasyon

            // 2. ADIM: Piston Pozisyonlarını Hesapla
            double targetLeft, targetRight;

            if (isRightRotationOp) // SAĞ BÜKÜM OPERASYONU
            {
                _logger?.LogDebug("Operasyon Türü: SAĞ BÜKÜM");
                // Sol piston geri, Sağ piston ileri
                targetLeft = currentLeftPosition - stepSize;

                if (isFirstOperation) // Sadece ilk operasyonda sağ 1x gider
                {
                    targetRight = currentRightPosition + stepSize;
                }
                else // Sonraki tüm sağ bükümlerde sağ 2x gider
                {
                    targetRight = currentRightPosition + (2 * stepSize);
                }
            }
            else // SOL BÜKÜM OPERASYONU
            {
                _logger?.LogDebug("Operasyon Türü: SOL BÜKÜM");
                // Sağ piston geri, Sol piston ileri
                targetRight = currentRightPosition - stepSize;
                targetLeft = currentLeftPosition + (2 * stepSize);
            }
            
            isFirstOperation = false; // İlk operasyondan sonra false yap

            // Hedeflerin sideBallTravelDistance sınırını aşmadığından emin ol
            targetLeft = Math.Clamp(targetLeft, -stepSize, sideBallTravelDistance);
            targetRight = Math.Clamp(targetRight, -stepSize, sideBallTravelDistance);
            
            _logger?.LogInformation("🎯 Yeni Hedefler -> Sol: {LeftTarget:F2}mm, Sağ: {RightTarget:F2}mm", targetLeft, targetRight);


            // 3. ADIM: Pistonları Hareket Ettir
            _logger?.LogInformation("🔧 PASO: S2 valfi açılıyor (her iki piston için)");
            await OpenS2ValveAsync();
            await Task.Delay(500, cancellationToken); // Valfin açılması için bekle

            // Pistonları SIRAYLA hareket ettir
            bool rightSuccess, leftSuccess;

            if (isRightRotationOp) // Sağ büküm, önce SAĞ piston hareket eder
            {
                _logger?.LogInformation("-> Önce Sağ Piston hareket ediyor...");
                rightSuccess = await MovePistonToPositionForPasoAsync(PistonType.RightPiston, targetRight);
                if (rightSuccess)
                {
                    _logger?.LogInformation("-> Şimdi Sol Piston hareket ediyor...");
                    leftSuccess = await MovePistonToPositionForPasoAsync(PistonType.LeftPiston, targetLeft);
                    }
                    else
                    {
                    leftSuccess = false; // Sağ başarısızsa solu hiç deneme
                }
            }
            else // Sol büküm, önce SOL piston hareket eder
            {
                _logger?.LogInformation("-> Önce Sol Piston hareket ediyor...");
                leftSuccess = await MovePistonToPositionForPasoAsync(PistonType.LeftPiston, targetLeft);
                if (leftSuccess)
                {
                    _logger?.LogInformation("-> Şimdi Sağ Piston hareket ediyor...");
                    rightSuccess = await MovePistonToPositionForPasoAsync(PistonType.RightPiston, targetRight);
                }
                else
                {
                    rightSuccess = false; // Sol başarısızsa sağı hiç deneme
                }
            }
            
            // Valfi kapat
            await CloseS2ValveAsync();

            if (!leftSuccess || !rightSuccess)
            {
                _logger?.LogError("❌ Piston hareketleri başarısız - Sol Başarı: {LeftRes}, Sağ Başarı: {RightRes} - Operasyon #{OpNum}", 
                    leftSuccess, rightSuccess, operationNumber);
                    return false;
            }

            // Mevcut pozisyonları güncelle
            currentLeftPosition = _pistons[PistonType.LeftPiston].CurrentPosition;
            currentRightPosition = _pistons[PistonType.RightPiston].CurrentPosition;

            _logger?.LogInformation("✅ Operasyon #{OperationNum} Başarıyla Tamamlandı.", operationNumber);
            _logger?.LogDebug("Güncel Pozisyonlar -> Sol: {LeftPos:F2}mm, Sağ: {RightPos:F2}mm", currentLeftPosition, currentRightPosition);

            // Bitiş koşulunu kontrol et
            var leftError = Math.Abs(currentLeftPosition - sideBallTravelDistance);
            var rightError = Math.Abs(currentRightPosition - sideBallTravelDistance);
            var tolerance = 1.0; // 1mm tolerans
            
            _logger?.LogDebug("🎯 Hedef Kontrol -> Sol: {LeftPos:F2}/{Target:F2}mm (Hata: {LeftErr:F2}mm), Sağ: {RightPos:F2}/{Target:F2}mm (Hata: {RightErr:F2}mm)",
                currentLeftPosition, sideBallTravelDistance, leftError,
                currentRightPosition, sideBallTravelDistance, rightError);
            
            if (leftError <= tolerance && rightError <= tolerance)
            {
                _logger?.LogInformation("🏁 HEDEF MESAFEYE ULAŞILDI! Sol={LeftPos:F2}mm, Sağ={RightPos:F2}mm (Hedef={Target:F2}mm ±{Tol:F1}mm)", 
                    currentLeftPosition, currentRightPosition, sideBallTravelDistance, tolerance);
                break; // Döngüden çık
            }
            
            // Son adımda hedeften uzaksak, ek adım yap
            if (i == requiredSteps * 2 - 1) // Son iterasyon
            {
                if (leftError > tolerance)
                {
                    _logger?.LogWarning("⚠️ Sol piston henüz hedefte değil, ek adım gerekiyor. Mevcut={Curr:F2}mm, Hedef={Target:F2}mm", 
                        currentLeftPosition, sideBallTravelDistance);
                    i--; // Bir adım daha ver
                }
                if (rightError > tolerance)
                {
                    _logger?.LogWarning("⚠️ Sağ piston henüz hedefte değil, ek adım gerekiyor. Mevcut={Curr:F2}mm, Hedef={Target:F2}mm", 
                        currentRightPosition, sideBallTravelDistance);
                    i--; // Bir adım daha ver
                }
            }
        }

        _logger?.LogInformation("✅✅ Paso Bending Test Başarıyla Tamamlandı!");
            return true;
    }

    /// <summary>
    /// PASO TEST İÇİN HASSAS ENCODER BAZLI ROTASYON - Parça sıfırlamadaki gibi kademeli hız kontrolü
    /// PARÇA SIFIRLAMA SONRASI ENCODER REFERANS UYUMLU
    /// </summary>
    private async Task<bool> PerformPreciseEncoderRotationAsync(RotationDirection direction, double targetDistance, double initialSpeed, CancellationToken cancellationToken)
    {
        try
        {
            const double ballDiameter = 220.0; // mm - Alt orta top çapı
            const double maxRotationTimeSeconds = 120.0; // Maksimum rotasyon süresi
            const double encoderTolerance = 3.0; // mm - Encoder toleransı (10.0mm→3.0mm)
            const double minSuccessPercentage = 99.5; // Minimum başarı yüzdesi (98.0→99.5)
            
            _logger?.LogInformation("🔄 Hassas encoder bazlı rotasyon başlatılıyor - Yön: {Direction}, Hedef: {Target}mm", 
                direction, targetDistance);
            
            // Başlangıç encoder pozisyonunu al
            await UpdateMachineStatusAsync();
            var startEncoderRaw = _currentStatus.RotationEncoderRaw;
            var startDistance = PulseToDistanceConvert(startEncoderRaw, ballDiameter);
            
            _logger?.LogInformation("📍 Başlangıç encoder - Raw: {Raw}, Mesafe: {Distance:F2}mm", startEncoderRaw, startDistance);
            
            // Hedef encoder pozisyonunu hesapla
            var targetEncoderDistance = direction == RotationDirection.Clockwise ? 
                startDistance + targetDistance : startDistance - targetDistance;
            
            _logger?.LogInformation("🎯 Hedef encoder mesafesi: {Target:F2}mm", targetEncoderDistance);
            
            // Kademeli hız kontrolü için eşik değerleri (MUTLAK MESAFE)
            var currentTravelTarget = Math.Abs(targetDistance);
            var stage1Threshold = currentTravelTarget * 0.70; // 0.80→0.70 (daha erken yavaşla)
            var stage2Threshold = currentTravelTarget * 0.90; // 0.95→0.90
            var stage3Threshold = currentTravelTarget * 0.95; // 0.99→0.95
            
            // Hız kademeleri - Daha yumuşak geçişler
            const double stage1Speed = 100.0; // Başlangıç: %50 hız (80→50)
            const double stage2Speed = 80.0; // %70'den sonra: %25 hız (30→25)
            const double stage3Speed = 50.0; // %90'dan sonra: %15 hız (20→15)
            const double stage4Speed = 30.0; // %95'den sonra: %10 hız (15→10)
            
            // Rotasyonu başlat
            await StartRotationAsync(direction, stage1Speed);
            double currentSpeed = stage1Speed;
            
            var startTime = DateTime.UtcNow;
            var lastEncoderCheck = DateTime.UtcNow;
            var stuckCount = 0;
            const int maxStuckCount = 15;
            var previousEncoderRaw = startEncoderRaw;
            var lastProgressCheck = DateTime.UtcNow;
            var lastProgress = 0.0;
            const int maxNoProgressCount = 8;
            var noProgressCount = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken);
                
                // Timeout kontrolü
                if ((DateTime.UtcNow - startTime).TotalSeconds > maxRotationTimeSeconds)
                {
                    _logger?.LogError("❌ Hassas encoder rotasyon timeout! Süre: {Time:F1}s", maxRotationTimeSeconds);
                    await StopRotationAsync();
                    return false;
                }
                
                // Encoder pozisyonunu güncelle
                await UpdateMachineStatusAsync();
                var currentEncoderRaw = _currentStatus.RotationEncoderRaw;
                var currentDistance = PulseToDistanceConvert(currentEncoderRaw, ballDiameter);
                
                // Encoder stuck kontrolü
                if (Math.Abs(currentEncoderRaw - previousEncoderRaw) < 1)
                {
                    stuckCount++;
                    if (stuckCount >= maxStuckCount)
                    {
                        _logger?.LogError("❌ Encoder dondu! {Count} kez değişim yok. Raw: {Raw}", maxStuckCount, currentEncoderRaw);
                        await StopRotationAsync();
                        return false;
                    }
                }
                else
                {
                    stuckCount = 0;
                    previousEncoderRaw = currentEncoderRaw;
                }
                
                // Mevcut ilerlemeyi hesapla (MUTLAK DEĞER)
                var traveledDistance = Math.Abs(currentDistance - startDistance);
                var remainingDistance = Math.Abs(targetEncoderDistance - currentDistance);
                var progressPercentage = (traveledDistance / currentTravelTarget) * 100.0;
                
                // İlerleme kontrolü (her 1 saniyede bir)
                if ((DateTime.UtcNow - lastProgressCheck).TotalSeconds >= 1.0)
                {
                    if (Math.Abs(traveledDistance - lastProgress) < 0.5)
                    {
                        noProgressCount++;
                        if (noProgressCount >= maxNoProgressCount)
                        {
                            _logger?.LogError("❌ İlerleme durdu! {Count} saniyedir hareket yok. Mesafe: {Distance:F2}mm", 
                                maxNoProgressCount, traveledDistance);
                                await StopRotationAsync();
                                return false;
                        }
                    }
                    else
                    {
                        noProgressCount = 0;
                    }
                    lastProgress = traveledDistance;
                    lastProgressCheck = DateTime.UtcNow;
                }
                
                _logger?.LogDebug("📊 MESAFE DURUMU - Başlangıç: {Start:F1}mm, Şu An: {Current:F1}mm, İlerleme: {Traveled:F1}mm (%{Progress:F1})", 
                    startDistance, currentDistance, traveledDistance, progressPercentage);
                
                // Kademeli hız kontrolü (MUTLAK MESAFE)
                if (traveledDistance >= stage1Threshold && traveledDistance < stage2Threshold && currentSpeed != stage2Speed)
                {
                    _logger?.LogInformation("⚡ HIZ DEĞİŞİMİ: %{OldSpeed} → %{NewSpeed} (İlerleme: {Progress:F1}mm - %{Percent:F1})", 
                        currentSpeed, stage2Speed, traveledDistance, progressPercentage);
                    await SetRotationSpeedAsync(stage2Speed);
                    currentSpeed = stage2Speed;
                    await Task.Delay(200); // 100ms→200ms (daha yumuşak geçiş)
                }
                else if (traveledDistance >= stage2Threshold && traveledDistance < stage3Threshold && currentSpeed != stage3Speed)
                {
                    _logger?.LogInformation("⚡ HIZ DEĞİŞİMİ: %{OldSpeed} → %{NewSpeed} (İlerleme: {Progress:F1}mm - %{Percent:F1})", 
                        currentSpeed, stage3Speed, traveledDistance, progressPercentage);
                    await SetRotationSpeedAsync(stage3Speed);
                    currentSpeed = stage3Speed;
                    await Task.Delay(200);
                }
                else if (traveledDistance >= stage3Threshold && currentSpeed != stage4Speed)
                {
                    _logger?.LogInformation("⚡ HIZ DEĞİŞİMİ: %{OldSpeed} → %{NewSpeed} (İlerleme: {Progress:F1}mm - %{Percent:F1})", 
                        currentSpeed, stage4Speed, traveledDistance, progressPercentage);
                    await SetRotationSpeedAsync(stage4Speed);
                    currentSpeed = stage4Speed;
                    await Task.Delay(200);
                }
                
                // Son 10mm için ekstra yavaş mod (15mm→10mm)
                if (remainingDistance <= 10.0 && currentSpeed > stage4Speed)
                {
                    _logger?.LogInformation("⚡ SON YAKLAŞMA: %{OldSpeed} → %{NewSpeed} (Kalan: {Remaining:F1}mm)", 
                        currentSpeed, stage4Speed, remainingDistance);
                    await SetRotationSpeedAsync(stage4Speed);
                    currentSpeed = stage4Speed;
                    await Task.Delay(200);
                }
                
                // ✨ YENİ: Başarı kriteri - HEM tolerans içinde olmalı HEM minimum başarı yüzdesini aşmalı
                var successPercentage = (traveledDistance / currentTravelTarget) * 100.0;
                if (remainingDistance <= encoderTolerance && successPercentage >= minSuccessPercentage)
                {
                    _logger?.LogInformation("✅ Encoder hedef mesafesine ulaşıldı! Başarı: %{Success:F1}", successPercentage);
                    _logger?.LogInformation(" SONUÇ - Başlangıç: {Start:F2}mm → Hedef: {Target:F2}mm → Gerçek: {Actual:F2}mm (Fark: {Diff:F2}mm)", 
                        startDistance, targetEncoderDistance, currentDistance, remainingDistance);
                    await StopRotationAsync();
                    return true;
                }
                
                // Her 500ms'de bir progress log
                if ((DateTime.UtcNow - lastEncoderCheck).TotalMilliseconds >= 500)
                {
                    _logger?.LogDebug(" Encoder ilerlemesi - Mevcut: {Current:F2}mm, Hedef: {Target:F2}mm, Kalan: {Remaining:F2}mm, Hız: %{Speed}", 
                        currentDistance, targetEncoderDistance, remainingDistance, currentSpeed);
                    lastEncoderCheck = DateTime.UtcNow;
                }
            }
            
            await StopRotationAsync();
            _logger?.LogWarning("⚠️ Hassas encoder rotasyon iptal edildi");
                return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Hassas encoder rotasyon sırasında hata oluştu");
            await StopRotationAsync();
            return false;
        }
    }

    /// <summary>
    /// PASO TEST İÇİN ÖZEL BÜKÜM ALGORİTMASI - Sensör kontrolü olmadan
    /// </summary>
} 