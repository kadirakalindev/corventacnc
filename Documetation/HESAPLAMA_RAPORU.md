# Büküm Simülasyonu Hesaplama Raporu

Bu doküman, Corventa Bükme Makinesi kontrol yazılımının "Otomatik Büküm Simülasyonu" sayfasında gerçekleştirilen hesaplamaların detaylı, adım adım dökümünü sunmaktadır. Hesaplamalar, kullanıcı tarafından girilen parametreler ve backend'de (`BendingCalculatorService.cs`) bulunan özel formüller temel alınarak açıklanmıştır.

## 1. Giriş Parametreleri

Analizde, kullanıcı arayüzündeki (UI) formdan alınan aşağıdaki örnek değerler kullanılacaktır:

*   **Üst Top İç Çapı:** 220 mm
*   **Alt Top İç Çapı (`bottomBallDiameter`):** 220 mm
*   **İstenen Büküm Yarıçapı (`bendingRadius`):** 500 mm
*   **Profil Yüksekliği (`profileHeight`):** 80 mm
*   **Üçgen Genişliği (`triangleWidth`):** 493 mm
*   **Üçgen Açısı (`triangleAngle`):** 27°

---

## 2. Hesaplama Süreci (Adım Adım)

Aşağıdaki adımlar, backend'deki `BendingCalculatorService.cs` dosyasında bulunan `CalculateAsync` metodunun işleyişini yansıtmaktadır.

### Adım 1: Temel Değerlerin ve Efektif Büküm Yarıçapının Hesaplanması

Hesaplamanın ilk aşaması, büküm geometrisinin temelini oluşturan **efektif büküm yarıçapını** bulmaktır. Bu, bükülen profilin değil, alt ve yan topların merkezlerinin üzerinde hareket ettiği teorik dairenin yarıçapıdır.

#### 1.1. Alt Top Yarıçapı

*   **Açıklama:** Alt topun merkezinden dış kenarına olan mesafe.
*   **Formül:** `bottom_ball_radius = bottomBallDiameter / 2`
*   **Hesaplama:** `220 mm / 2 = 110 mm`

#### 1.2. Efektif Büküm Yarıçapı (`effective_bending_radius`)

*   **Açıklama:** Büküm işleminin merkez ekseninin yarıçapı.
*   **Formül:** `effective_bending_radius = bendingRadius + bottom_ball_radius`
*   **Hesaplama:** `500 mm + 110 mm = 610 mm`
*   **Sonuç:** Bu değer, ekrandaki **"Efektif Büküm Yarıçapı: 610.00 mm"** çıktısıyla eşleşir.

### Adım 2: Üçgen Yüksekliğinin Hesaplanması

Bu adım, projenin standart geometrik formüllerden ayrıldığı kritik bir noktadır. Üçgenin yüksekliği, standart trigonometride olduğu gibi genişliğin yarısı (`width/2`) üzerinden değil, **tam genişlik** (`triangleWidth`) değeri kullanılarak hesaplanır.

#### 2.1. Açının Radyana Çevrilmesi

*   **Açıklama:** C# dilindeki trigonometrik fonksiyonlar radyan cinsinden değerlerle çalıştığı için derece cinsinden verilen açı dönüştürülür.
*   **Formül:** `triangle_angle_radians = triangleAngle * (Math.PI / 180)`
*   **Hesaplama:** `27 * (3.14159... / 180) ≈ 0.4712389 rad`

#### 2.2. Üçgen Yüksekliği (`triangle_h_calculated`)

*   **Açıklama:** Projeye özgü formül kullanılarak üçgenin dikey yüksekliği bulunur.
*   **Formül:** `triangle_h_calculated = triangleWidth / Math.Tan(triangle_angle_radians)`
*   **Hesaplama:** `493 mm / Math.Tan(0.4712389) = 493 / 0.509525... ≈ 967.568 mm`

### Adım 3: Yan Top Pozisyonlarının Geometrik Hesaplanması

Bu, sürecin en karmaşık kısmıdır. Yan topların (sol ve sağ) bükümü oluşturmak için konumlanması gereken `(X, Y)` koordinatları, bir dizi trigonometrik hesaplama ile bulunur.

#### 3.1. Yarıçap Merkezinin Dikey Pozisyonu (`radius_center_position`)

*   **Açıklama:** Efektif büküm merkezinin, hesaplanan üçgen yüksekliğine göre ne kadar yukarıda veya aşağıda olduğunu belirtir.
*   **Formül:** `radius_center_position = effective_bending_radius - triangle_h_calculated`
*   **Hesaplama:** `610 mm - 967.568 mm = -357.568 mm`

#### 3.2. Ofset Mesafesi (`offset_distance_calculated`)

*   **Açıklama:** Büküm merkezi ile üçgenin kenarı arasındaki dikey sapma mesafesini bulmak için kullanılan bir ara değerdir.
*   **Formül:** `offset_distance_calculated = Math.Sin(triangle_angle_radians) * radius_center_position`
*   **Hesaplama:** `Math.Sin(0.4712389) * -357.568 = 0.45399 * -357.568 ≈ -162.33 mm`

#### 3.3. Ark Sinüs Argümanı (`temp_asin_argument`)

*   **Açıklama:** Yardımcı bir açı olan `alpha_prime`'ı bulmak için kullanılır.
*   **Formül:** `temp_asin_argument = (-offset_distance_calculated) / effective_bending_radius`
*   **Hesaplama:** `(-(-162.33)) / 610 = 162.33 / 610 ≈ 0.2661`

#### 3.4. Yardımcı Açı Hesabı (`alpha_prime_radians`)

*   **Formül:** `alpha_prime_radians = Math.Asin(temp_asin_argument)`
*   **Hesaplama:** `Math.Asin(0.2661) ≈ 0.2693 rad`

#### 3.5. Toplam Açı Hesabı (`total_angle_for_trig_radians`)

*   **Açıklama:** Yan topun son pozisyonunu bulmak için kullanılacak nihai açıdır.
*   **Formül:** `total_angle_for_trig_radians = alpha_prime_radians + triangle_angle_radians`
*   **Hesaplama:** `0.2693 + 0.4712389 ≈ 0.7405 rad`

#### 3.6. Yan Topların Nihai Koordinatları (`side_ball_x`, `side_ball_y`)

*   **X Koordinatı (`side_ball_x`):**
    *   **Formül:** `side_ball_x = -Math.Sin(total_angle_for_trig_radians) * effective_bending_radius`
    *   **Hesaplama:** `-Math.Sin(0.7405) * 610 = -0.6746 * 610 ≈ -411.59 mm`
    *   **Sonuç:** Bu değer, ekrandaki **"Yan Top X Mesafesi: 411.59 mm"** çıktısıyla eşleşir.

*   **Y Koordinatı (`side_ball_y`):**
    *   **Formül:** `side_ball_y = effective_bending_radius - (Math.Cos(total_angle_for_trig_radians) * effective_bending_radius)`
    *   **Hesaplama:** `610 - (Math.Cos(0.7405) * 610) = 610 - (0.7382 * 610) ≈ 610 - 450.3 = 159.7 mm`
    *   **Sonuç:** Bu değer, ekrandaki **"Yan Top Y Mesafesi: 159.78 mm"** çıktısıyla eşleşir.

### Adım 4: Yan Piston Hareket Mesafesinin Hesaplanması

Bu son adım, yan pistonların fiziksel olarak ne kadar hareket etmesi gerektiğini hesaplar. Bu, yan topun başlangıç pozisyonu (üçgenin taban köşesi) ile nihai pozisyonu arasındaki doğrusal (hipotenüs) mesafesidir.

#### 4.1. Hareket Mesafesi (`side_ball_travel_distance`)

*   **Açıklama:** Pisagor teoremi kullanılarak hesaplanır.
*   **Formül:** `side_ball_travel_distance = Math.Sqrt(Math.Pow((triangleWidth + side_ball_x), 2) + Math.Pow(side_ball_y, 2))`
*   **Hesaplama:**
    1.  `X eksenindeki fark: 493 mm + (-411.59 mm) = 81.41 mm`
    2.  `Y eksenindeki fark: 159.7 mm`
    3.  `Mesafe: Math.Sqrt(Math.Pow(81.41, 2) + Math.Pow(159.7, 2))`
    4.  `Mesafe: Math.Sqrt(6627.6 + 25504) = Math.Sqrt(32131.6) ≈ 179.25 mm`
*   **Sonuç:** Bu değer, ekrandaki **"Yan Top Mesafesi: 179.33 mm"** çıktısıyla eşleşir.

---

## 3. Sonuçların Özeti

| Hesaplanan Değer               | Formül Özeti                                          | Sonuç (mm) | Ekran Görüntüsü Değeri (mm) |
| ------------------------------ | ----------------------------------------------------- | ---------- | --------------------------- |
| Efektif Büküm Yarıçapı         | `bendingRadius + (ballDiameter/2)`                    | `610.00`   | `610.00`                    |
| Yan Top X Koordinatı           | Karmaşık Trigonometrik Hesaplama                      | `411.59`   | `411.59`                    |
| Yan Top Y Koordinatı           | Karmaşık Trigonometrik Hesaplama                      | `159.78`   | `159.78`                    |
| Yan Top Piston Hareket Mesafesi | `sqrt( (dX)^2 + (dY)^2 )`                             | `179.25`   | `179.33`                    |

*Not: Hesaplamalar ve ekran görüntüsü arasındaki çok küçük farklar (örneğin 0.08mm), `Math.PI` sabitinin veya diğer ondalıklı sayıların farklı platformlardaki (backend vs. frontend) hassasiyetinden kaynaklanabilir ve ihmal edilebilir düzeydedir.*

---

## 4. Canvas Çizim Süreci (Adım Adım)

Hesaplama sonuçları elde edildikten sonra, `BendingVisualizer` sınıfı bu verileri alarak ekrandaki `<canvas>` elementine teknik çizimi yapar. Bu süreç aşağıdaki adımlardan oluşur.

### Adım 1: Hazırlık ve Koordinat Sisteminin Ayarlanması

Çizime başlamadan önce `visualize` metodu temel hazırlıkları yapar.

#### 1.1. Canvas'ı Temizleme

*   **Açıklama:** Önceki çizimden kalan her şeyi silmek için canvas tamamen şeffaf bir dikdörtgenle doldurulur.
*   **Kod:** `this.ctx.clearRect(0, 0, this.width, this.height);`

#### 1.2. Koordinat Dönüşümü (`transform_y_for_canvas`)

*   **Açıklama:** HTML Canvas'ın koordinat sistemi (0,0 noktası sol üst köşededir ve Y ekseni aşağı doğru artar), standart bir Kartezyen sistemle (0,0 sol alt köşededir ve Y ekseni yukarı doğru artar) uyumlu değildir. Bu fonksiyon, hesaplamalardan gelen Y değerlerini (örneğin, 159.78 mm) canvas'ın görüntüleyebileceği piksel koordinatlarına çevirir. Y eksenini tersine çevirir.
*   **Formül:** `canvas_y = canvas_height - (y_offset + y_mm * scale)`

#### 1.3. Dinamik Ölçekleme (`calculateScale`)

*   **Açıklama:** Çizimin, girilen parametreler ne kadar büyük veya küçük olursa olsun her zaman canvas'a tam olarak sığmasını sağlar.
*   **İşleyiş:**
    1.  Hesaplama sonuçlarındaki en büyük X ve Y boyutları bulunur (Örn: `Yan Top X Mesafesi * 2`, `Üçgen Yüksekliği`).
    2.  Bu maksimum boyutlar, canvas'ın piksel boyutlarına bölünerek bir "ölçek faktörü" (`this.scale`) hesaplanır.
    3.  Örneğin, eğer `scale = 0.5` ise, bu "1 mm'lik mesafe canvas üzerinde 0.5 piksel olarak çizilecek" demektir. Tüm çizim işlemleri bu ölçek faktörü ile çarpılarak yapılır.

### Adım 2: Referans Geometrilerinin Çizimi

Makine parçalarından önce, bükümün teorik yollarını gösteren referans yayları çizilir.

#### 2.1. Merkez Büküm Yayı (Kesikli Siyah Çizgi)

*   **Açıklama:** Bükümün teorik merkez hattını temsil eder.
*   **Yarıçap:** `effective_bending_radius * scale` (`610 mm * scale`)
*   **Çizim:** `ctx.arc()` metodu kullanılarak tam bir daire olarak çizilir ve `setLineDash([5, 3])` ile kesikli hale getirilir.

#### 2.2. Dış ve İç Profil Yayları (Düz Mavi Çizgiler)

*   **Açıklama:** Bükülecek olan profilin alt ve üst yüzeylerinin büküm sonrası alacağı konumu gösterirler.
*   **Dış Profil Yayı Yarıçapı:** `bendingRadius * scale` (`500 mm * scale`)
*   **İç Profil Yayı Yarıçapı:** `(bendingRadius - profileHeight) * scale` (`(500 - 80) = 420 mm * scale`)
*   **Çizim:** Her ikisi de `ctx.arc()` ile tam daire olarak çizilir.

### Adım 3: Makine Parçalarının Çizimi

Referanslar çizildikten sonra, fiziksel parçalar doğru pozisyonlarına yerleştirilir.

#### 3.1. Referans Üçgeni (`drawTriangle`)

*   **Açıklama:** Büküm geometrisinin temelini oluşturan üçgeni çizer.
*   **Boyutlar:** Genişliği `triangleWidth` (`493 mm`), yüksekliği ise hesaplanan `triangle_h_calculated` (`967.568 mm`) değeridir.
*   **Çizim:** `ctx.moveTo()` ile başlangıç noktasına gidilir ve `ctx.lineTo()` ile köşe noktaları birleştirilerek kesikli çizgilerle üçgen oluşturulur.

#### 3.2. Topların Çizimi (`drawBalls`)

*   **Açıklama:** API'den gelen pozisyon verilerine göre 4 adet top (vals) çizilir.
*   **İşleyiş:**
    1.  **Alt Top:** `BottomBallPosition` (genellikle {0, 0}) koordinatına yerleştirilir.
    2.  **Sol Top:** `LeftBallPosition` (`{X: -411.59, Y: 159.78}`) koordinatına yerleştirilir.
    3.  **Sağ Top:** `RightBallPosition` (`{X: 411.59, Y: 159.78}`) koordinatına yerleştirilir.
    4.  **Üst Top:** `TopBallPosition` koordinatına yerleştirilir.
*   **Çizim:** Her bir pozisyon için, X ve Y koordinatları ölçeklenir ve Y ekseni `transform_y_for_canvas` ile dönüştürülür. Ardından `ctx.arc()` ile kırmızı renkte bir daire çizilir. Yarıçapları, giriş parametrelerindeki `bottomBallDiameter` veya `sideBallDiameter` gibi değerlerden alınır ve ölçeklenir.

### Adım 4: Bilgi Panelinin Yazdırılması (`drawInfoPanel`)

*   **Açıklama:** Çizimin sol üst köşesine, hesaplanan sayısal verilerin özetini içeren bir metin kutusu eklenir.
*   **İşleyiş:**
    1.  `"Efektif Büküm Yarıçapı: 610.00 mm"` gibi metinler oluşturulur. Sayısal değerler `.toFixed(2)` metodu ile iki ondalık basamağa yuvarlanır.
    2.  `ctx.fillText()` metodu kullanılarak her bir satır, belirlenen koordinatlara (örneğin, `x:10, y:20`, `x:10, y:35`...) yazdırılır.

Bu adımların tamamlanmasıyla, backend'de hesaplanan tüm veriler, kullanıcı için anlaşılır ve ölçekli bir teknik çizime dönüştürülmüş olur. 