# 🎨 **Canvas Çiziminde Profil Çizim Teknikleri Detaylı Analiz**

## 📋 **Genel Bakış**

Corventa Büküm Makinesi'nin web arayüzünde, profil büküm simülasyonu için **HTML5 Canvas** kullanılarak **3 farklı çizgi türü** ile görselleştirme yapılır:

1. **🔲 Kesikli Çizgi** (Dashed Line) - Merkez ve referans çizgileri
2. **🔵 Dış Çizgi** (Outer Line) - Dış profil sınırı  
3. **🔵 İç Çizgi** (Inner Line) - İç profil sınırı

---

## 🎯 **1. Kesikli Çizgi (Dashed Line) Çizimi**

### **Kullanım Alanları:**
- ✅ **Merkez büküm yayı** (referans çizgisi)
- ✅ **Üçgen geometri çizgileri** (yardımcı çizgiler)
- ✅ **Koordinat eksenleri** (rehber çizgiler)

### **Teknik Implementasyon:**

```javascript
// 1. Kesikli çizgi stilini ayarla
this.ctx.setLineDash([5, 3]); // 5px çizgi, 3px boşluk
this.ctx.strokeStyle = 'black';
this.ctx.lineWidth = 2;

// 2. Merkez büküm yayını çiz
this.ctx.beginPath();
this.ctx.arc(center_x_px, arc_center_y_px_corrected, bending_center_radius_px, 0, 2 * Math.PI);
this.ctx.stroke();

// 3. Kesikli çizgi stilini sıfırla
this.ctx.setLineDash([]); // Normal çizgiye dön
```

### **Kesikli Çizgi Desenleri:**

| Desen | Kod | Görünüm | Kullanım |
|-------|-----|---------|----------|
| **Kısa Kesik** | `[5, 3]` | `-----   -----   -----` | Merkez çizgileri |
| **Uzun Kesik** | `[20, 5, 8, 5]` | `-------- - -------- -` | Üçgen geometri |
| **Noktalı** | `[2, 2]` | `- - - - - - - -` | İnce rehber çizgiler |

### **Üçgen Geometri Çizimi:**

```javascript
// Üçgen çizimi için yardımcı metod
drawTriangle(center_x_px, result) {
    const triangle_w_px = (result.TriangleWidth / 2) * this.scale;
    const triangle_h_px = result.TriangleHeight * this.scale;

    const tx_apex = center_x_px;
    const ty_apex = this.transform_y_for_canvas(result.TriangleHeight);
    const tx_bottom_left = center_x_px - triangle_w_px;
    const tx_bottom_right = center_x_px + triangle_w_px;
    const ty_bottom = this.transform_y_for_canvas(0);

    this.ctx.beginPath();
    this.ctx.strokeStyle = 'black';
    this.ctx.lineWidth = 1;
    this.ctx.setLineDash([5, 3]);

    // Taban çizgisi
    this.ctx.moveTo(tx_bottom_left, ty_bottom);
    this.ctx.lineTo(tx_bottom_right, ty_bottom);

    // Yan kenarlar
    this.ctx.moveTo(tx_bottom_left, ty_bottom);
    this.ctx.lineTo(tx_apex, ty_apex);
    this.ctx.moveTo(tx_bottom_right, ty_bottom);
    this.ctx.lineTo(tx_apex, ty_apex);

    // Dikey merkez çizgisi
    this.ctx.moveTo(center_x_px, ty_bottom);
    this.ctx.lineTo(center_x_px, ty_apex);

    this.ctx.stroke();
    this.ctx.setLineDash([]);
}
```

---

## 🔵 **2. Dış Profil Çizgisi (Outer Line)**

### **Matematiksel Hesaplama:**

```javascript
// Dış profil yarıçapı = Büküm yarıçapı - Vals topu yarıçapı
const outer_profile_radius_mm = effective_bending_radius_mm - bottom_ball_radius_mm;
const outer_profile_radius_px = outer_profile_radius_mm * this.scale;
```

### **Çizim İmplementasyonu:**

```javascript
// Dış profil çizgisi (MAVİ - Kalın)
this.ctx.beginPath();
this.ctx.strokeStyle = 'blue';
this.ctx.lineWidth = 3; // Kalın çizgi
this.ctx.setLineDash([]); // Kesintisiz çizgi
this.ctx.arc(center_x_px, arc_center_y_px_corrected, outer_profile_radius_px, 0, 2 * Math.PI);
this.ctx.stroke();
```

### **Fiziksel Anlamı:**
- 🔧 **Profil dış yüzeyi** ile vals topunun **temas noktası**
- 📏 **Büküm yarıçapından** vals topu yarıçapı **çıkarılır**
- 🎯 **Malzeme dış kenarının** takip edeceği yol

### **Alternatif Çizim Yöntemi:**

```javascript
// Backup sistemdeki dış büküm çizgisi
const bendingOutsideRadius = (bending_radius - ball_radius) * scale;

ctx.strokeStyle = 'blue';
ctx.lineWidth = 3;
ctx.setLineDash([]);
ctx.beginPath();
ctx.arc(radiusOutsideX, radiusOutsideY, bendingOutsideRadius * 2, 0, Math.PI * 2);
ctx.stroke();
```

---

## 🔵 **3. İç Profil Çizgisi (Inner Line)**

### **Matematiksel Hesaplama:**

```javascript
// İç profil yarıçapı = Dış profil yarıçapı - Profil kalınlığı
const inner_profile_radius_mm = outer_profile_radius_mm - profile_height_mm;
const inner_profile_radius_px = inner_profile_radius_mm * this.scale;
```

### **Çizim İmplementasyonu:**

```javascript
// İç profil çizgisi (MAVİ - İnce)
this.ctx.beginPath();
this.ctx.strokeStyle = 'blue';
this.ctx.lineWidth = 1; // İnce çizgi
this.ctx.setLineDash([]); // Kesintisiz çizgi
this.ctx.arc(center_x_px, arc_center_y_px_corrected, inner_profile_radius_px, 0, 2 * Math.PI);
this.ctx.stroke();
```

### **Fiziksel Anlamı:**
- 🔧 **Profil iç yüzeyi** sınırı
- 📏 **Dış profilden** profil kalınlığı **çıkarılır**
- 🎯 **Malzeme iç kenarının** takip edeceği yol

### **Alternatif Çizim Yöntemi:**

```javascript
// Backup sistemdeki iç büküm çizgisi
const bendingInsideRadius = (bending_radius - profile_thickness - ball_radius) * scale;

ctx.strokeStyle = 'blue';
ctx.lineWidth = 3;
ctx.setLineDash([]);
ctx.beginPath();
ctx.arc(radiusInsideX, radiusInsideY, bendingInsideRadius * 2, 0, Math.PI * 2);
ctx.stroke();
```

---

## 🎨 **Canvas Çizim Stil Kontrolleri**

### **Temel Stil Parametreleri:**

```javascript
// Çizgi rengi
ctx.strokeStyle = 'blue';    // Mavi profil çizgileri
ctx.strokeStyle = 'black';   // Siyah merkez çizgileri
ctx.strokeStyle = 'red';     // Kırmızı vals topları
ctx.strokeStyle = '#d9534f'; // Kırmızı (HEX formatında)
ctx.strokeStyle = '#aaa';    // Gri yardımcı çizgiler

// Çizgi kalınlığı
ctx.lineWidth = 1;  // İnce çizgi (iç profil, yardımcı çizgiler)
ctx.lineWidth = 2;  // Orta kalınlık (merkez çizgileri)
ctx.lineWidth = 3;  // Kalın çizgi (dış profil, vals topları)

// Çizgi deseni
ctx.setLineDash([]);          // Kesintisiz (normal)
ctx.setLineDash([5, 3]);      // Kısa kesikli
ctx.setLineDash([5, 5]);      // Eşit kesikli
ctx.setLineDash([20, 5, 8, 5]); // Karışık desen
```

### **Dolgu ve Şeffaflık:**

```javascript
// Dolgu rengi ve şeffaflık
ctx.fillStyle = 'rgba(255, 0, 0, 0.3)'; // %30 şeffaf kırmızı
ctx.fillStyle = 'rgba(0, 0, 255, 0.2)'; // %20 şeffaf mavi
ctx.fillStyle = '#f8f9fa';              // Arka plan rengi

// Vals topları için şeffaf dolgu
ctx.strokeStyle = 'red';
ctx.fillStyle = 'rgba(255, 0, 0, 0.3)';
ctx.lineWidth = 3;
ctx.beginPath();
ctx.arc(x, y, radius, 0, 2 * Math.PI);
ctx.stroke(); // Çevre çizgisi
ctx.fill();   // Şeffaf dolgu
```

---

## 📐 **Koordinat Sistemi ve Ölçeklendirme**

### **Y Koordinat Dönüşümü:**

```javascript
// Canvas Y koordinatını matematiksel Y koordinatına çevir
transform_y_for_canvas(y_mm) {
    return this.canvas.height - (y_mm * this.scale + this.margin);
}

// Alternatif dönüşüm metodu (backup sistemde)
function Y_CordinateTransformationReflection(y, panel) {
    return panel.height - y;
}
```

**Neden Gerekli:**
- 🔄 **Canvas**: Y ekseni yukarıdan aşağıya (0 üstte)
- 📊 **Matematik**: Y ekseni aşağıdan yukarıya (0 altta)
- 🔧 **Makine**: Fiziksel koordinatlar aşağıdan yukarıya

### **Ölçeklendirme Hesaplaması:**

```javascript
// Otomatik ölçeklendirme
this.scale = Math.min(
    (this.canvas.width - 2 * this.margin) / total_width_mm,
    (this.canvas.height - 2 * this.margin) / total_height_mm
);

// Manuel ölçeklendirme (backup sistemde)
const scale = Math.min(
    (canvas.width - 100) / (triangleHeight * 2),
    (canvas.height - 100) / (triangleHeight * 2)
);
```

### **Merkez Koordinat Hesaplama:**

```javascript
// Canvas merkez noktası
const centerX = canvas.width / 2;
const centerY = canvas.height / 2;

// Offset değerleri
const offsetX = canvas.width / 2;
const offsetY = canvas.height / 2;
const pnlEdgeOffset = 40; // Kenar boşluğu
```

---

## 🔧 **Çizim Sekansı ve Optimizasyon**

### **Doğru Çizim Sırası:**

```javascript
function visualize(parameters, result) {
    // 1. Canvas'ı temizle
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

    // 2. Arka plan
    this.ctx.fillStyle = '#f8f9fa';
    this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);

    // 3. Koordinat eksenleri (opsiyonel)
    this.drawAxes();

    // 4. Yardımcı çizgiler (kesikli)
    this.drawTriangle(center_x_px, result);

    // 5. Vals topları (kırmızı dolgulu)
    this.drawBalls(parameters, result);

    // 6. Ana profil çizgileri
    this.drawOuterProfile(parameters, result); // Dış profil (mavi, kalın)
    this.drawInnerProfile(parameters, result); // İç profil (mavi, ince)

    // 7. Merkez referans (kesikli, en üstte)
    this.drawCenterArc(parameters, result); // Merkez yay (siyah, kesikli)

    // 8. Bilgi paneli
    this.drawInfoPanel(result, radii);
}
```

### **Context State Yönetimi:**

```javascript
// Context durumunu kaydet/geri yükle
function drawWithState(drawFunction) {
    ctx.save();    // Mevcut durumu kaydet
    try {
        drawFunction();
    } finally {
        ctx.restore(); // Önceki duruma dön
    }
}

// Kullanım örneği
drawWithState(() => {
    ctx.strokeStyle = 'red';
    ctx.lineWidth = 5;
    ctx.setLineDash([10, 5]);
    // ... çizim işlemleri
}); // Otomatik olarak önceki duruma döner
```

### **Path Optimizasyonu:**

```javascript
// ❌ Verimsiz - her çizgi için ayrı path
ctx.beginPath();
ctx.moveTo(x1, y1);
ctx.lineTo(x2, y2);
ctx.stroke();

ctx.beginPath();
ctx.moveTo(x3, y3);
ctx.lineTo(x4, y4);
ctx.stroke();

// ✅ Verimli - tek path'te birden fazla çizgi
ctx.beginPath();
ctx.moveTo(x1, y1);
ctx.lineTo(x2, y2);
ctx.moveTo(x3, y3);
ctx.lineTo(x4, y4);
ctx.stroke(); // Tek seferde çiz
```

---

## 🎯 **Profil Türlerine Göre Çizim Stratejileri**

### **1. C Profil Çizimi:**

```javascript
function drawCProfile(center, bending_radius, ball_radius, profile_thickness, scale) {
    // C profili için özel hesaplamalar
    const c_profile_outer_radius = (bending_radius - ball_radius) * scale;
    const c_profile_inner_radius = (bending_radius - ball_radius - profile_thickness) * scale;
    
    // Dış kenar (kalın mavi)
    ctx.strokeStyle = 'blue';
    ctx.lineWidth = 3;
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.arc(center.x, center.y, c_profile_outer_radius, 0, 2 * Math.PI);
    ctx.stroke();

    // İç kenar (ince mavi)  
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(center.x, center.y, c_profile_inner_radius, 0, 2 * Math.PI);
    ctx.stroke();
}
```

### **2. L Profil Çizimi:**

```javascript
function drawLProfile(points, profile_thickness, scale) {
    ctx.beginPath();
    ctx.strokeStyle = 'blue';
    ctx.lineWidth = 2;
    ctx.setLineDash([]);

    // Dış kenar çizgileri
    ctx.moveTo(points.outer[0].x * scale, points.outer[0].y * scale);
    points.outer.forEach(point => {
        ctx.lineTo(point.x * scale, point.y * scale);
    });

    // İç kenar çizgileri
    ctx.moveTo(points.inner[0].x * scale, points.inner[0].y * scale);
    points.inner.forEach(point => {
        ctx.lineTo(point.x * scale, point.y * scale);
    });
    
    ctx.stroke();
}
```

### **3. Kutu Profil Çizimi:**

```javascript
function drawBoxProfile(center, width, height, thickness, scale) {
    const w = width * scale;
    const h = height * scale;
    const t = thickness * scale;
    
    ctx.strokeStyle = 'blue';
    ctx.lineWidth = 2;
    ctx.setLineDash([]);
    
    // Dış dikdörtgen
    ctx.strokeRect(center.x - w/2, center.y - h/2, w, h);
    
    // İç dikdörtgen
    ctx.strokeRect(center.x - (w-2*t)/2, center.y - (h-2*t)/2, w-2*t, h-2*t);
}
```

---

## 🔍 **Debug ve Görselleştirme Araçları**

### **Koordinat Noktalarını İşaretleme:**

```javascript
// Merkez noktaları göster
function drawCenterPoint(x, y, color = 'red', size = 3) {
    ctx.save();
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(x, y, size, 0, 2 * Math.PI);
    ctx.fill();
    ctx.restore();
}

// Çarpı işareti çiz
function drawCross(x, y, size = 8, color = '#aaa') {
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(x - size, y);
    ctx.lineTo(x + size, y);
    ctx.moveTo(x, y - size);
    ctx.lineTo(x, y + size);
    ctx.stroke();
    ctx.restore();
}
```

### **Koordinat Eksenleri:**

```javascript
// Merkez eksenleri çiz
function drawAxes() {
    ctx.save();
    ctx.strokeStyle = '#ddd';
    ctx.lineWidth = 1;
    ctx.setLineDash([2, 2]);
    
    // X ekseni (yatay)
    ctx.beginPath();
    ctx.moveTo(0, canvas.height / 2);
    ctx.lineTo(canvas.width, canvas.height / 2);
    ctx.stroke();
    
    // Y ekseni (dikey)
    ctx.beginPath();
    ctx.moveTo(canvas.width / 2, 0);
    ctx.lineTo(canvas.width / 2, canvas.height);
    ctx.stroke();
    
    // Merkez nokta
    drawCenterPoint(canvas.width / 2, canvas.height / 2, '#999', 5);
    
    ctx.restore();
}
```

### **Bilgi Paneli:**

```javascript
// Hesaplama sonuçlarını göster
function drawInfoPanel(result, radii) {
    const ctx = this.ctx;
    const margin = 10;
    const lineHeight = 20;
    let y = margin + lineHeight;

    ctx.save();
    ctx.font = '12px Arial';
    ctx.fillStyle = 'black';
    ctx.textAlign = 'left';

    // Değerleri formatla
    const formatNumber = (num) => typeof num === 'number' ? num.toFixed(2) : '0.00';

    // Bilgileri yaz
    const info = [
        `Efektif Büküm Yarıçapı: ${formatNumber(radii.effectiveRadius)} mm`,
        `Dış Profil Yarıçapı: ${formatNumber(radii.outerRadius)} mm`,
        `İç Profil Yarıçapı: ${formatNumber(radii.innerRadius)} mm`,
        `Yan Top Mesafesi: ${formatNumber(result.LeftPistonPosition)} mm`,
        `Yan Top X Mesafesi: ${formatNumber(Math.abs(result.LeftBallPosition.X))} mm`,
        `Yan Top Y Mesafesi: ${formatNumber(result.LeftBallPosition.Y)} mm`,
        `Üst Top Mesafesi: ${formatNumber(radii.topBallY)} mm`,
        `Profil Yüksekliği: ${formatNumber(radii.profileHeight)} mm`
    ];

    info.forEach(text => {
        ctx.fillText(text, margin, y);
        y += lineHeight;
    });

    ctx.restore();
}
```

---

## 🎨 **Gelişmiş Çizim Teknikleri**

### **Gradient Dolgu:**

```javascript
// Radyal gradient (vals topları için)
function createRadialGradient(x, y, radius) {
    const gradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
    gradient.addColorStop(0, 'rgba(255, 255, 255, 0.8)');
    gradient.addColorStop(0.7, 'rgba(255, 0, 0, 0.6)');
    gradient.addColorStop(1, 'rgba(200, 0, 0, 0.8)');
    return gradient;
}

// Kullanım
ctx.fillStyle = createRadialGradient(ballX, ballY, ballRadius);
ctx.fill();
```

### **Gölge Efekti:**

```javascript
// Gölge efekti ekle
function drawWithShadow(drawFunction, shadowColor = 'rgba(0,0,0,0.3)') {
    ctx.save();
    ctx.shadowColor = shadowColor;
    ctx.shadowBlur = 5;
    ctx.shadowOffsetX = 2;
    ctx.shadowOffsetY = 2;
    
    drawFunction();
    
    ctx.restore();
}
```

### **Animasyon Desteği:**

```javascript
// Büküm animasyonu
function animateBending(startAngle, endAngle, duration = 2000) {
    const startTime = Date.now();
    
    function animate() {
        const elapsed = Date.now() - startTime;
        const progress = Math.min(elapsed / duration, 1);
        
        // Easing function (smooth animation)
        const easeProgress = 1 - Math.pow(1 - progress, 3);
        
        const currentAngle = startAngle + (endAngle - startAngle) * easeProgress;
        
        // Canvas'ı temizle ve yeniden çiz
        clearCanvas();
        drawBendingAtAngle(currentAngle);
        
        if (progress < 1) {
            requestAnimationFrame(animate);
        }
    }
    
    animate();
}
```

---

## ⚠️ **Yaygın Hatalar ve Çözümleri**

### **1. Çizgi Deseni Sıfırlanmaması:**

```javascript
// ❌ Yanlış - desen sıfırlanmıyor
ctx.setLineDash([5, 3]);
drawDashedLine();
drawSolidLine(); // Hala kesikli çıkar!

// ✅ Doğru - desen sıfırlanıyor
ctx.setLineDash([5, 3]);
drawDashedLine();
ctx.setLineDash([]); // Mutlaka sıfırla
drawSolidLine();
```

### **2. Koordinat Sistemi Karışıklığı:**

```javascript
// ❌ Yanlış - Y koordinatı ters
ctx.arc(x, y_mm * scale, radius, 0, 2 * Math.PI);

// ✅ Doğru - Y koordinatı dönüştürülmüş
ctx.arc(x, transform_y_for_canvas(y_mm), radius, 0, 2 * Math.PI);
```

### **3. Ölçeklendirme Hataları:**

```javascript
// ❌ Yanlış - ölçek unutulmuş
ctx.arc(center_x, center_y, radius_mm, 0, 2 * Math.PI);

// ✅ Doğru - ölçek uygulanmış
ctx.arc(center_x, center_y, radius_mm * scale, 0, 2 * Math.PI);
```

### **4. Context State Unutulması:**

```javascript
// ❌ Yanlış - stil değişiklikleri kalıcı
function drawSpecialLine() {
    ctx.strokeStyle = 'red';
    ctx.lineWidth = 10;
    ctx.setLineDash([20, 10]);
    // ... çizim
    // Önceki duruma döndürme yok!
}

// ✅ Doğru - save/restore kullanımı
function drawSpecialLine() {
    ctx.save();
    ctx.strokeStyle = 'red';
    ctx.lineWidth = 10;
    ctx.setLineDash([20, 10]);
    // ... çizim
    ctx.restore(); // Otomatik olarak önceki duruma döner
}
```

### **5. Performance Sorunları:**

```javascript
// ❌ Yanlış - her frame'de yeniden hesaplama
function drawFrame() {
    const expensiveCalculation = heavyMathFunction(); // Her seferinde hesapla
    drawWithResult(expensiveCalculation);
}

// ✅ Doğru - cache kullanımı
let cachedResult = null;
function drawFrame() {
    if (!cachedResult) {
        cachedResult = heavyMathFunction(); // Sadece bir kez hesapla
    }
    drawWithResult(cachedResult);
}
```

---

## 📊 **Performans Optimizasyon İpuçları**

### **1. Canvas Boyutlandırma:**

```javascript
// High-DPI ekranlar için optimizasyon
function setupHighDPICanvas(canvas) {
    const ctx = canvas.getContext('2d');
    const devicePixelRatio = window.devicePixelRatio || 1;
    
    // Canvas fiziksel boyutunu ayarla
    const displayWidth = canvas.clientWidth;
    const displayHeight = canvas.clientHeight;
    
    canvas.width = displayWidth * devicePixelRatio;
    canvas.height = displayHeight * devicePixelRatio;
    
    // CSS boyutunu koru
    canvas.style.width = displayWidth + 'px';
    canvas.style.height = displayHeight + 'px';
    
    // Context'i ölçeklendir
    ctx.scale(devicePixelRatio, devicePixelRatio);
    
    return ctx;
}
```

### **2. Çizim Batching:**

```javascript
// Aynı stildekileri grupla
function drawMultipleCircles(circles) {
    // Kırmızı daireler
    ctx.strokeStyle = 'red';
    ctx.lineWidth = 2;
    ctx.beginPath();
    circles.filter(c => c.color === 'red').forEach(circle => {
        ctx.moveTo(circle.x + circle.radius, circle.y);
        ctx.arc(circle.x, circle.y, circle.radius, 0, 2 * Math.PI);
    });
    ctx.stroke();
    
    // Mavi daireler
    ctx.strokeStyle = 'blue';
    ctx.beginPath();
    circles.filter(c => c.color === 'blue').forEach(circle => {
        ctx.moveTo(circle.x + circle.radius, circle.y);
        ctx.arc(circle.x, circle.y, circle.radius, 0, 2 * Math.PI);
    });
    ctx.stroke();
}
```

Bu Canvas çizim sistemi, büküm makinesinin karmaşık geometrik hesaplamalarını görsel olarak anlaşılır hale getirerek operatörlerin işlerini kolaylaştırır! 🚀

---

## 📚 **Kaynaklar ve Daha Fazla Bilgi**

- [HTML5 Canvas API Dokümantasyonu](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API)
- [Canvas Optimizasyon Teknikleri](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API/Tutorial/Optimizing_canvas)
- [2D Grafik Matematik Temelleri](https://en.wikipedia.org/wiki/2D_computer_graphics) 