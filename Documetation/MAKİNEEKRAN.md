# Endüstriyel Kıvrım Makinesi Kullanıcı Arayüzü Dokümantasyonu

## 1. GENEL SİSTEM YAPISI

### Sistem Özellikleri
- **Yapay Zeka Entegrasyonu**: Makine, AI modülü ile otomatik kıvrım hesaplamaları yapabilir
- **Big Data Bağlantısı**: Bulut tabanlı veri işleme ve analiz
- **Kullanıcı Yönetimi**: Çoklu kullanıcı desteği ile iz sürme
- **Servis Modülü**: Uzaktan servis desteği ve rapor sistemi
- **Eğitim Modülü**: Entegre kullanıcı eğitim sistemi

## 2. BAŞLANGIÇ EKRANLARI

### 2.1 Açılış Süreci (Şekil 1-5)
- **Şekil 1**: Açılış logosu
- **Şekil 2**: Kullanıcı giriş ekranı (ayarlardan kullanıcı ekleme mümkün)
- **Şekil 3**: Sistem kontrolü - encoder ve cetvel sinyalleri kontrol edilir
- **Şekil 4**: Normal çalışma modu - hatalar kıvrımı engellemiyorsa ana sayfa
- **Şekil 5**: Güvenli mod - kritik hatalar varsa makine kilitleme

### 2.2 Hata Yönetimi
- Açılışta tüm sensörler kontrol edilir
- Kritik olmayan hatalar: Normal çalışmaya izin verir
- Kritik hatalar: Güvenli moda geçiş, kıvrım engellenir

## 3. İLETİŞİM VE SERVİS SİSTEMİ

### 3.1 İletişim Sayfası (Şekil 6)
- Yakın bayiler ve konumları
- Fabrika iletişim bilgileri
- Servis talep butonu

### 3.2 Servis Modülü (Şekil 51-53)
- **Servis Talep Oluştur**: Yeni servis talebinin girilmesi
- **Servis Talebi Görüntüle**: Mevcut taleplerin takibi
- **Servis Raporu**: Tamamlanan servislerin değerlendirilmesi
- **Servis Memnuniyet**: Servis sonrası geri bildirim sistemi

## 4. YAPAY ZEKA KIVRIMI SİSTEMİ

### 4.1 Profil Seçimi (Şekil 8)
- Farklı kıvrım profilleri için ayrı kombinasyonlar
- Özel profillerde manuel pozisyonlama gerekli
- Her profil için özel parametre setleri

### 4.2 Kıvrım Yönü Seçimi (Şekil 9)
- Kıvrım yönünün belirlenmesi
- Farklı yönlerde kıvrım için manuel pozisyonlama
- Alt panelde sosyal medya ve bilgi butonları
- Wi-Fi ve Big Data bağlantı durumu göstergesi

### 4.3 Kıvrım Metodu (Şekil 10-11)
- Çeşitli kıvrım yöntemleri ve ölçülerin belirlenmesi
- Detaylı bilgi ekranları (soru işaretli alanlar)
- **Vals Topu Kodlama Sistemi**:
  - Her vals topunun kendine özgü kodu
  - Dış çap, iç çap, genişlik bilgileri otomatik tanıma
  - Makine arka planda kodları eşleştirerek optimizasyon

### 4.4 Özel Kıvrım Metodları
- **Çoklu Radius (Şekil 11 LP)**: Birden fazla farklı yarıçapta kıvrım
- **Serpantin (Şekil 11 Y)**: Zigzag şeklinde kıvrım
- **Sıvama (Şekil 11 X)**: Belirli açıda düzleştirme işlemi

## 5. KIVRIMI İŞLEMİ

### 5.1 Parça Yükleme (Şekil 12)
- Makine kıvrım pozisyonuna hazırlanır
- Parça sürme uyarısı
- Temas sensörü kontrolü
- Manuel sürme seçeneği (sensör arızasında)

### 5.2 Sıkıştırma ve Hazırlık (Şekil 13)
- Yağ basıncı kontrolü
- Sıkıştırma işlemi onayı
- **Kod Sistemi**:
  - Machine Code: Makine kimlik kodu
  - AI Module Code: Yapay zeka modül kodu
  - K kodu: Kiralık sistem (yıllık big data ücreti)
  - A kodu: Asil sistem (sınırsız big data)

### 5.3 Top Kontrolü (Şekil 13H)
- Vals toplarının doğru dizilimi kontrolü
- Parça yüklemeden önce son kontrol

### 5.4 Kıvrım Süreci (Şekil 14)
- **Gerçek Zamanlı Görselleştirme**:
  - Ok işaretleri hareket yönünü gösterir
  - Aktif vals topu kırmızı renkte
  - Geri esneme hesaplama topları ölçüm modunda görünür
- **Çoklu Kıvrım**: Her kıvrım bitiminde yeni radius parametreleri

### 5.5 Güvenlik ve Acil Durumlar
- **Parça Bozulma Tespiti (Şekil 15)**: Basınç düşümünden anlık tespit
- **Acil Tahliye (Şekil 16)**: Çift el onayı sistemi
- **Acil Stop (Şekil 17)**: Alarm kodlu durdurma

## 6. KIVRIMI BİTİMİ VE TAHLİYE

### 6.1 Başarılı Kıvrım (Şekil 18)
- Kıvrım tamamlanma mesajı
- **Esnek Tahliye Sistemi**:
  - Önceden tanımlı süreler (3, 5, 10 saniye vb.)
  - Özelleştirilebilir süre (değiştir butonu)
  - Geri sayım sonrası otomatik açılma

### 6.2 İşlem Sonrası Seçenekler (Şekil 20-21)
- Kıvrımı tekrar et
- Yeni kıvrım başlat
- Kolay erişim menüsü

## 7. MANUEL KIVRIMI SİSTEMİ

### 7.1 Program Yönetimi (Şekil 22)
- Eski kıvrımları kaydetme
- Program adlandırma sistemi
- Çap değeri görüntüleme
- Kullanıcı bazlı değişiklik takibi

### 7.2 Program İşlemleri (Şekil 23-24)
- **Program Silme**: 30 günlük çöp kutusu sistemi
- **Program Düzenleme**: Adım bazında değişiklik
- Kullanıcı bazlı işlem logları

### 7.3 Manuel Kontrol (Şekil 26-30)
- **Yan Dayama Kontrol**: Otomatik/Manuel seçenekler
- **Gerçek Çap Ölçümü**: Mastar kullanmadan ölçüm
- **Geometrik Çap**: Her iki kıvrım merkezinde görüntüleme
- **Tolerans Ayarı**: Vals topu hareket hassasiyeti (0.1, 1, 10)

## 8. SERVİS MODÜLÜ

### 8.1 Ana Servis Sayfası (Şekil 31)
- Soru, öneri, şikayet sistemleri
- Kurulum ve eğitim modülleri
- Entegre servis yönetimi

### 8.2 İletişim Sistemleri (Şekil 32-43)
- **Soru Sistemi**: Sık sorulan sorular, cevap okuma
- **Öneri Sistemi**: Kullanıcı önerileri ve geri bildirimler
- **Şikayet Sistemi**: Sorun bildirimi ve takip

### 8.3 Eğitim Modülü (Şekil 44-45)
- Konu bazında eğitim notları
- Sınav sistemi (Evet/Hayır ve çoktan seçmeli)
- Yetkinlik sertifikası kazanma
- Kullanıcı bazlı ilerleme takibi

## 9. SERVİS YÖNETİMİ

### 9.1 Servis Süreci (Şekil 46-50)
- **Servis Başlat**: Tamir/değişim sürecinin başlatılması
- **Otomatik Bilgiler**: Müşteri ve makine bilgileri
- **Manuel Girişler**: Servis veren adı, yapılan işler, yedek parçalar
- **Rapor Oluşturma**: Kapsamlı servis raporu

### 9.2 Servis Tamamlama (Şekil 52-53)
- **Tamamlanmayan Servis**: RE kodu ile yeniden planlama
- **Tamamlanan Servis**: 6 haneli random kod onayı
- **Müşteri Onayı**: Çift taraflı doğrulama sistemi

## 10. KURULUM VE YAPILANDIRMA

### 10.1 İlk Kurulum (Şekil 54-55)
- Müşteri ile birlikte yapılan ayarlar
- 6 haneli random onay kodu sistemi
- Email ve SMS çift onayı
- Garantiden çıkarma uyarı sistemi

### 10.2 Veri Yönetimi (Şekil 56-58)
- Manuel kıvrım verilerinin dışa aktarımı
- Yedekleme sistemleri
- Veri güvenliği

## 11. MAKİNE AYARLARI (Şekil 59)

### 11.1 Garanti Yönetimi
- **Bileşen Bazlı Garanti**: Mekanik, Elektrik, Sensör, Cetvel, Hidrolik
- Farklı süreler için geri sayım
- Süre bitiminde garanti dışı uyarısı

### 11.2 Sistem Ayarları
- **Big Data Bağlantısı**: Bulut bağlantı konfigürasyonu
- **Kullanıcı Yönetimi**: SMS kodlu kullanıcı ekleme/silme
- **Varsayılan Hız**: 6 metre/dakika (değiştirilebilir)
- **Karşılama Mesajı**: Özelleştirilebilir açılış mesajı

### 11.3 Bakım ve Kontrol
- **Check List**: Üretim sonrası kontrol listesi
- **PDF Kullanım Kitabı**: Sayfa sayfa erişilebilir dokümantasyon
- **Bakım Süreleri**: Haftalık, aylık, 6 aylık periyodlar
- **Otomatik Uyarılar**: Bakım zamanı geldiğinde bildirim

### 11.4 Kalibrayon ve Sıfırlama
- **Cetvel Değişikliği**: Otomatik cetvel sıfırlama
- **Piston Kalibrasyonu**: Alt/üst ölü nokta belirleme
- **Basınç Sensörü**: Bar basıncından otomatik algılama

### 11.5 Raporlama Sistemi
- **Hata Raporu**: Tüm hata, uyarı ve hareketlerin kaydı
- **Kullanım Süresi**: Basınç, boş çalışma sürelerinin takibi
- **Kıvrım Adetleri**: Kullanıcı bazlı kıvrım raporları
- **AI/Manuel Takibi**: Hangi modun kullanıldığının kaydı

### 11.6 Kullanıcı Hizmetleri
- **Dil Seçimi**: Konum bazlı veya manuel dil değişikliği
- **Sertifika Sorgulama**: Kullanıcı sertifikalarının görüntülenmesi
- **Email Gönderimi**: Sertifika basım desteği

### 11.7 Müşteri Değişikliği
- **Makine Devri**: Satış, takas, hibe durumlarında
- **Konum Takibi**: GPS bazlı konum değişikliği izleme
- **Veri Sıfırlama**: Eski müşteri bilgilerinin tamamen silinmesi
- **Yeni Müşteri Aktivasyonu**: 5 günlük konum onayı sistemi

## 12. TEKNİK ÖZELLİKLER

### 12.1 Donanım Entegrasyonu
- Encoder sistemi
- Cetvel (Ruler) sistemi
- Basınç sensörleri
- Temas sensörleri
- Hidrolik sistem kontrol

### 12.2 Yazılım Mimarisi
- Yapay zeka modülü entegrasyonu
- Big data bulut bağlantısı
- Gerçek zamanlı veri işleme
- Kullanıcı oturum yönetimi
- Güvenlik ve yetkilendirme

### 12.3 İletişim Protokolleri
- Wi-Fi bağlantısı
- SMS bildirimi
- Email sistemi
- Sosyal medya entegrasyonu
- GPS konum hizmetleri

## 13. GÜVENLİK VE UYGUNLUK

### 13.1 Güvenlik Önlemleri
- Çift el onayı sistemi
- Acil stop protokolleri
- Sensör arıza tespiti
- Basınç düşümü algılama
- Otomatik makine kilitleme

### 13.2 Veri Güvenliği
- Kullanıcı bazlı erişim kontrolü
- 6 haneli random kod sistemleri
- Çift faktörlü doğrulama
- Veri şifreleme ve yedekleme
- 30 günlük veri saklama politikası

Bu dokümantasyon, endüstriyel bir kıvrım makinesinin kapsamlı kullanıcı arayüzünü tanımlamaktadır. Sistem, yapay zeka destekli otomatik işlemlerden manuel kontrole, servis yönetiminden eğitim modüllerine kadar geniş bir yelpazede özellik sunmaktadır.