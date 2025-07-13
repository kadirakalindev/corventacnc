# Modbus Haberleşme, Veri Okuma ve Yazma Raporu

Bu doküman, Corventa Bükme Makinesi kontrol yazılımında Modbus TCP protokolü kullanılarak fiziksel cihazlarla (sensörler, motorlar, pistonlar) nasıl iletişim kurulduğunu, veri okuma, veri yazma ve okunan ham verilerin anlamlı birimlere nasıl dönüştürüldüğünü çok detaylı bir şekilde açıklamaktadır.

## 1. Temel Kavramlar ve Proje Mimarisi

### 1.1. Modbus Nedir?

Modbus, endüstriyel otomasyon sistemlerinde yaygın olarak kullanılan bir haberleşme protokolüdür. Bir "Master" (efendi - bizim yazılımımız) ve bir veya daha fazla "Slave" (köle - PLC, sensör, motor sürücüsü) cihaz arasında basit bir sorgu-cevap mekanizmasıyla çalışır. Veriler, "Register" adı verilen 4 farklı hafıza alanında tutulur:

*   **Holding Registers:** Okunabilir ve yazılabilir 16-bit'lik (0-65535 arası) analog değerler (örn: hedeflenen motor hızı).
*   **Input Registers:** Sadece okunabilir 16-bit'lik analog değerler (örn: bir sensörden gelen basınç değeri).
*   **Coils:** Okunabilir ve yazılabilir tek bit'lik (ON/OFF - True/False) dijital değerler (örn: bir motoru çalıştır/durdur).
*   **Discrete Inputs:** Sadece okunabilir tek bit'lik dijital değerler (örn: bir acil durum butonunun basılı olup olmadığı).

### 1.2. Projedeki Yeri ve Kullandığı Kütüphane

Proje, Modbus haberleşmesi için `.NET` dünyasında popüler olan **NModbus** kütüphanesini kullanır. Haberleşme mantığı, Temiz Mimari (Clean Architecture) prensiplerine uygun olarak `Modbus` adlı ayrı bir projede soyutlanmıştır.

*   **`IModbusClient.cs` (Arayüz):** Uygulamanın geri kalanının (örn: `Driver` projesi) kullandığı sözleşmedir. Bu, projenin belirli bir Modbus kütüphanesine sıkı sıkıya bağlanmasını engeller ve test edilebilirliği artırır.
*   **`NModbusClient.cs` (Uygulama):** `IModbusClient` arayüzünü uygulayan ve arka planda `NModbus` kütüphanesinin gerçek fonksiyonlarını çağıran sınıftır. Tüm TCP bağlantı yönetimi ve Modbus komutları burada bulunur.
*   **`ModbusAddresses.cs` (Adres Sabitleri):** PLC veya cihaz üreticisinin dokümanlarında belirtilen sayısal adreslere (`40101`, `00001` gibi) kod içinde anlamlı isimler (`LeftPistonPosition`, `EmergencyStopButton` gibi) verir. Bu, kodun okunabilirliğini ve bakımını muazzam ölçüde kolaylaştırır.

---

## 2. Veri Okuma Süreci (Adım Adım)

Bir sensörden veri okuma işlemi, yazılımın en üst katmanından donanıma kadar uzanan bir zincir şeklinde gerçekleşir. Örnek olarak, S1 valfinin yağ basıncını okuma senaryosunu inceleyelim.

### Adım 1: Ne Okunacak? Adresin Tanımlanması

*   **Yer:** `Core/Constants/ModbusAddresses.cs`
*   **Açıklama:** Geliştirici, PLC programcısından veya donanım dokümanından S1 valfi basınç sensörünün, örneğin, `30001` numaralı "Input Register" adresinde olduğunu öğrenir. Bu "sihirli sayı"nın kod içinde kaybolmaması için `ModbusAddresses.cs` dosyasına bir sabit olarak eklenir.
    ```csharp
    public static class ModbusAddresses
    {
        // ... diğer adresler
        public const int S1_OilPressure = 30001;
        // ... diğer adresler
    }
    ```

### Adım 2: Okuma İsteğinin Yapılması

*   **Yer:** `Driver/MachineDriver.cs` (veya benzer bir servis)
*   **Açıklama:** Makine durumunu güncelleyen bir metod (`UpdateMachineState`), `IModbusClient` arayüzü üzerinden okuma isteğini başlatır. Hangi adresi okuyacağını `ModbusAddresses` sınıfından alır.
    ```csharp
    // _modbusClient, Dependency Injection ile alınmış IModbusClient implementasyonudur.
    var s1OilPressureRaw = await _modbusClient.ReadInputRegisterAsync(ModbusAddresses.S1_OilPressure);
    ```
*   **Detay:** Burada `ReadInputRegisterAsync` fonksiyonunun çağrıldığına dikkat edin. Bu, verinin "Input Register" hafıza alanından okunacağını belirtir. Eğer bir motorun durumu okunacak olsaydı `ReadCoilAsync` çağrılabilirdi.

### Adım 3: NModbus Kütüphanesinin Devreye Girmesi

*   **Yer:** `Modbus/NModbusClient.cs`
*   **Açıklama:** `IModbusClient` arayüzü üzerinden gelen istek, `NModbusClient` sınıfı tarafından karşılanır. Bu sınıf, isteği `NModbus` kütüphanesinin anlayacağı formata çevirir ve TCP üzerinden donanıma gönderir.
    ```csharp
    public async Task<ushort> ReadInputRegisterAsync(int address)
    {
        EnsureConnected(); // Bağlantı var mı kontrol et

        // NModbus kütüphanesinin kendi fonksiyonunu çağır
        // _slaveId: Hangi cihazla konuşulacağı (genellikle 1)
        // (ushort)address: Okunacak register adresi
        // 1: Kaç adet register okunacağı (bu durumda 1 tane)
        var result = await _modbusMaster!.ReadInputRegistersAsync(_slaveId, (ushort)address, 1);

        // NModbus dizi döner, biz ilk elemanı alırız.
        return result[0];
    }
    ```
*   **Sonuç:** Bu fonksiyonun sonunda `s1OilPressureRaw` değişkeni, donanımdan gelen **ham (raw)** veriyi içerir. Bu genellikle 0 ile 65535 arasında bir `ushort` (unsigned short integer) değeridir. Örneğin, `1234`.

### Adım 4: Ham Verinin Anlamlı Birime Dönüştürülmesi

*   **Yer:** `Driver/MachineDriver.cs` veya `Core/Converters` klasöründeki bir yardımcı sınıf.
*   **Açıklama:** `1234` değeri, kullanıcı veya yazılımın geri kalanı için bir anlam ifade etmez. Bu değerin "Bar" veya "PSI" gibi bir basıç birimine dönüştürülmesi gerekir. Bu dönüştürme faktörü veya formülü, sensörün üretici dokümanlarında belirtilir.
*   **Örnek Senaryo:** Sensör dokümanında "Okunan her 100 birim, 1 Bar basınca eşittir" yazıyor olabilir.
    ```csharp
    // Önceki adımdan gelen ham değer
    ushort s1OilPressureRaw = 1234;

    // Dönüştürme işlemi
    double s1OilPressureInBar = (double)s1OilPressureRaw / 100.0; // ==> 12.34 Bar

    // Sonuç, makine durumu nesnesine atanır
    _currentState.S1_OilPressure = s1OilPressureInBar;
    ```
*   **Önemli:** Bu "bölme (`/ 100.0`)" işlemi, Modbus entegrasyonunun en kritik ve en çok hata yapılan kısmıdır. Bu bir "kalibrasyon faktörüdür" ve tamamen donanıma bağlıdır. Bazen sadece bölme değil, ofset ekleme (`(değer / 100.0) - 5.0`) gibi daha karmaşık formüller de gerekebilir.

---

## 3. Veri Yazma Süreci (Adım Adım)

Veri yazma, okuma sürecinin tersine işler: Anlamlı birim ham veriye dönüştürülür ve donanıma gönderilir. Örnek olarak, bir pistonu hareket ettiren bir valfi (`M03_LeftBodyForward`) ON (aktif) durumuna getirmeyi inceleyelim.

### Adım 1: Ne Yazılacak? Adres ve Değerin Belirlenmesi

*   **Yer:** `Driver/Commands/LeftSideCommands.cs` (veya benzer bir komut sınıfı)
*   **Açıklama:** Pistonu ileri hareket ettirme komutu, ilgili valfin adresini `ModbusAddresses` sınıfından alır ve yazılacak değeri (`true`) belirler. Valfler genellikle "Coil" olarak kontrol edilir.
    ```csharp
    public async Task MoveForwardAsync()
    {
        // Yazılacak adres ve değer
        int addressToWrite = ModbusAddresses.M03_LeftBodyForward;
        bool valueToWrite = true;

        // Yazma isteğini başlat
        await _modbusClient.WriteCoilAsync(addressToWrite, valueToWrite);
    }
    ```

### Adım 2: Birimin Ham Veriye Dönüştürülmesi (Bu Örnekte Gerekli Değil)

*   **Açıklama:** Bu örnekte, bir `bool` (True/False) değeri doğrudan Modbus "Coil"ine karşılık geldiği için bir dönüştürme gerekmez.
*   **Analog Örnek:** Eğer bir motorun hızı %50'ye ayarlanmak istenseydi ve motor sürücüsü hızı 0-10000 arasında bir değer olarak kabul etseydi, önce dönüştürme yapılırdı:
    ```csharp
    double targetSpeedPercentage = 50.0;
    ushort valueToWrite = (ushort)(targetSpeedPercentage * 100.0); // ==> 5000
    // await _modbusClient.WriteRegisterAsync(motorHizAdresi, valueToWrite);
    ```

### Adım 3: NModbus Kütüphanesi ile Yazma İşlemi

*   **Yer:** `Modbus/NModbusClient.cs`
*   **Açıklama:** Yazma isteği, `NModbusClient` tarafından karşılanır ve donanıma gönderilir.
    ```csharp
    public async Task WriteCoilAsync(int address, bool value)
    {
        EnsureConnected();

        // NModbus'un ilgili yazma fonksiyonunu çağır
        await _modbusMaster!.WriteSingleCoilAsync(_slaveId, (ushort)address, value);
    }
    ```
*   **Sonuç:** Bu komutun çalışmasıyla birlikte, PLC'nin `0x100A` adresindeki Coil'i `1` (ON) durumuna geçer ve buna bağlı olan valf açılarak pistonun ileri hareket etmesini sağlar.

Bu adımlar, yazılım katmanı ile fiziksel donanım arasındaki köprünün nasıl kurulduğunu, ham verilerin nasıl işlendiğini ve kontrol komutlarının nasıl gönderildiğini detaylı bir şekilde göstermektedir. 