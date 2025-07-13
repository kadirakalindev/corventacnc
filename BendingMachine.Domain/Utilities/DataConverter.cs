using System.Drawing;

namespace BendingMachine.Domain.Utilities;

/// <summary>
/// Makine komutları ve ölçümleri için birim dönüşümleri yapan sınıf
/// Corventa Büküm Makinesi için optimize edilmiş, güvenli ve hassas dönüşüm sistemi
/// converter.md dokümanına uygun olarak standardize edilmiştir
/// </summary>
public static class DataConverter
{
    // Endüstriyel standart sabitler
    private const double min_mA = 4.0;
    private const double max_mA = 20.0;
    private const int registerCount = 4095;  // 12-bit ADC
    private const int pulseCount = 1024;     // Encoder resolution
    
    #region Angle Conversions
    
    /// <summary>
    /// Derece değerini radian değerine dönüştürür
    /// </summary>
    /// <param name="degree">Derece değeri</param>
    /// <returns>Radian değeri</returns>
    public static double DegreeToRadianConvert(double degree)
    {
        return degree * Math.PI / 180.0;
    }
    
    /// <summary>
    /// Radian değerini derece değerine dönüştürür
    /// </summary>
    /// <param name="radian">Radian değeri</param>
    /// <returns>Derece değeri</returns>
    public static double RadianToDegreeConvert(double radian)
    {
        return radian * 180.0 / Math.PI;
    }
    
    #endregion
    
    #region Register-Voltage Dönüşümleri
    
    /// <summary>
    /// Register değerini voltaja dönüştürür
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <returns>Voltaj değeri (-10V ile 10V arası)</returns>
    public static double RegisterToVoltage(int registerValue)
    {
        const double minRegister = -2048;
        const double maxRegister = 2047;
        const double minVoltage = -10.0;
        const double maxVoltage = 10.0;
        
        // Formül: voltage = (registerValue - minRegister) / (maxRegister - minRegister) * (maxVoltage - minVoltage) + minVoltage
        double voltage = Math.Round(((registerValue - minRegister) / (maxRegister - minRegister)) * (maxVoltage - minVoltage) + minVoltage, 1);
        return voltage;
    }
    
    /// <summary>
    /// Voltaj değerini register değerine dönüştürür
    /// </summary>
    /// <param name="voltage">Voltaj değeri (-10V ile 10V arası)</param>
    /// <returns>Register değeri (-2048 ile 2047 arası)</returns>
    public static int VoltageToRegister(double voltage)
    {
        const double minRegister = -2048;
        const double maxRegister = 2047;
        const double minVoltage = -10.0;
        const double maxVoltage = 10.0;
        
        // Formül: registerValue = (voltage - minVoltage) / (maxVoltage - minVoltage) * (maxRegister - minRegister) + minRegister
        double result = Math.Round(((voltage - minVoltage) / (maxVoltage - minVoltage)) * (maxRegister - minRegister) + minRegister, 0);
        return Convert.ToInt32(Math.Clamp(result, minRegister, maxRegister));
    }
    
    #endregion
    
    #region Register-mA Dönüşümleri
    
    /// <summary>
    /// Register değerini miliamper değerine dönüştürür
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <returns>mA değeri (4-20mA arası)</returns>
    public static double RegisterToMilliamps(int registerValue)
    {
        // Formül: mA = registerValue / registerCount * (maxMilliamps - minMilliamps) + minMilliamps
        double mA = Math.Round((double)registerValue / registerCount * (max_mA - min_mA) + min_mA, 2);
        return Math.Clamp(mA, min_mA, max_mA);
    }
    
    /// <summary>
    /// mA değerini register değerine dönüştürür
    /// </summary>
    /// <param name="milliamps">mA değeri (4-20mA arası)</param>
    /// <returns>Register değeri (0-4095 arası)</returns>
    public static int MilliampsToRegister(double milliamps)
    {
        // Formül: registerValue = (milliamps - minMilliamps) / (maxMilliamps - minMilliamps) * registerCount
        var result = (int)((milliamps - min_mA) / (max_mA - min_mA) * registerCount);
        return Math.Clamp(result, 0, registerCount - 1);
    }
    
    /// <summary>
    /// Register değerini miliamper üzerinden cm/sn değerine dönüştürür (Akış sensörleri için)
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <returns>cm/sn değeri (0-297 cm/sn arası)</returns>
    public static double RegisterToMilliampsToCmPerSecond(int registerValue)
    {
        // Akış hızı dönüşümü için değerler
        const double max_flowSpeed = 297; // cm/sn
        
        // Önce mA değerine dönüştür
        double mA = RegisterToMilliamps(registerValue);
        
        // Eğim hesapla
        double slope = max_flowSpeed / (max_mA - min_mA); // 297/16 = 18.5625
        
        // Lineer dönüşüm formülü
        double flowRate = Math.Round(slope * (mA - min_mA), 1);
        return Math.Clamp(flowRate, 0, max_flowSpeed);
    }
    
    /// <summary>
    /// Register değerini miliamper üzerinden sıcaklık değerine dönüştürür (Yağ sıcaklık sensörü için)
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <returns>Sıcaklık değeri (-20°C ile 120°C arası)</returns>
    public static double RegisterToMilliampsToTemperature(int registerValue)
    {
        // Sıcaklık dönüşümü için değerler
        const double min_C = -20.0; // °C
        const double max_C = 120.0; // °C
        
        // Önce mA değerine dönüştür
        double mA = RegisterToMilliamps(registerValue);
        
        // Eğim hesapla
        double slope = (max_C - min_C) / (max_mA - min_mA); // 140/16 = 8.75
        
        // Lineer dönüşüm formülü
        double temperature = Math.Round(slope * (mA - min_mA) + min_C, 1);
        return Math.Clamp(temperature, min_C, max_C);
    }
    
    /// <summary>
    /// Register değerini miliamper üzerinden nem yüzdesine dönüştürür
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <returns>Nem değeri (% 0-100 arası)</returns>
    public static double RegisterToHumidity(int registerValue)
    {
        // Nem dönüşümü için değerler
        const double max_Moisture = 100; // %
        
        // Önce mA değerine dönüştür
        double mA = RegisterToMilliamps(registerValue);
        
        // Eğim hesapla
        double slope = max_Moisture / (max_mA - min_mA); // 100/16 = 6.25
        
        // Lineer dönüşüm formülü
        double humidity = Math.Round(slope * (mA - min_mA), 1);
        return Math.Clamp(humidity, 0, max_Moisture);
    }
    
    #endregion
    
    #region Register-Percentage Dönüşümleri
    
    /// <summary>
    /// Register değerini yüzde değerine dönüştürür
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 4095)</param>
    /// <returns>Yüzde değeri (0-100 arası)</returns>
    public static double RegisterToPercentage(int registerValue, int registerCountParam = 4095)
    {
        // Formül: percentage = (registerValue / registerCount) * 100
        double percentage = Math.Round(((double)registerValue / registerCountParam) * 100.0, 1);
        return Math.Clamp(percentage, 0.0, 100.0);
    }
    
    /// <summary>
    /// Yüzde değerini register değerine dönüştürür
    /// </summary>
    /// <param name="percentage">Yüzde değeri (0-100 arası)</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 4095)</param>
    /// <returns>Register değeri (0-registerCount arası)</returns>
    public static int PercentageToRegister(double percentage, int registerCountParam = 4095)
    {
        // Yüzdeyi sınırla
        percentage = Math.Clamp(percentage, 0.0, 100.0);
        
        // Formül: registerValue = (percentage / 100) * registerCount
        var result = (int)((percentage / 100.0) * registerCountParam);
        return Math.Clamp(result, 0, registerCountParam - 1);
    }
    
    #endregion
    
    #region Register-Millimetre Dönüşümleri
    
    /// <summary>
    /// Register değerini millimetre değerine dönüştürür (Register To mm)
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 21085)</param>
    /// <param name="strokeLength">Strok uzunluğu (varsayılan: 422mm)</param>
    /// <returns>Millimetre değeri</returns>
    public static double RegisterToMillimeter(int registerValue, int registerCountParam = 21085, double strokeLength = 422.0)
    {
        // Hesaplama ornek.txt'deki formül: mm = registerValue * strokeLength / registerCount
        double mm = registerValue * strokeLength / registerCountParam;
        return Math.Round(mm, 2);
    }
    
    /// <summary>
    /// Millimetre değerini register değerine dönüştürür (mm To Register)
    /// </summary>
    /// <param name="millimeters">Millimetre değeri</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 21085)</param>
    /// <param name="strokeLength">Strok uzunluğu (varsayılan: 422mm)</param>
    /// <returns>Register değeri</returns>
    public static int MillimeterToRegister(double millimeters, int registerCountParam = 21085, double strokeLength = 422.0)
    {
        // Hesaplama ornek.txt'deki formül: registerValue = millimeters * registerCount / strokeLength
        int register = Convert.ToInt32(Math.Round(millimeters * registerCountParam / strokeLength));
        return register;
    }
    
    /// <summary>
    /// Register değerini millimetre değerine dönüştürür (RV3100 Register To mm)
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <param name="ballDiameter">Top çapı (varsayılan: 220mm)</param>
    /// <param name="pulsCountParam">Puls sayısı (varsayılan: 1024)</param>
    /// <returns>Millimetre değeri</returns>
    public static double RV3100RegisterToMillimeter(int registerValue, double ballDiameter = 220.0, int pulsCountParam = 1024)
    {
        double perimeterDistance = ballDiameter * Math.PI; // Top çevre uzunluğu: 220 * π = 691.15mm
        double mm = registerValue * perimeterDistance / pulsCountParam;
        return Math.Round(mm, 2);
    }
    
    /// <summary>
    /// Millimetre değerini register değerine dönüştürür (RV3100 mm To Register)
    /// </summary>
    /// <param name="millimeters">Millimetre değeri</param>
    /// <param name="ballDiameter">Top çapı (varsayılan: 220mm)</param>
    /// <param name="pulsCountParam">Puls sayısı (varsayılan: 1024)</param>
    /// <returns>Register değeri</returns>
    public static int MillimeterToRV3100Register(double millimeters, double ballDiameter = 220.0, int pulsCountParam = 1024)
    {
        double perimeterDistance = ballDiameter * Math.PI; // Top çevre uzunluğu
        int register = Convert.ToInt32(Math.Round(millimeters * pulsCountParam / perimeterDistance));
        return register;
    }
    
    #endregion
    
    #region Register-Bar Dönüşümleri
    
    /// <summary>
    /// Register değerini bar ve miliamper değerine dönüştürür
    /// </summary>
    /// <param name="registerValue">Register değeri</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 4095)</param>
    /// <param name="minMilliamps">Minimum mA değeri (varsayılan: 4mA)</param>
    /// <param name="maxMilliamps">Maximum mA değeri (varsayılan: 20mA)</param>
    /// <param name="minBar">Minimum bar değeri (varsayılan: 0 bar)</param>
    /// <param name="maxBar">Maximum bar değeri (varsayılan: 250 bar)</param>
    /// <returns>Bar ve mA değerini içeren tuple</returns>
    public static (double Bar, double Milliamps) RegisterToBarAndMilliamps(int registerValue, int registerCountParam = 4095, 
        double minMilliamps = 4.0, double maxMilliamps = 20.0, double minBar = 0.0, double maxBar = 250.0)
    {
        // mA değerini hesapla
        double mA = RegisterToMilliamps(registerValue);
        
        // Basınç değerini hesapla
        double slope = (maxBar - minBar) / (maxMilliamps - minMilliamps);
        double bar = Math.Round((mA - minMilliamps) * slope + minBar, 1);
        
        return (bar, mA);
    }
    
    /// <summary>
    /// Bar değerini register değerine dönüştürür
    /// </summary>
    /// <param name="bar">Bar değeri</param>
    /// <param name="registerCountParam">Register sayısı (varsayılan: 4095)</param>
    /// <param name="minMilliamps">Minimum mA değeri (varsayılan: 4mA)</param>
    /// <param name="maxMilliamps">Maximum mA değeri (varsayılan: 20mA)</param>
    /// <param name="minBar">Minimum bar değeri (varsayılan: 0 bar)</param>
    /// <param name="maxBar">Maximum bar değeri (varsayılan: 250 bar)</param>
    /// <returns>Register değeri</returns>
    public static int BarToRegister(double bar, int registerCountParam = 4095, 
        double minMilliamps = 4.0, double maxMilliamps = 20.0, double minBar = 0.0, double maxBar = 250.0)
    {
        // Bar değerinden mA değerini hesapla
        double slope = (maxMilliamps - minMilliamps) / (maxBar - minBar);
        double mA = (bar - minBar) * slope + minMilliamps;
        
        // mA değerinden register değerini hesapla
        int register = (int)((mA - minMilliamps) * registerCountParam / (maxMilliamps - minMilliamps));
        
        return Math.Clamp(register, 0, registerCountParam - 1);
    }
    
    #endregion
    
    #region Mikron-Millimetre Dönüşümü
    
    /// <summary>
    /// Mikron değerini millimetre değerine dönüştürür
    /// </summary>
    /// <param name="microns">Mikron değeri</param>
    /// <returns>Millimetre değeri</returns>
    public static double MicronToMillimeter(double microns)
    {
        // 1 mikron = 0.001 mm
        // Tablodaki örnekte: 2.5 mikron = 0.0025 mm
        return microns * 0.001;
    }
    
    /// <summary>
    /// Millimetre değerini mikron değerine dönüştürür
    /// </summary>
    /// <param name="millimeters">Millimetre değeri</param>
    /// <returns>Mikron değeri</returns>
    public static double MillimeterToMicron(double millimeters)
    {
        // 1 mm = 1000 mikron
        return millimeters * 1000;
    }
    
    #endregion
    
    #region Sensör Sağlık Kontrolü
    
    /// <summary>
    /// Sensör okuma değerinin geçerli olup olmadığını kontrol eder
    /// </summary>
    /// <param name="value">Sensör değeri</param>
    /// <param name="sensorType">Sensör türü</param>
    /// <returns>Geçerli ise true, değilse false</returns>
    public static bool IsValidSensorReading(double value, string sensorType)
    {
        return sensorType switch
        {
            "temperature" => value >= -25 && value <= 125,
            "humidity" => value >= 0 && value <= 100,
            "flow" => value >= 0 && value <= 300,
            "pressure" => value >= 0 && value <= 260,
            _ => false
        };
    }
    
    #endregion
    
    #region Legacy Method Names (Backward Compatibility)
    
    /// <summary>
    /// RegisterToMiliAmperConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToMiliAmperConvert(int register) => RegisterToMilliamps(register);
    
    /// <summary>
    /// RegisterToFlowRateConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToFlowRateConvert(int register) => RegisterToMilliampsToCmPerSecond(register);
    
    /// <summary>
    /// RegisterToTemperatureConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToTemperatureConvert(int register) => RegisterToMilliampsToTemperature(register);
    
    /// <summary>
    /// RegisterToHumidityConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToHumidityConvert(int register) => RegisterToHumidity(register);
    
    /// <summary>
    /// RegisterToPercentageConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToPercentageConvert(int register) => RegisterToPercentage(register);
    
    /// <summary>
    /// RegisterToPressureConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToPressureConvert(int register, double minPressure = 0.0, double maxPressure = 250.0)
    {
        var (bar, _) = RegisterToBarAndMilliamps(register, 4095, min_mA, max_mA, minPressure, maxPressure);
        return bar;
    }
    
    /// <summary>
    /// PulseToDistanceConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double PulseToDistanceConvert(double register, double ballDiameter) => RV3100RegisterToMillimeter((int)register, ballDiameter);
    
    /// <summary>
    /// DistanceToPulseConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static int DistanceToPulseConvert(double mm, double ballDiameter) => MillimeterToRV3100Register(mm, ballDiameter);
    
    /// <summary>
    /// RegisterToVoltConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static double RegisterToVoltConvert(int register) => RegisterToVoltage(register);
    
    /// <summary>
    /// VoltToRegisterConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static int VoltToRegisterConvert(double volt) => VoltageToRegister(volt);
    
    /// <summary>
    /// PercentageToRegisterConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static int PercentageToRegisterConvert(double percentage) => PercentageToRegister(percentage);
    
    /// <summary>
    /// PressureToRegisterConvert metodunun alias'ı (geriye uyumluluk için)
    /// </summary>
    public static int PressureToRegisterConvert(double pressure, double minPressure = 0.0, double maxPressure = 400.0)
    {
        return BarToRegister(pressure, 4095, min_mA, max_mA, minPressure, maxPressure);
    }
    
    #endregion
    
    #region Radius Calculation (Geometry)
    
    /// <summary>
    /// Üç nokta kullanarak yarıçap hesaplar (Hesaplama ornek.txt'den)
    /// </summary>
    /// <param name="a">Birinci nokta</param>
    /// <param name="b">İkinci nokta</param>
    /// <param name="c">Üçüncü nokta</param>
    /// <returns>Yarıçap değeri</returns>
    public static double ComputeRadius(Point a, Point b, Point c)
    {
        double x1 = a.X;
        double y1 = a.Y;
        double x2 = b.X;
        double y2 = b.Y;
        double x3 = c.X;
        double y3 = c.Y;
        double mr = (double)((y2 - y1) / (x2 - x1));
        double mt = (double)((y3 - y2) / (x3 - x2));

        double xc = (double)((mr * mt * (y3 - y1) + mr * (x2 + x3) - mt * (x1 + x2)) / (2 * (mr - mt)));

        double yc = (double)((-1 / mr) * (xc - (x1 + x2) / 2) + (y1 + y2) / 2);
        double d = (xc - x1) * (xc - x1) + (yc - y1) * (yc - y1);

        return Math.Sqrt(d);
    }
    
    #endregion
} 