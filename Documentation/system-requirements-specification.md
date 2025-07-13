# CORVENTA 4R CPB CNC MIDI - SİSTEM GEREKSİNİMLER SPESİFİKASYONU

## 1. SİSTEM GENEL BAKIŞ

### 1.1 Proje Tanımı
**Proje Adı:** CORVENTA 4R CPB CNC MIDI Control System  
**Sistem Türü:** Industrial CNC Bending Machine Control Software  
**Mimari:** Clean Architecture + Driver Pattern + Multithreading  
**Platform:** .NET 8 / Razor Pages / PostgreSQL / SignalR  

### 1.2 Makine Teknik Özellikleri

#### A) Fiziksel Spesifikasyonlar
```
Boyutlar: 2100mm x 2300mm x 2150mm
Ağırlık: 3000 Kg
Motor Gücü: 18.5 kW (Ana Motor) + 22.5 kW (Toplam Kurulu Güç)
Vals Çapları: Ø 280mm (4 adet hareketli vals)
Mil Çapları: Ø 80mm
Çalışma Hızı: 0.5 - 7.5 m/dk (değişken hız)
Güç Gereksinimi: 380V 50Hz, 63-80A sigorta
```

#### B) Hidrolik Sistem
```
Ana Basınç: 0-200 bar (ayarlanabilir)
S1 Basınç Valfi: Ana sistem kontrolü
S2 Basınç Valfi: Yardımcı sistem kontrolü
Hidrolik Yağ: ISO VG 32/46/68 (sıcaklığa göre)
Tank Kapasitesi: 150L
Filtrasyon: 10 mikron hassasiyet
```

#### C) Piston Sistemi Detayları
```
4 ANA PİSTON:

ÜST PİSTON (TopPiston):
- Kurs Mesafesi: 161mm
- Kontrol: Analog voltaj (-10V/+10V)
- Register Count: 7973
- Pozisyon Sensörü: 4-20mA Linear Potansiyometre
- Maksimum Basınç: 200 bar

ALT ORTA PİSTON (BottomPiston):
- Kurs Mesafesi: [Belirtilecek]mm
- Register Count: 9742 
- Kontrol: Analog voltaj (-10V/+10V)
- Referans Pozisyon: Stage ayarı sırasında belirlenir

SOL ANA PİSTON (LeftPiston):
- Kurs Mesafesi: 422.3mm
- Register Count: 21082
- Büküm İşlevi: Ana büküm pistonlarından biri
- Pozisyon Toleransı: ±1.0mm

SAĞ ANA PİSTON (RightPiston):
- Kurs Mesafesi: 422.3mm  
- Register Count: 21123
- Büküm İşlevi: Ana büküm pistonlarından biri
- Paralel Hareket: Sol piston ile senkronize

6 YAN DAYAMA PİSTONU (SAĞ VE SOL OLMAK ÜZERE 3'ER ADET):

SOL GRUP:
- LeftReel: 352mm kurs, Register: 400-4021
- LeftBody: 129mm kurs, Register: 698-2806
- LeftJoin: 187mm kurs, Register: 365-3425

SAĞ GRUP:
- RightReel: 352mm kurs, Register: 400-4021  
- RightBody: 129mm kurs, Register: 698-2806
- RightJoin: 187mm kurs, Register: 365-3425

- Kontrol: Digital coil (İleri/Geri)
- Toplam: 6 adet yan dayama pistonu
```

#### D) Rotasyon Sistemi
```
Encoder: 1024 pulse/devir
Top Çapı: 220mm (encoder-mesafe dönüşümü için)
Yön Kontrolü: 
  - CW (Saat Yönü): Coil 0x1002
  - CCW (Ters Saat): Coil 0x1003
Hız Kontrolü: 1V-10V analog (0%-100%)
Pozisyon Sensörleri: Sol/Sağ magnetic sensör
```

#### E) Güvenlik Sistemi
```
Acil Durdurma: Donanım seviyesi
Termal Koruma: Hidrolik + Fan motoru
Faz Sırası Kontrolü: Otomatik tespit
Basınç Güvenlik Valfi: 210 bar
Parça Varlık Sensörleri: Sol/Sağ kızılötesi
Kirlilik Sensörleri: 3 adet (Sensor1/2/3)
```

## 2. İŞLEVSEL GEREKSİNİMLER

### 2.1 Manuel Kontrol Sistemi

#### A) Piston Kontrolü
```
GEREKSINIMLER:
- Her piston için bağımsız jog kontrolü
- Pozisyon hedefleme (±1mm hassasiyet)
- Hız kontrolü (%1-%100 arası)
- Gerçek zamanlı pozisyon gösterimi
- Limit switch kontrolü
- Acil durdurma entegrasyonu

KULLANICI ARAYÜZÜ:
- Virtual joystick kontrolü
- Pozisyon göstergesi (progress bar)
- Hız ayarlama slider'ı
- Limit durumu LED göstergeleri
- Koordinat sistemi gösterimi
```

#### B) Rotasyon Kontrolü  
```
GEREKSINIMLER:
- Manuel CW/CCW rotasyon
- Hız kontrolü (0.5-7.5 m/dk)
- Encoder pozisyon takibi
- Hassas pozisyonlama (±1 pulse)
- Otomatik durdurma (mesafe bazlı)

KULLANICI ARAYÜZÜ:
- Rotasyon yönü butonları
- Hız kontrolü slider
- Encoder pozisyon göstergesi
- Mesafe bazlı hareket girişi
- Rotasyon geçmişi log'u
```

#### C) Valf ve Motor Kontrolü
```
GEREKSINIMLER:
- S1/S2 ana valf kontrolü
- P1/P2 pnömatik valf kontrolü  
- Hidrolik motor start/stop
- Fan motor kontrolü
- Basınç izleme ve kontrol

KULLANICI ARAYÜZÜ:
- Valf durumu LED'leri
- Basınç göstergeleri (gauge)
- Motor durum indikatorleri
- Güvenlik uyarı paneli
```

### 2.2 Veri İzleme Sistemi

#### A) Gerçek Zamanlı Monitoring
```
VERİ KAYNAKLARI:
- Piston pozisyonları (4 ana + 6 yan dayama)
- Hidrolik basınç (S1/S2)
- Encoder pozisyonu
- Motor durumları
- Güvenlik sensörleri
- Sıcaklık/nem verileri

GÜNCELLEME SIKLIĞI:
- Kritik veriler: 50ms
- Standart veriler: 100ms  
- Log verileri: 1 saniye
- Trend verileri: 5 saniye

GÖRSELLEŞTIRME:
- Anlık veri göstergeleri (gauge, LED, progress bar)
- Tabular veri görünümü
- Alarm/uyarı paneli
- Basit trend göstergeleri
```

#### B) Veri Saklama ve Analiz
```
YEREL VERİTABANI (SQLite/PostgreSQL):
- Makine durumu snapshots (her 100ms)
- İşlem geçmişi (büküm operasyonları)
- Alarm/hata logları
- Bakım kayıtları
- Kullanıcı aktiviteleri

BULUT SENKRONIZASYONU:
- Haftalık toplu veri transferi
- Kritik alarm anında push
- AI öğrenme verisi paylaşımı
- Global benchmark karşılaştırması
```

### 2.3 Otomatik Büküm Sistemi

#### A) Hazırlık Süreçleri

##### Parça Sıkıştırma
```
SÜREÇ ADIMI:
1. Güvenlik kontrolü (sensörler, valfler)
2. Hidrolik motor hazırlık (3s bekleme)
3. S1/S2 valflerini aç
4. Üst pistonu parçaya kadar indirme
5. Hedef basınca ulaşana kadar sıkıştırma
6. Basınç stabilizasyonu kontrolü (500ms)
7. Başarı/başarısızlık onayı

PARAMETRELER:
- Hedef Basınç: 10-200 bar (kullanıcı tanımlı)
- Basınç Toleransı: ±5 bar (varsayılan)
- Maksimum Süre: 30 saniye
- Minimum Hareket: 5mm (sahte basınç önleme)
- Kalkış Bypass: 600ms (başlangıç basınç anomalisi)
```

##### Parça Sıfırlama
```
SÜREÇ ADIMI:
1. Sol/sağ sensör durumu kontrolü
2. Rotasyon hazırlık (S1/S2 valfler açık)
3. Sensör tarafına doğru rotasyon başlat
4. Encoder takip sistemi aktif
5. Sensör tetiklendiğinde rotasyonu durdur
6. Fine-tuning pozisyonlama (±2 pulse)
7. Encoder referans noktası kaydetme

PARAMETRELER:
- Reset Mesafesi: 500-2000mm (kullanıcı tanımlı)
- Rotasyon Hızı: %70→%40→%15 (kademeli)
- Encoder Toleransı: ±2 pulse
- Timeout Süresi: 60 saniye
- Maksimum Stuck Count: 6
```

##### Stage Ayarlama
```
STAGE TÜRLERİ:
- Stage 0: Gönye pozisyonu (10.5, 3.75, 0.0)mm
- Stage 60: 60mm yükseklik
- Stage 120: 120mm yükseklik

SÜREÇ ADIMI:
1. Mevcut pozisyonları oku
2. Hedef pozisyonları hesapla
3. Tüm pistonları aynı anda hareket ettir
4. Pozisyon toleranslarını kontrol et
5. Reference position'ları kaydet
6. Cetvel sıfırlama onayı

TOLERANSLAR:
- Ana Pistonlar: ±0.8mm
- Yan Dayama: ±1.0mm
- Timeout: 30 saniye
```

##### Cetvel Sıfırlama
```
SÜREÇ ADIMI:
1. Güvenlik kontrolü
2. Tüm sistemleri aynı anda geri çek:
   - Ana pistonlar: +10V
   - Yan dayama: Backward coil
   - Pnömatik valfler: Close
3. S1 VE S2 basınç hedefine ulaşma kontrolü
4. Gönye pozisyonlarına hareket
5. Modbus reset protokolü (4 adres: 2570)
6. Doğrulama (tüm adresler 2570 mu?)

RESET ADRESLERİ:
- M13-M16 Grubu: 0x080D-0x0810
- M17-M20 Grubu: 0x0811-0x0814  
- Pnömatik Valf: 0x0815
- Rotasyon: 0x0816
```

#### B) Paso Büküm Süreci

##### Algoritma Özellikleri
```
PASO YAKLAŞIMI:
- Step-by-step kontrollü büküm
- Her paso'da encoder ile mesafe kontrolü
- Piston geri hareketi ile vals topu boşluk açma
- Rotasyon ile parça ilerletme
- Sıralı piston tekrarı

BÜKÜM PATTERN'İ:
1. Sağa rotasyon (profil uzunluğu kadar)
2. Sağ piston ilerlemesi (stepSize mm)
3. Sağ piston geri hareketi (stepSize mm)
4. Sola rotasyon (profil uzunluğuna geri dön)
5. Sol piston ilerlemesi (stepSize mm)
6. Sol piston geri hareketi (stepSize mm)
7. Tekrar döngü...
```

##### Hassas Encoder Kontrolü
```
ENCODER BAZLI ROTASYON:
- Top çapı: 220mm
- Dönüşüm formülü: mesafe = (pulse * π * 220) / 1024
- Hassasiyet: ±1 pulse = ±0.67mm
- Hız kontrolü: Kademeli (Hızlı→Orta→Hassas)
- Stuck detection: 5 ardışık pulse değişimsizlik
- Timeout: 60 saniye maksimum
```

##### Paso Test Sistemi
```
BAĞIMSIZ TEST:
- Paso algoritmasını hazırlık olmadan test etme
- Encoder referans noktası yönetimi
- Paralel valf açma (S1+S2 manuel)
- Piston valf bypass sistemi
- Real-time paso durumu bildirimi

PARAMETRELER:
- Step Size: 5-50mm arası
- Profile Length: Encoder mesafe hesabı
- Evacuation Time: 0-300 saniye
- Tolerans: ±15mm (vals topu oynaması için)
```

### 2.4 Cloud Entegrasyon Sistemi

#### A) Büküm Örnek Veritabanı
```
BULUT ARKİTEKTÜRÜ:
- RESTful API (Basit sorgu sistemi)
- Veritabanı (Büküm örnekleri)
- Filtreleme sistemi
- Kategori yönetimi
- Güncelleme servisleri

VERİ SENKRONİZASYONU:
- Haftalık örnek güncelleme
- Kategori senkronizasyonu
- Yeni büküm pattern'leri
- Başarılı operasyon kayıtları
```

#### B) Büküm Örnek Sistemi
```
ÖRNEK ARAMA SİSTEMİ:
1. Profil tipi seçimi
2. Malzeme özelliklerini filtreleme
3. Büküm açısı aralığı
4. Benzer örnekleri getirme
5. Parametre önerisi
6. Başarı oranı gösterimi

VERİ TÜRLERİ:
- Profil spesifikasyonları
- Büküm parametreleri
- Başarı oranları
- Kalite değerlendirmeleri
- Operasyon süreleri
```

#### C) Örnek Veritabanı
```
FİLTRELEME KRİTERLERİ:
- Profil tipi (L, U, C, Z profil)
- Malzeme (Çelik, Alüminyum, vb.)
- Kalınlık aralığı
- Büküm açısı
- Vals topu tipi

VERİ YAPISI:
- Kategorize edilmiş örnekler
- Parametre setleri
- Başarı metrikleri
- Kullanım sıklığı
- Güncelleme tarihleri
```

### 2.5 Multithreading Mimarisi

#### A) Thread Yönetimi
```
THREAD YAPISI:
1. MAIN UI THREAD: Kullanıcı arayüzü
2. MODBUS COMMUNICATION THREAD: Makine haberleşmesi
3. SIGNALR THREAD: Real-time veri akışı
4. DATA PROCESSING THREAD: Veri işleme
5. LOGGING THREAD: Log yazma işlemleri
6. BACKGROUND SERVICES: Periyodik görevler

THREAD GÜVENLİĞİ:
- Thread-safe collections
- Locking mechanisms
- Async/await patterns
- CancellationToken kullanımı
- Deadlock prevention
```

#### B) Real-time Veri Akışı
```
SIGNALR THREAD ÖZELLİKLERİ:
- 100ms aralıklarla veri gönderimi
- Bağımsız çalışma (UI bloklamaz)
- Otomatik reconnection
- Error handling
- Connection pool yönetimi

VERİ AKIŞ KONTROLÜ:
- Bandwidth yönetimi
- Data compression
- Selective updates
- Client-side caching
- Connection monitoring
```

### 2.6 Ayarlar Yönetim Sistemi

#### A) Makine Parametreleri
```
KATEGORİLER:
1. PİSTON AYARLARI:
   - Kurs mesafeleri (4 ana piston)
   - Hız limitleri  
   - Pozisyon toleransları
   - Register count değerleri
   - Yan dayama piston ayarları (6 adet)

2. HİDROLİK SİSTEM:
   - Maksimum basınç limitleri
   - Güvenlik valve ayarları
   - Yağ değişim periyotları
   - Filtre değişim uyarıları

3. ROTASYON SİSTEMİ:
   - Encoder kalibrasyonu
   - Hız profilleri
   - Pozisyon limitleri
   - Sensör hassasiyeti

4. BÜKÜM ALGORİTMALARI:
   - Step size varsayılanları
   - Tolerans değerleri
   - Timeout süreleri
   - Retry sayıları

5. SYSTEM PARAMETRELERI:
   - Modbus bağlantı ayarları
   - SignalR güncelleme sıklığı
   - Thread pool boyutları
   - Logging seviyeleri
```

#### B) Vals Topu Yönetimi
```
VALS TOPU KONFİGÜRASYONU:
- Özel vals kodları (user input)
- Çap ve profil bilgileri
- Büküm kapasiteleri
- Material compatibility
- Otomatik seçim algoritması

VERİTABANI YAPISI:
- Vals topu kütüphanesi
- Profil-vals mapping
- Capacity calculations
- Historical usage data
```

#### C) Kullanıcı Yönetimi
```
ROLLER:
1. OPERATOR: Temel operasyonlar
2. TECHNICIAN: Bakım + ayarlar
3. SUPERVISOR: İleri ayarlar
4. ADMIN: Sistem yönetimi
5. SERVICE: Uzaktan destek

YETKİLER:
- Endpoint bazlı erişim kontrolü
- Feature flag sistemi
- Audit logging (kim ne değiştirdi)
- Session management
```

### 2.7 Servis ve Bakım Sistemi

#### A) Dinamik Bakım Planlama
```
BAKIM TÜRLERİ:
1. GÜNLÜK: Görsel kontrol, ses analizi
2. HAFTALIK: Temizlik, genel kontrol
3. AYLIK: Yağlama, filtre kontrolü
4. 6 AYLIK: Rulman kontrolü
5. YILLIK: Major overhaul

DİNAMİK HESAPLAMA:
- Kullanım saati bazlı
- İş yoğunluğu faktörü
- Çevre koşulları (sıcaklık, nem)
- Hata geçmişi analizi
```

#### B) Predictive Maintenance
```
SENSÖR VERİLERİ:
- Titreşim analizi (rulman durumu)
- Sıcaklık trendi (motor sağlığı)
- Akım tüketimi (motor verimlilik)
- Basınç değişimleri (sistem sızdırması)
- Yağ kalitesi (kirlilik sensörleri)

AI TAHMIN MODELİ:
- Component failure probability
- Remaining useful life (RUL)
- Maintenance window optimization
- Parts inventory management
```

#### C) Remote Support
```
UZAKTAN DESTEK SİSTEMİ:
- VPN tunnel (secure)
- Screen sharing (TeamViewer like)
- Log file automatic upload
- Configuration backup/restore
- Live diagnostic data stream

GÜVENLİK:
- Müşteri onayı gerekli
- Session recording
- Limited access (read-only default)
- Audit trail
```

## 3. TEKNIK MİMARİ GEREKSİNİMLER

### 3.1 Software Architecture

#### A) Clean Architecture Layers
```
1. PRESENTATION LAYER:
   - Razor Pages UI (.NET 8)
   - API Controllers (.NET 8)
   - SignalR Hubs (Real-time)
   - Static Files (CSS, JS)

2. APPLICATION LAYER:
   - Business Logic Services
   - DTO Mappings (AutoMapper)
   - Validation (FluentValidation)
   - Background Services

3. DOMAIN LAYER:
   - Entities & Value Objects
   - Domain Services
   - Business Rules
   - Interfaces

4. INFRASTRUCTURE LAYER:
   - Database (PostgreSQL)
   - Modbus Communication
   - Cloud Services (Basit API)
   - File System Operations

5. DRIVER LAYER:
   - Machine Communication Agent
   - Modbus Protocol Handler
   - Safety Controls
   - Real-time Data Collection
```

#### B) Design Patterns
```
1. DRIVER PATTERN: Machine communication agent
2. COMMAND PATTERN: Her makine operasyonu
3. FACADE PATTERN: Complex operations
4. OBSERVER PATTERN: Real-time events
5. FACTORY PATTERN: Configuration creation
6. STRATEGY PATTERN: Algorithm selection
7. STATE PATTERN: Machine state management
8. SINGLETON PATTERN: Thread-safe services
```

### 3.2 Database Design

#### A) Local Database (PostgreSQL)
```sql
-- Machine Status
CREATE TABLE machine_status (
    id SERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL,
    connection_status VARCHAR(20),
    hydraulic_pressure_s1 DECIMAL(5,2),
    hydraulic_pressure_s2 DECIMAL(5,2),
    encoder_position INTEGER,
    safety_status JSONB,
    piston_positions JSONB
);

-- Bending Operations
CREATE TABLE bending_operations (
    id UUID PRIMARY KEY,
    operation_type VARCHAR(50),
    parameters JSONB,
    start_time TIMESTAMPTZ,
    end_time TIMESTAMPTZ,
    status VARCHAR(20),
    result JSONB,
    error_message TEXT
);

-- Maintenance Records
CREATE TABLE maintenance_records (
    id SERIAL PRIMARY KEY,
    maintenance_type VARCHAR(50),
    performed_by VARCHAR(100),
    performed_at TIMESTAMPTZ,
    details JSONB,
    next_maintenance_due TIMESTAMPTZ
);
```

#### B) Cloud Database (Büküm Örnekleri)
```sql
-- Büküm Örnekleri
CREATE TABLE bending_examples (
    id UUID PRIMARY KEY,
    profile_type VARCHAR(50),
    material_type VARCHAR(50),
    thickness DECIMAL(5,2),
    bending_angle DECIMAL(5,2),
    parameters JSONB,
    success_rate DECIMAL(3,2),
    usage_count INTEGER,
    created_at TIMESTAMPTZ
);

-- Kategori Yönetimi
CREATE TABLE example_categories (
    id UUID PRIMARY KEY,
    name VARCHAR(100),
    description TEXT,
    parent_id UUID,
    sort_order INTEGER,
    created_at TIMESTAMPTZ
);
```

### 3.3 API Design

#### A) RESTful API Structure
```
/api/v1/machine/
├── GET /status
├── POST /connect
├── POST /disconnect
└── GET /health

/api/v1/pistons/
├── GET /{pistonType}/position
├── POST /{pistonType}/move
├── POST /{pistonType}/jog
└── POST /{pistonType}/stop

/api/v1/bending/
├── POST /calculate
├── POST /start
├── POST /stop
├── GET /{jobId}/status
└── GET /{jobId}/result

/api/v1/maintenance/
├── GET /schedule
├── POST /record
├── GET /predictions
└── POST /alerts

/api/v1/cloud/
├── GET /examples
├── POST /search-examples
├── GET /categories
└── POST /sync-data
```

#### B) SignalR Hubs
```csharp
public class MachineStatusHub : Hub
{
    // Real-time machine status
    public async Task JoinMachineGroup()
    public async Task LeaveMachineGroup()
    
    // Events to clients
    public async Task MachineStatusUpdate(MachineStatusDto status)
    public async Task BendingProgressUpdate(BendingProgressDto progress)
    public async Task AlarmRaised(AlarmDto alarm)
    public async Task MaintenanceAlert(MaintenanceAlertDto alert)
}
```

### 3.4 Security Requirements

#### A) Authentication & Authorization
```
1. AUTHENTICATION:
   - JWT Token based
   - Multi-factor authentication
   - LDAP/Active Directory integration
   - Session management

2. AUTHORIZATION:
   - Role-based access control (RBAC)
   - Permission-based fine control
   - Feature flags
   - API rate limiting

3. DATA PROTECTION:
   - AES-256 encryption at rest
   - TLS 1.3 in transit
   - Certificate management
   - Key rotation
```

#### B) Network Security
```
1. FIREWALL RULES:
   - Whitelist approach
   - VPN for remote access
   - DMZ for web services
   - Isolated machine network

2. MONITORING:
   - Intrusion detection
   - Traffic analysis
   - Audit logging
   - Security alerts
```

## 4. NON-FUNCTIONAL REQUIREMENTS

### 4.1 Performance
```
RESPONSE TIMES:
- UI Actions: < 200ms
- API Calls: < 500ms
- Real-time Updates: < 100ms
- Database Queries: < 50ms

THROUGHPUT:
- SignalR Messages: 10/second (100ms interval)
- API Requests: 50/second
- Database Operations: 1,000/second

SCALABILITY:
- Support 5-10 concurrent users
- Handle 100K+ records in database
- Process 100MB+ daily data
- Thread-safe operations
```

### 4.2 Reliability
```
AVAILABILITY:
- System Uptime: 99.9%
- Database Availability: 99.95%
- Cloud Services: 99.9%

FAULT TOLERANCE:
- Automatic failover
- Circuit breaker pattern
- Retry mechanisms
- Graceful degradation

DATA INTEGRITY:
- ACID transactions
- Backup strategies
- Data validation
- Consistency checks
```

### 4.3 Usability
```
USER INTERFACE:
- Responsive design (desktop-first)
- Accessibility (WCAG 2.1)
- Turkish language support
- Industrial theme

USER EXPERIENCE:
- Intuitive navigation
- Contextual help
- Error messages clarity
- Loading indicators
- Real-time feedback
- Minimal graphics complexity
```

## 5. IMPLEMENTATION ROADMAP

### 5.1 Phase 1: Foundation (Weeks 1-2)
```
- Clean Architecture setup
- Database design & setup
- Modbus communication layer
- Basic API structure
- Authentication system
```

### 5.2 Phase 2: Core Features (Weeks 3-4)
```
- Manual control system
- Real-time monitoring
- Basic bending operations
- Settings management
- Local database operations
```

### 5.3 Phase 3: Advanced Features (Weeks 5-6)
```
- Automatic bending system
- Cloud connectivity (örnek veritabanı)
- Multithreading implementation
- Advanced monitoring
- Performance optimization
```

### 5.4 Phase 4: Integration & Polish (Weeks 7-8)
```
- Büküm örnek sistemi
- Settings management
- Thread optimization
- Remote support system
- Documentation
```

### 5.5 Phase 5: Final Testing & Deploy (Weeks 9-10)
```
- UI/UX improvements
- Security hardening
- Thread safety testing
- Performance tuning
- Deployment & testing
```

## 6. TESTING STRATEGY

### 6.1 Testing Pyramid
```
1. UNIT TESTS (70%):
   - Business logic
   - Domain services
   - Utilities
   - Calculations

2. INTEGRATION TESTS (20%):
   - API endpoints
   - Database operations
   - Modbus communication
   - External services

3. E2E TESTS (10%):
   - User workflows
   - Critical paths
   - UI interactions
   - Mobile app flows
```

### 6.2 Testing Tools
```
- Unit Testing: xUnit, NUnit
- Mocking: Moq, NSubstitute
- Integration: WebApplicationFactory
- Thread Testing: Concurrent collections test
- Load Testing: NBomber (SignalR focus)
- API Testing: Postman, Newman
```

## 7. ÖNEMLİ NOTLAR VE KISITLAMALAR

### 7.1 Sistem Kısıtlamaları
```
PİSTON YAPISI:
- 4 Ana Piston: TopPiston, BottomPiston, LeftPiston, RightPiston
- 6 Yan Dayama Pistonu: Sol grup (3 adet) + Sağ grup (3 adet)
- Toplam: 10 piston (4+6)

MOBİL UYGULAMA:
- Mobil uygulama geliştirilmeyecek
- Sistem sadece web tabanlı olacak
- Responsive design desktop odaklı

YAPAY ZEKA:
- Gerçek AI/ML sistemi olmayacak
- Sadece büküm örnek veritabanı sorgusu
- Filtreleme ve arama sistemi
- Parametre önerisi sistemi
```

### 7.2 Multithreading Gereksinimleri
```
ZORUNLU THREAD YAPISI:
- Real-time veri akışı kendi thread'inde
- Modbus haberleşmesi bloklamayan
- UI responsive kalmalı
- Background işlemler ayrı thread'de

THREAD GÜVENLİĞİ:
- ConcurrentQueue kullanımı
- Thread-safe collections
- Proper locking mechanisms
- Deadlock prevention
```

### 7.3 Real-time Veri Akışı
```
SİGNALR GEREKSİNİMLERİ:
- 100ms güncelleme sıklığı
- Bağımsız çalışma (UI bloklamaz)
- Otomatik reconnection
- Error handling
- Connection monitoring

VERİ TÜRLERİ:
- Piston pozisyonları (10 adet)
- Hidrolik basınç (S1/S2)
- Encoder durumu
- Motor durumları
- Güvenlik sensörleri
```

### 7.4 Görselleştirme Kısıtlamaları
```
KARMAŞIK GRAFİKLER OLMAYACAK:
- Real-time grafikler yok
- 3D görünüm yok
- Karmaşık chart'lar yok
- Sadece basit göstergeler

BASIT UI ELEMENTLERİ:
- Gauge göstergeleri
- LED indikatorleri
- Progress bar'lar
- Tabular veri görünümü
- Basit trend göstergeleri
```

## 7. DEPLOYMENT & DEVOPS

### 7.1 CI/CD Pipeline
```
1. SOURCE CONTROL: Git (Azure DevOps/GitHub)
2. BUILD: .NET 8 SDK, Node.js
3. TESTING: Automated test suites
4. PACKAGING: Docker containers
5. DEPLOYMENT: Kubernetes/Docker Swarm
6. MONITORING: Application Insights, Grafana
```

### 7.2 Infrastructure
```
PRODUCTION:
- Container orchestration (Kubernetes)
- Load balancing (Nginx/HAProxy)
- Database clustering (PostgreSQL HA)
- Monitoring stack (Prometheus/Grafana)
- Logging (ELK Stack)

CLOUD SERVICES:
- Azure/AWS Cloud Platform
- API Management
- Service Bus/Message Queues
- AI/ML Services
- Blob Storage
```

Bu dokümantasyon, CORVENTA 4R CPB CNC MIDI sistemi için kapsamlı bir sistem gereksinimleri spesifikasyonudur. Tüm teknik detaylar, business requirements ve implementation stratejisi bu dokümanda tanımlanmıştır. 