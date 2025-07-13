# MAKİNE CALİSMA PRENSİBİ
## Monitoring (Veri İzleme Sayfası) - UI
Her 100ms'de bir Socket programlama vasıtası ile verileri güncelleyip, kullanıcıya gösteren sayfa.
Sayfada;
1. Bağlantı Durumu; (Web API Durumu, Makine Durumu, Son Güncelleme)
2. Genel Durum; (Çalışma Modu, Hidrolik Motor Durumu, Fan Motor Durumu, Alarm Durumu) 
3. Sensör durumları; (Acil Stop,Faz Hatası, Sol Rot. Sensör, Sağ Rot. Sensör, Hidrolik Termal, Fan Termal, Sol Parça, Sağ Parça, Rotasyon Durumu, Rotasyon Hızı, Kirlilik Sensörleri(3 adet))
4. Yağ Sensörleri; (S1 Basınç: - bar, S2 Basınç: - bar, S1 Akış: - cm/sn, S2 Akış: - cm/sn, Yağ Sıcaklık: - °C, Yağ Nem: - %, Yağ Seviyesi: - %)
5. Ana pistonlar; (Üst Piston, Alt Piston, Sol Piston, Sağ Piston = Voltaj Değeri, Pozisyon(mm)) 
6. Sağ ve Sol Yan Dayamalar; (Makara, Gövde, Join = Durum(İleri,Geri), Pozisyon(mm))

## MANUEL KONTROL SAYFASI - UI
Ana pistonları, yan dayama pistonlarını, hidrolik motoru, fan, alarm, pnömatik valfleri kontrol edebildiğimiz sayfa.
Pistonlara hareket vermek için; Önce pistonun bağlı olduğu valf açılır, sonrasında pistonun adresine -10V ve +10V arasında değer gönderilerek (Gönderilen değer modbus altyapısına uygun dönüştürülmüş olacak) hareket ettirilir.
Durdurmak içinse; Önce 0V (kademeli yavaşlamaya uygun olarak en son 0V gönderilir) gönderilir, sonrasında bağlı olduğu valf kapatılır.
Ana pistonlar; 2 şekilde kontrol edilebilir:
1. Butona basılı olduğu sürece hareket (jogging): Kullanıcı butona basılı olduğu sürece pistonu ileri/geri hareket ettirebildiği seçenek. Butondan el çekildiğinde aniden durma yerine kademeli yavaşlayarak makineyi fiziksel olarak koruyabiliriz.
2. Kullanıcı göndermek istediği pozisyonu input alanına girip, butonuna tıklayarak tek butonla pistonu istediği pozisyona gönderdiği seçenek. Durdur butonu olmalı.
    Bu seçenekte; pozisyon bazlı bir gönderme olduğu için, anlık olarak pistona bağlı cetvel (lineer potansiyometre) kontrol edilir, istenen pozisyona yaklaşırken kademeli yavaşlama uygulanarak, hassas konumlandırma yapılır.
3. Ana pistonlar için; 1 adet + Volt için slider, 1 adet - Volt slider (-10V, +10V) olacak. Piston için hareket isteği yapılırken, hız için voltaj değeri sliderlardan alınacak
4. Rotasyon hareketi için; 1 adet + Volt için slider (%10 - %100 yani 1V - 10V) olacaktır. Rotasyon için hareket isteği yapılırken, hız için voltaj değeri sliderlardan alınacak. Slider arttırılırken 10'ar artacak.
5. Pistonlar 0 pozisyonlarından geri hareket ettirilmemeli. Çünkü makinenin fiziksel yapısından dolayı, biz pistonları sıfır pozisyonuna getiriyoruz ve pistonlara bağlı cetvelleri sıfırlıyoruz. Bu sebeple pistonların 0mm pozisyonundayken geri gitmemesi mümkün olmamalı.
6. Pistonlar ileri hareket için - Volt verilecek, geri hareket için + Volt verilecek.

## Otomatik Büküm Sayfası
Bu sayfada kullanıcı tarafından girilen parametrelerle büküm hesabı yapılır ve Topların büküm için pozisyonları vs. belirlenir. Canvas çizimi ile de top pozisyonları, parça konumu vs. canvas çizimi ile çizilir.
1. Büküm için parametreler alanı ; Top çapları(4 adet vals topunun çapı), Profil boyutları(Yükseklik, uzunluk, et kalınlığı), Geometri parametreleri(Üçgen genişliği, Üçgen açısı), Kademe ayarları (0, 60, 120mm),
Adım büyüklüğü(20mm), Hedef Basınç(50 bar), Basınç Toleransı (+- 5 bar).

### 2. Parça sıkıştırma alanı
*Bu alan makinenin bu özelliğini test etmek içindir. Bu özellik otomatik büküm prosesinde kullanılacaktır.
Büküm hesabı yapıldıktan sonra, parça sıkıştırma işlemi yapılabilir. Sıkıştırma hedef basınç inputuna basınç girilir. Parça sıkıştırma butonuna tıklanır. Bu işlevin çalışması için Sol parça varlık sensörünün parçayı görüyor olması gerekir.
Parça sıkıştırma şöyledir, butona tıklandığında; Üst top parçayı sıkıştırmak için ya büküm hesabındaki üst top mesafesine gitmesi gerekiyor ya da hedef basınca ulaşması gerekecek. İkisinden biri gerçekleştiğinde sıkıştırma olmuş demektir. Şöyle bir husus var: Üst pistonun bir kalkış basıncı olduğu için, 500-600ms bir görmezden gelme uygulanacak. Çünkü kalkış basıncı bizim sıkıştırma basınıcımıza ulaşmışız gibi görünebilir.

### 3. Parça Sıfırlama alanı
*Bu alan makinenin bu özelliğini test etmek içindir. Bu özellik otomatik büküm prosesinde kullanılacaktır.
Parça sıkıştırıldıktan sonra, parça sıfırlama yapılır. Parça sıfırlama şöyledir;
Parça sıfırlama mesafesi inputu olacak. Kullanıcı bir sıfırlama mesafesi girecek. Bu mesafe parça varlık sensöründen, alt orta top merkezine olan mesafeyi ifade eder.
Adımlar;
1. Başlangıçta Sol parça varlık sensörü görüyorsa;
1.1 Önce pistonlar Saat yönünde rotasyon ile normal hızda hareket ettirilir ve sensör görmeyene kadar devam eder, sensör görmediği anda durur.
1.2 Sonrasında çok yavaş şekilde, saat yönünün tersine rotasyon ile hareket ettirilir, sensör gördüğü anda durur.

2. Başlangıçta Sol parça varlık sensörü görmüyorsa;
2.1 Önce pistonlar Saat yönünün tersine rotasyon ile normal hızda hareket ettirilir ve sensör görene kadar devam eder, sensör gördüğü anda durur.
2.1 Pistonlar Saat yönünde rotasyon ile normal hızda hareket ettirilir ve sensör görmeyene kadar devam eder, sensör görmediği anda durur.
2.2 Sonrasında çok yavaş şekilde, saat yönünün tersine rotasyon ile hareket ettirilir, sensör gördüğü anda durur.

3. Parçanın alt top merkezine çekilmesi için girilen sıfırlama mesafesi kadar saat yönüne rotasyon ile hareket ettirilmesi gerekir. Böylelikle parçanın ucu alt top merkezine sıfırlanmış olur.
Bu alt top merkezine çekilmesi için hesaplama yapılması gerekir.
Örnek hesaplama :
public static double PulseToDistanceConvert(double register, double ballDiameter)
{
    double perimeterDistance = ballDiameter * Math.PI; //Top çevre uzunluğu
    double mm = register * perimeterDistance / pulseCount;

    return Math.Round(mm, 2);
}
public static int DistanceToPulseConvert(double mm, double ballDiameter)
{
    double perimeterDistance = ballDiameter * Math.PI; //Top çevre uzunluğu
    int register = Convert.ToInt32(Math.Round(mm * pulseCount / perimeterDistance));

    return register;
}

Eğer sağ parça varlık sensörü görüyorsa da yön olarak ters olacak şekilde aynı işlemler yapılacak.


### CETVEL SIFIRLAMA  ALANI
*Bu alan makinenin bu özelliğini test etmek içindir. Bu özellik otomatik büküm prosesinde kullanılacaktır.
Sıfırlama işlemi için bir adet Cetvel Sıfırlama butonu olacak.
Butona tıklandığında; Adres dosyasında bulunan Cetvel sıfırlama adreslerindeki değerler(Üst ve alt orta pistonlar, yan pistonlar, rotasyon, pnömatik) kontrol edilecek. Eğer okunan 4 değerden herhangi birinin ham değeri 2570 değil ise Cetvel sıfırlamaya uygun demektir. Eğer 4 değer de 2570 ise sıfırlama zaten yapılmıştır, sıfırlamaya gerek yok demektir.
Sıfırlama işlemi; Tüm pistonlar geri yönde 
Makinenin fiziksel yapısından dolayı pistonlar en altta olduğunda gönyede durmuyor. Bu sebeple sıfırlama işlemi yaparak, alt pistonları gönyeye getiriyoruz.
Sıfırlama işlemi: 
1. Tüm pistonlar aynı anda geri çekilir. Pnömatik valfler kapatılır. Ana vals topları ulaşana kadar geri çekilmeye devam edilir(vals toplarının en geri dayandığından emin olmak için)
2. Cetvel sıfırlama işlemi yapılır.
3. Alt pistonlar istenilen pozisyonlara getirilir (Ayarlar sayfasında pozisyonlar belirlenir).
4. Cetveller tekrar sıfırlanır ve Cetvel sıfırlama işlemi bitirilir.

Cetveller nasıl sıfırlanır:
1. Reset adeslerine -32640 değeri gönderilir.
2. 200ms beklenir.
3. Reset adeslerine 2570 değeri gönderilir
4. 200ms beklenir.
5. Reset adresleri kontrol edilir, 2570 değeri görülüyorsa Cetvel resetleme başarılı ile tamamlanmıştır.

### Stageler Alanı
*Bu alan makinenin bu özelliğini test etmek içindir. Bu özellik otomatik büküm prosesinde kullanılacaktır.
Stageler, makinenin farklı boyutlarda büküm yapılabilmesi için yaptığımız bir yenilik. Kullanıcı yapmak istediği büküme göre makineyi istediği stage alarak büküm yapabilir.
Cetvel sıfırlama gibi çalışır. Sadece farklı yükseklikte sıfırlama yapmak için olan bir yenilik.
Stageler de Ayarlar sayfasından ayarlanabilir (stage sayısı ve değerleri). Default olarak 3 stage olsun ve değerleri de 0mm, 60mm, 120mm olsun.
İstenen stage butonuna tıklandığında;
1. Cetvel sıfırlama işlemi yapılır (0mm değerine, yani Ayarlar sayfasında belirlenen pozisyonlar).
2. Seçilen Stage kademesine kadar toplar yukarı kaldırılır. (Ayarlar sayfasından Stage ayarlında belirlenir). Hassas konumlandırma yapılarak kaldırılır yani ilerletilir.
Default olarak;
60 Değeri için; Alt sağ ve sol top: 67.34, orta alt top:60mm
120 Değeri için; Alt sağ ve sol top: 134.68, orta alt top:120mm
3. Cetvel değerleri sıfırlanır

###  OTOMATİK BÜKÜM ALANI
Otomatik büküm prosesi; parça sıkıştırma, parça sıfırlama, stage ayarlama gibi işlemleri içerir.
1. Stage ayarlanır.
1. Büküm hesabı yapılır. Sonuçlar elde edilir.
2. Parça sıkıştırma işlemi yapılır.
3. Parça sıfırlama yapılır.
4. 
4.1 Adım büyüklüğü, sağ ve sol topun büküm için her pasoda gideceği mesafeyi belirtir.
4.2 Adım mesafesine göre bükümün toplamda kaç pasoda bitirileceği hesaplanır.
Devamında büküm işlemi başlar;
-Parça hangi yöne doğru sıfırlandıysa (Sol parça varlık için saat yönü, sağ parça varlık için saat yönü tersi), ters yönünde parça boyu kadar rotasyon ile %80 hızla hareket ettirilir. Bu açıklama için sol parça varlık referans alınmıştır.!
- Sağ vals topu adım mesafesi kadar ilerletilir. Bu esnada eğer müsaitse (gidebileceği mesafe varsa) sol vals topu 20mm geri hareket eder.
- Sağ vals topun adımı tamamladığında parça rotasyon ile sol vals topuna doğru sürülür, bu esnada sol vals topu adım mesafesine hareket eder. Sol vals topu adımı tamamlarken sağ vals topu 20mm aşağı iner(mesafe varsa).
- Sağ ve sol vals topları belirlenen konuma ulaşıp bükümü tamamlayana kadar bu proses devam eder.
- Şöyle dikkat edilmesi gereken bir konu var: Topların pozisyonları iyi hesaplanmalı. Örneğin Sağ vals topu 40mm'deyken, sol vals topu büküm yaparken 20mm aşağı indiğinde, paso sağ vals topuna geldiğinde gideceği konum tekrar 40mm değil 60mm olacak. Bunlar önemli işlemler
- Sağ ve sol vals topunda son kalan pasoda ne kadar mesafe gideceği hesaplanmalı. Örneğin Gitmeleri gereken konum 110mm olarak hesaplandı. 20 - 40 - 60 - 80 - 100. Son pasoda gitmesi gereken mesafe 10mm. Bunu iyi hesaplamalı.
- Büküm bittiğinde kullanıcı bilgilendirilmeli. Parça tahliye için kullanıcıdan bir süre seçmesi beklenmeli. 5sn, 10sn, 25sn, 30sn gibi. Kullanıcı seçimi yapacak ve tahliye başlat butonuna tıklayacak.
    Tahliye başladığında, tüm pistonlar en geri çekilecek, belirlenen süre kadar bekleyecek ve süre bittiğinde son seçilen stage'e sıfırlanacak.
- Büküm sırasında ani basınç değişiklikleri kontrol edilmeli ve büküm durdurulmalı (parça kırılmış veya deforme olmuş olabilir).
- Büküm durdurulabilmeli, Devam ettirilebilmeli, iptal edilebilmeli. (iptal tahliye süreci olacak, tekrar büküm yap butonuna tıklanırsa seçilen stage göre sıfırlanacak)

## AYARLAR SAYFASI
Makine ile ilgili bilgilerin yer aldığı, güncellemeler yapılabilen alan.
Makinenin parametreleri bu alandan düzenlenebilir (ileride yeni alanlar eklenecek, Garanti süresi vs. gibi)
Örneğin; vals topu çapları, üçgen genişliği, üçgen açısı, kademe sayısı ve değerleri, sıkıştırma basıncı ve toleransı, max bar (genel olarak güvenlik için olan max basınç), profil reset mesafesi(her stage ayrı girilecek). 