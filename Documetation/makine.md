{
  "document_title": "Corventa CNC Profil Büküm Makinesi - Teknik Dokümantasyon",
  "sections": [
    {
      "title": "1. Genel Tanıtım",
      "content": "Corventa, 4 vals topuna sahip, CNC tabanlı bir hidrolik profil büküm makinesidir. Bu makine, metal profillere hassas ve tekrarlanabilir büküm işlemleri uygulamak üzere tasarlanmıştır. Sistem, hidrolik pistonlar, lineer cetveller (ruler), sensörler, gelişmiş bir kontrol mimarisi ve güvenlik mekanizmaları üzerine kuruludur.\n\nMakinenin temel amacı, bir üst sıkıştırma valsi ve üç alt şekillendirme valsi kullanarak profilleri istenen yarıçapta bükmektir. Ayrıca, büküm sırasında profilin stabilitesini sağlamak için sağ ve sol taraflarda bulunan Yan Dayama Grupları (Side Supports) mevcuttur."
    },
    {
      "title": "2. Makine Mimarisi ve Ana Bileşenler",
      "subsections": [
        {
          "title": "2.1. Vals Topları (Rolls)",
          "content": "Makinede 4 adet ana vals topu bulunur:\n\n- **Üst Vals Topu (Top Roll):** Makineye yüklenen profili, alttaki referans valsine doğru bastırarak sıkıştırma görevini üstlenir. Bu hareket `M13_M14_TopPiston` ile kontrol edilir.\n- **Alt Orta Vals Topu (Bottom-Middle Roll):** Sabit bir referans noktasıdır. Büküm operasyonu için dayanak noktası oluşturur. Hareketi `M15_M16_BottomPiston` ile kontrol edilir.\n- **Sağ ve Sol Alt Vals Topları (Bottom-Right & Left Rolls):** Büküm işlemini gerçekleştiren ana toplardır. Belirlenen pozisyonlara hidrolik pistonlar aracılığıyla hareket ederek profile istenen formu verirler. Bu hareketler `M17-M20` pistonları ile sağlanır."
        },
        {
          "title": "2.2. Yan Dayama Grupları (Side Support Groups)",
          "content": "Büküm esnasında profilin burulmasını ve deforme olmasını engellemek için iki adet yan dayama grubu bulunur. Her grup 3 ana pistondan oluşur:\n\n- **Sol Yan Dayama Grubu:** Reel Piston (`M01_M02`), Body Piston (`M03_M04`), Join Piston (`M05_M06`).\n- **Sağ Yan Dayama Grubu:** Reel Piston (`M07_M08`), Body Piston (`M09_M10`), Join Piston (`M11_M12`)."
        },
        {
          "title": "2.3. Hidrolik ve Kontrol Sistemi",
          "content": "Makinenin tüm hareketleri hidrolik pistonlarla sağlanır. Bu pistonların kontrolü `S1` ve `S2` olarak adlandırılan iki ana valf grubu üzerinden yapılır.\n\n- **S1 Valfi:** Sol yan dayama grubunu, üst pistonu ve alt-orta pistonu kontrol eder.\n- **S2 Valfi:** Sağ yan dayama grubunu, sağ ve sol alt pistonları kontrol eder.\n- **Rotasyon:** Vals toplarının döndürülmesi işlemi, `S1` ve `S2` valflerinin senkronize kullanımı ile gerçekleştirilir."
        }
      ]
    },
    {
      "title": "3. Kontrol Mantığı ve Sinyal İşleme",
      "subsections": [
        {
          "title": "3.1. Hareket Kontrolü",
          "content": "Pistonların hareketi, analog çıkış kanallarından gönderilen voltaj sinyalleri ile kontrol edilir. Voltaj aralığı, hareketin yönünü belirler:\n\n- **İleri Hareket (Forward):** `0V` ile `-10V` arası bir voltaj uygulanır.\n- **Geri Hareket (Backward):** `0V` ile `+10V` arası bir voltaj uygulanır.\n\nSistemin kontrol kartındaki DAC (Digital-to-Analog Converter) aralığı **2048 (-10V) ile 2047 (+10V)** arasındadır. `0` değeri `0V`'a karşılık gelir."
        },
        {
          "title": "3.2. Pozisyon ve Basınç Okuma",
          "content": "Pistonların anlık pozisyonları ve sistemdeki hidrolik basınç gibi değerler, **Lineer Cetveller (Ruler)** ve sensörler aracılığıyla okunur. Bu bileşenler analog giriş kanallarına bağlıdır.\n\n- Okunan ham dijital değerler (örn: 0-4095 veya 0-32767), anlamlı fiziksel birimlere (mm, bar, °C, cm³/sn) dönüştürülmelidir.\n- **Örnek Pozisyon Dönüşümü:** `RulerM13_M14_TopPiston` cetveli 160mm kursa sahiptir ve 0-32767 arasında bir değer okur.\n  `Pozisyon (mm) = (Okunan_Deger / 32767.0) * 160.0`\n- **Örnek Basınç Dönüşümü:** `S1_OilPressure` sensörü 0-400 bar ölçüm yapıyorsa ve okunan değer 0-4095 aralığındaysa:\n  `Basınç (bar) = (Okunan_Deger / 4095.0) * 400.0`"
        },
        {
          "title": "3.3. Hassas Konumlandırma Stratejisi",
          "content": "Pistonların hedeflenen bir konuma hassas bir şekilde gönderilmesi için **kademeli yavaşlama (ramping down)** stratejisi uygulanır. Piston hedefe yaklaşırken, hareket hızı (uygulanan voltaj) orantısal olarak azaltılır. Bu, hedefe minimum tolerans payı ile durulmasını, mekanik stresi azaltmayı ve konumlandırma hassasiyetini en üst düzeye çıkarmayı sağlar."
        }
      ]
    },
    {
      "title": "4. Piston Hareketleri ve Kurs Ölçüleri",
      "subsections": [
        {
          "title": "4.1. Yan Dayama Pistonları",
          "table": {
            "headers": ["Piston Grubu", "Hareket Açıklaması", "Kurs Ölçüsü"],
            "rows": [
              ["M01-M02 / M07-M08", "Yan Dayama Makara Pistonları", "352 mm"],
              ["M05-M06 / M11-M12", "Yan Dayama Mafsal Pistonları", "187 mm"],
              ["M03-M04 / M09-M10", "Yan Dayama Gövde Pistonları", "129 mm"]
            ]
          }
        },
        {
          "title": "4.2. Ana Pistonlar",
          "table": {
            "headers": ["Piston Grubu", "Hareket Açıklaması", "Kurs Ölçüsü"],
            "rows": [
              ["M17-M18 / M19-M20", "Sağ/Sol Alt Pistonler", "422 mm"],
              ["M13-M14", "Üst Piston", "160 mm"],
              ["M15-M16", "Alt Orta Piston", "195 mm"]
            ]
          }
        }
      ]
    },
    {
      "title": "5. Yazılım Mimarisi ve Tasarım Prensipleri",
      "subsections": [
        {
          "title": "5.1. Katmanlı Mimari (Layered Architecture)",
          "content": "Proje, best practice'lere uygun, katmanlı bir mimari ile geliştirilecektir. UI (Kullanıcı Arayüzü), BLL (İş Mantığı Katmanı) ve DAL (Veri Erişim Katmanı) gibi katmanların ayrımı, sistemin bakımını, güncellenmesini ve yeni özellik eklenmesini kolaylaştıracaktır."
        },
        {
          "title": "5.2. Çok Kanallı (Multithreaded) Yapı",
          "content": "Uygulama, **multithread** bir yapıda çalışacaktır. Makineden anlık veri akışını yöneten iletişim kanalı (Socket) ve otomatik büküm gibi uzun süren prosesler, ana kullanıcı arayüzü (UI) thread'ini blocklamayacak şekilde ayrı thread'lerde çalışacaktır. Bu sayede, makine büküm yaparken bile operatör arayüzde gezinebilir, anlık verileri takılma olmadan izleyebilir."
        },
        {
          "title": "5.3. Geleceğe Yönelik Tasarım (Future-Proofing)",
          "content": "Yazılım mimarisi, ileride **Cloud (Bulut) veritabanı** entegrasyonuna olanak tanıyacak şekilde esnek tasarlanacaktır. Pistonların son konumları gibi operasyonel veriler, hem yerel olarak saklanacak hem de ileride bulut sistemleriyle senkronize edilebilecektir."
        }
      ]
    },
    {
      "title": "6. Güvenlik Mekanizmaları ve Hata Yönetimi",
      "subsections": [
        {
          "title": "6.1. Başlangıç Kontrolleri (Startup Checks)",
          "content": "Uygulama ilk başlatıldığında, `HydraulicEngineThermalError`, `FanEngineThermalError`, `PhaseSequenceError` gibi kritik hata adreslerini kontrol eder. Eğer bu hatalardan herhangi biri aktif ise, operatöre bir uyarı mesajı gösterilir ve sorunun giderilmesi sağlanana kadar ana operasyon ekranlarının açılmasına izin verilmez."
        },
        {
          "title": "6.2. Maksimum Basınç Koruması (Max Bar Protection)",
          "content": "Ayarlar sayfasında tanımlanan **maksimum çalışma basıncı (Max Bar)** değeri, otomatik büküm sırasında sürekli olarak izlenir. Eğer sistem basıncı bu limiti aşarsa, makine anında **Acil Stop** moduna geçer, tüm hareketleri durdurur ve operatörü uyarır."
        },
        {
          "title": "6.3. Ani Basınç Düşüşü Tespiti",
          "content": "Otomatik büküm gibi yüksek basınç gerektiren bir işlem sırasında (örneğin 70 bar), sistem basıncında beklenmedik ve ani bir düşüş (örn. 20 bar'a düşmesi) tespit edilirse, bu durum bir anomali olarak kabul edilir. Profilin kırılması veya deforme olması gibi bir duruma işaret edebileceğinden, sistem otomatik olarak **Acil Stop** moduna geçerek olası hasarı önler."
        }
      ]
    },
    {
      "title": "7. Yazılım Arayüzü ve Operasyon Modları",
      "subsections": [
        {
          "title": "7.1. İletişim Protokolü",
          "content": "Makine kontrol ünitesi ile kullanıcı arayüzü arasındaki veri akışı, **Socket** tabanlı bir iletişim protokolü üzerinden gerçek zamanlı (real-time) olarak gerçekleştirilir. UI, bu verileri alarak ilgili sayfalarda anlık olarak görselleştirir."
        },
        {
          "title": "7.2. Monitoring Sayfası (İzleme)",
          "content": "Makinenin anlık durumunu görsel olarak izlemek için tasarlanmış bir gösterge panelidir. Piston pozisyonları, basınçlar, sıcaklık, alarmlar ve makine durumu gibi veriler bu ekranda canlı olarak sunulur."
        },
        {
          "title": "7.3. Manuel Kontrol Sayfası",
          "content": "Operatörün, makinenin tüm bileşenlerini bireysel olarak kontrol etmesini sağlar.\n\n- **Buton-Basılı Hareket (Jogging):** Operatör, bir pistonun 'İleri' veya 'Geri' butonuna basılı tuttuğu sürece piston hareket eder. Buton bırakıldığında hareket durur.\n- **Hedef Konuma Git (Go to Position):** Operatör, bir metin kutusuna hedef konumu (örn. '150.5 mm') girip 'Git' butonuna tıklar. Piston, hassas konumlandırma stratejisi ile otomatik olarak hedefe gider."
        },
        {
          "title": "7.4. Otomatik Büküm Sayfası",
          "content": "CNC tabanlı, reçete bazlı otomatik büküm operasyonlarının yapıldığı ana çalışma ekranıdır. Operatörün girdiği verilere göre **paso sayısı** ve **adım mesafesi** hesaplanarak profil sıkıştırma, sıfırlama ve otomatik büküm işlemleri sırayla gerçekleştirilir. Önceden tanımlanmış **Stage**'ler (örn. 0, 60, 120 mm pozisyonları) ile hızlı pozisyon geçişleri sağlanır."
        },
        {
          "title": "7.5. Ayarlar Sayfası",
          "content": "Makinenin temel konfigürasyon ve yönetiminin yapıldığı yetkili personel alanıdır. İçeriği:\n\n- **Makine Parametreleri:** Vals topu çapları, piston kurs mesafeleri, profil reset mesafesi.\n- **Kullanıcı Yönetimi:** Farklı yetki seviyelerine sahip kullanıcılar (Operatör, Bakımcı, Yönetici) oluşturma ve yönetme.\n- **Raporlama:** Hata raporları ve kıvrım adetleri gibi üretim istatistiklerinin görüntülendiği alan.\n- **Sistem Ayarları:** Garanti süresi bilgileri, dil seçimi (Türkçe, İngilizce vb.).\n- **Kalibrasyon:** Cetvel sıfırlama işlemleri ve sensör kalibrasyon katsayıları."
        },
        {
          "title": "7.6. Servis Sayfası",
          "content": "Makinenin bakım, arıza takibi ve servis yönetimi için kullanılır. Servis kaydı oluşturma, periyodik bakım takvimi, arıza geçmişi (loglar) gibi özellikleri barındırır."
        }
      ]
    },
    {
      "title": "8. G/Ç (Giriş/Çıkış) Adres Haritası",
      "subsections": [
        {
          "title": "8.1. Dijital Girişler (DI - Read)",
          "table": {
            "headers": ["Açıklama", "Adres", "C# Sabiti"],
            "rows": [
              ["Hidrolik Motor Termik Hatası", "0x0000", "HydraulicEngineThermalError"],
              ["Fan Motoru Termik Hatası", "0x0001", "FanEngineThermalError"],
              ["Faz Sırası Hatası", "0x0002", "PhaseSequenceError"],
              ["Kirlilik Sensörü 1", "0x0003", "PollutionSensor1"],
              ["Kirlilik Sensörü 2", "0x0004", "PollutionSensor2"],
              ["Kirlilik Sensörü 3", "0x0005", "PollutionSensor3"],
              ["Sol Dönüş Sensörü", "0x0006", "LeftRotationSensor"],
              ["Sağ Dönüş Sensörü", "0x0007", "RightRotationSensor"],
              ["Sol Parça Varlığı", "0x0008", "LeftPartPresence"],
              ["Sağ Parça Varlığı", "0x0009", "RightPartPresence"],
              ["Acil Stop Butonu", "0x000A", "EmergenyStopButton"]
            ]
          }
        },
        {
          "title": "8.2. Dijital Çıkışlar (DO - Write)",
          "table": {
            "headers": ["Açıklama", "Adres", "C# Sabiti"],
            "rows": [
              ["S1 Ana Valf Aktif", "0x1000", "S1"],
              ["S2 Ana Valf Aktif", "0x1001", "S2"],
              ["Sol Makara Piston İleri (M01)", "0x100E", "M01_LeftReelPistonForward"],
              ["Sol Makara Piston Geri (M02)", "0x100F", "M02_LeftReelPistonBackward"],
              ["Sol Gövde İleri (M03)", "0x100A", "M03_LeftBodyForward"],
              ["Sol Gövde Geri (M04)", "0x100B", "M04_LeftBodyBackward"],
              ["Sol Mafsal Piston İleri (M05)", "0x1006", "M05_LeftJoinPistonForward"],
              ["Sol Mafsal Piston Geri (M06)", "0x1007", "M06_LeftJoinPistonBackward"],
              ["Sağ Makara Piston İleri (M07)", "0x100C", "M07_RightReelPistonForward"],
              ["Sağ Makara Piston Geri (M08)", "0x100D", "M08_RightReelPistonBackward"],
              ["Sağ Gövde İleri (M09)", "0x1008", "M09_RightBodyForward"],
              ["Sağ Gövde Geri (M10)", "0x1009", "M10_RightBodyBackward"],
              ["Sağ Mafsal Piston İleri (M11)", "0x1004", "M11_RightJoinPistonForward"],
              ["Sağ Mafsal Piston Geri (M12)", "0x1005", "M12_RightJoinPistonBackward"],
              ["Dönüş Saat Yönü Tersi (CWW) (M21)", "0x1003", "M21_Rotation_CWW"],
              ["Dönüş Saat Yönü (CW) (M22)", "0x1002", "M22_Rotation_CW"],
              ["Pnömatik Valf 1 Açık (P1)", "0x1010", "P1_Open"],
              ["Pnömatik Valf 1 Kapalı (P1)", "0x1011", "P1_Close"],
              ["Pnömatik Valf 2 Açık (P2)", "0x1012", "P2_Open"],
              ["Pnömatik Valf 2 Kapalı (P2)", "0x1013", "P2_Close"],
              ["Fan Motoru Aktif", "0x1014", "FanEngine"],
              ["Hidrolik Motor Aktif", "0x1015", "HydraulicEngine"],
              ["Alarm Sinyali", "0x1016", "Alarm"]
            ]
          }
        },
        {
          "title": "8.3. Analog Girişler (AI - Read)",
          "table": {
            "headers": ["Açıklama", "Adres", "Veri Aralığı", "Fiziksel Birim", "C# Sabiti"],
            "rows": [
              ["Cetvel: Sol Yan Dayama Makara Pistonu", "0x0005", "0-4095", "mm", "RulerM01_M02_LeftSideSupportReelPiston"],
              ["Cetvel: Sol Yan Dayama Gövde", "0x0003", "0-4095", "mm", "RulerM03_M04_LeftSideSupportBody"],
              ["Cetvel: Sol Yan Dayama Mafsal Pistonu", "0x0007", "0-4095", "mm", "RulerM05_M06_LeftSideSupportJoinPiston"],
              ["Cetvel: Sağ Yan Dayama Makara Pistonu", "0x0004", "0-4095", "mm", "RulerM07_M08_RightSideSupportReelPiston"],
              ["Cetvel: Sağ Yan Dayama Gövde", "0x0002", "0-4095", "mm", "RulerM09_M10_RightSideSupportBody"],
              ["Cetvel: Sağ Yan Dayama Mafsal Pistonu", "0x0006", "0-4095", "mm", "RulerM11_M12_RightSideSupportJoinPiston"],
              ["Cetvel: Üst Piston Pozisyonu", "0x0016", "0-32767", "mm", "RulerM13_M14_TopPiston"],
              ["Cetvel: Alt Orta Piston Pozisyonu", "0x0018", "0-32767", "mm", "RulerM15_M16_BottomPiston"],
              ["Cetvel: Sol Alt Piston Pozisyonu", "0x0014", "0-32767", "mm", "RulerM17_M18_LeftPiston"],
              ["Cetvel: Sağ Alt Piston Pozisyonu", "0x0012", "0-32767", "mm", "RulerM19_M20_RightPiston"],
              ["Cetvel: Sol Pnömatik Valf Pozisyonu", "0x001C", "0-32767", "mm", "RulerLeftPneumaticValve"],
              ["Cetvel: Sağ Pnömatik Valf Pozisyonu", "0x001A", "0-32767", "mm", "RulerRightPneumaticValve"],
              ["Cetvel: Rotasyon Açısı", "0x001E", "0-32767", "Derece", "RulerRotation"],
              ["S1 Yağ Basıncı", "0x000B", "0-4095", "Bar", "S1_OilPressure"],
              ["S2 Yağ Basıncı", "0x000A", "0-4095", "Bar", "S2_OilPressure"],
              ["S1 Yağ Akış Oranı", "0x000D", "0-4095", "cm³/sn", "S1_OilFlowRate"],
              ["S2 Yağ Akış Oranı", "0x000C", "0-4095", "cm³/sn", "S2_OilFlowRate"],
              ["Yağ Sıcaklığı", "0x000E", "0-4095", "°C", "OilTemperature"],
              ["Yağ Nemi", "0x000F", "0-4095", "%", "OilHumidity"],
              ["Yağ Seviyesi", "0x0010", "0-4095", "%", "OilLevel"]
            ]
          }
        },
        {
          "title": "8.4. Analog Çıkışlar (AO - Write)",
          "table": {
            "headers": ["Açıklama", "Adres", "Veri Aralığı", "Voltaj Karşılığı", "C# Sabiti"],
            "rows": [
              ["Üst Piston Voltaj (M13-M14)", "0x0805", "2048 - 2047", "-10V / +10V", "M13_M14_TopPistonVolt"],
              ["Alt Orta Piston Voltaj (M15-M16)", "0x0804", "2048 - 2047", "-10V / +10V", "M15_M16_BottomPistonVolt"],
              ["Sol Alt Piston Voltaj (M17-M18)", "0x0803", "2048 - 2047", "-10V / +10V", "M17_M18_LeftPistonVolt"],
              ["Sağ Alt Piston Voltaj (M19-M20)", "0x0802", "2048 - 2047", "-10V / +10V", "M19_M20_RightPistonVolt"],
              ["Dönüş Hızı Voltajı (M23)", "0x0806", "2048 - 2047", "-10V / +10V", "M23_RotationSpeedVolt"],
              ["Cetvel Sıfırlama: M17-M20", "0x080A", "-", "-", "RulerResetM17toM20"],
              ["Cetvel Sıfırlama: M13-M16", "0x080B", "-", "-", "RulerResetM13toM16"],
              ["Cetvel Sıfırlama: Pnömatik Valfler", "0x080C", "-", "-", "RulerResetPneumaticValve"],
              ["Cetvel Sıfırlama: Rotasyon", "0x080D", "-", "-", "RulerResetRotation"]
            ]
          }
        }
      ]
    },
    {
      "title": "9. Hareket Kontrol Tablosu",
      "content": "Bu tablo, belirli bir hareketi gerçekleştirmek için hangi valflerin ve dijital/analog çıkışların aktive edilmesi gerektiğini gösterir.",
      "table": {
        "headers": ["#", "Valf Kodu / Sinyal", "Hareket Açıklaması", "Grup"],
        "rows": [
          ["1", "S1 + M01_LeftReelPistonForward", "Sol Yan Dayama Makara Pistonu İleri", "YAN DAYAMA GRUBU SOL"],
          ["2", "S1 + M02_LeftReelPistonBackward", "Sol Yan Dayama Makara Pistonu Geri", "YAN DAYAMA GRUBU SOL"],
          ["3", "S1 + M03_LeftBodyForward", "Sol Yan Dayama Gövde Hareketi İleri", "YAN DAYAMA GRUBU SOL"],
          ["4", "S1 + M04_LeftBodyBackward", "Sol Yan Dayama Gövde Hareketi Geri", "YAN DAYAMA GRUBU SOL"],
          ["5", "S1 + M05_LeftJoinPistonForward", "Sol Yan Dayama Mafsal Pistonu İleri", "YAN DAYAMA GRUBU SOL"],
          ["6", "S1 + M06_LeftJoinPistonBackward", "Sol Yan Dayama Mafsal Pistonu Geri", "YAN DAYAMA GRUBU SOL"],
          ["7", "S2 + M07_RightReelPistonForward", "Sağ Yan Dayama Makara Pistonu İleri", "YAN DAYAMA GRUBU SAĞ"],
          ["8", "S2 + M08_RightReelPistonBackward", "Sağ Yan Dayama Makara Pistonu Geri", "YAN DAYAMA GRUBU SAĞ"],
          ["9", "S2 + M09_RightBodyForward", "Sağ Yan Dayama Gövde Hareketi İleri", "YAN DAYAMA GRUBU SAĞ"],
          ["10", "S2 + M10_RightBodyBackward", "Sağ Yan Dayama Gövde Hareketi Geri", "YAN DAYAMA GRUBU SAĞ"],
          ["11", "S2 + M11_RightJoinPistonForward", "Sağ Yan Dayama Mafsal Pistonu İleri", "YAN DAYAMA GRUBU SAĞ"],
          ["12", "S2 + M12_RightJoinPistonBackward", "Sağ Yan Dayama Mafsal Pistonu Geri", "YAN DAYAMA GRUBU SAĞ"],
          ["13", "S1 + M13_M14_TopPistonVolt (-10V)", "Üst Ana Piston Geri (Sıkıştırma)", "ÜST PİSTON HAREKETİ"],
          ["14", "S1 + M13_M14_TopPistonVolt (+10V)", "Üst Ana Piston İleri (Bırakma)", "ÜST PİSTON HAREKETİ"],
          ["15", "S1 + M15_M16_BottomPistonVolt (-10V)", "Alt Ana Piston İleri", "ALT PİSTON HAREKETİ"],
          ["16", "S1 + M15_M16_BottomPistonVolt (+10V)", "Alt Ana Piston Geri", "ALT PİSTON HAREKETİ"],
          ["17", "S2 + M17_M18_LeftPistonVolt (-10V)", "Ana Piston Sol Yan İleri (Bükme)", "SOL YAN PİSTON HAREKETİ"],
          ["18", "S2 + M17_M18_LeftPistonVolt (+10V)", "Ana Piston Sol Yan Geri (Bırakma)", "SOL YAN PİSTON HAREKETİ"],
          ["19", "S2 + M19_M20_RightPistonVolt (-10V)", "Ana Piston Sağ Yan İleri (Bükme)", "SAĞ YAN PİSTON HAREKETİ"],
          ["20", "S2 + M19_M20_RightPistonVolt (+10V)", "Ana Piston Sağ Yan Geri (Bırakma)", "SAĞ YAN PİSTON HAREKETİ"],
          ["21", "S1 + S2 + M21_Rotation_CWW", "Saat Yönünün Tersi Dönüş", "REDÜKTÖR GRUBU"],
          ["22", "S1 + S2 + M22_Rotation_CW", "Saat Yönü Dönüş", "REDÜKTÖR GRUBU"],
          ["23", "M23_RotationSpeedVolt (değişken voltaj)", "Dönüş Hızı Azaltma/Artırma (%10-100)", "DÖNÜŞ HIZI AYAR GRUBU"]
        ]
      }
    },
     {
      10. Piston hareketleri için önemli not!!!
      Piston hareketi yaptırılırken valf, volt sırasını belirtir.
     Pistonlara ileri/geri hareket verilirken, veya rotasyon yaptırılırken;
     -Önce bağlı olduğu valf açılır, sonrasında ise uygun adrese uygun veri gönderilerek hareket sağlanıyor.
    Pistonları durdurma için;
    -Önce gönderilen hız değeri durdurmak için uygun şekilde gönderilir, sonrasında ise ilgili valf kapatılır.

    Acil Stop: Önce tüm piston vs gönderilen hız vs. gibi değerler sıfırlanır, sonrasında ise valfler, en sonda ise S1 ve S2 valfi kapatılarak acil stopa geçirilir.

    Sistem çalışırken yağ sıcaklığı 50 derece ve üstüne çıkarsa fan otomatik olarak açılır.
    40 derece ve altında ise otomatik olarak kapatılır.

    Sistem ilk açıldığında ayarlar sayfasında belirlenecek olan "Çalışma yağ ısısı" parametresi kontrol edilerek, yağ belirlenen sıcaklığın altında ise, hiçbir fonksiyonu kullanmaya izin vermeden yağ belirlenen sıcaklığa ulaşana kadar pistonları ileri/geri hareket ettirerek (örneğin 20mm ileri ve geri) yağın çalışma ısısına gelmesi sağlanır.
    }
  ]
}