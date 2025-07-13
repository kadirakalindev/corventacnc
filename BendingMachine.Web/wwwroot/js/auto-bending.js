// Auto Bending JavaScript - Corventa Profil Büküm Simülasyonu

class AutoBendingController {
    constructor() {
        this.apiBaseUrl = 'http://localhost:5002';
        this.connection = null;
        this.calculationResult = null;
        this.bendingVisualizer = null;
        this.isConnected = false;
        this.isConnecting = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        
        // Timeout ve retry ayarları
        this.requestTimeout = 30000; // 30 saniye
        this.maxRetries = 3;
        this.retryDelay = 1000; // 1 saniye
        
        this.init();
    }
    
    async init() {
        try {
        console.log('AutoBendingController başlatılıyor...');
        
        // Canvas'ı hazırla
        this.initCanvas();
        
        // Event listener'ları ekle
        this.addEventListeners();
        
        // SignalR bağlantısını başlat
            await this.initSignalR();
        
        // Makine bağlantı durumunu kontrol et
        this.checkMachineConnection();
        
        // Cetvel durumunu kontrol et
        this.checkRulerStatus();
        
        // İlk görselleştirme
        this.renderEmptyCanvas();
        } catch (error) {
            console.error('AutoBendingController başlatılırken hata:', error);
            this.showError('Bağlantı başlatılamadı: ' + error.message);
        }
    }
    
    initCanvas() {
        const canvas = document.getElementById('bendingCanvas');
        if (!canvas) {
            console.error('Canvas elementi bulunamadı!');
            return;
        }
        
        // Canvas boyutlarını ayarla
        canvas.width = 500;
        canvas.height = 400;
        
        // BendingVisualizer sınıfını başlat
        this.bendingVisualizer = new BendingVisualizer('bendingCanvas');
    }
    
    addEventListeners() {
        // Form elemanlarına değişiklik listener'ları ekle
        const inputs = document.querySelectorAll('input[type="number"]');
        inputs.forEach(input => {
            input.addEventListener('change', () => {
                this.updateVisualizationLog(`${input.previousElementSibling.textContent} değiştirildi: ${input.value}`);
            });
        });
    }
    
    async initSignalR() {
        if (this.isConnecting) {
            console.log('Zaten bağlantı kurulmaya çalışılıyor...');
                return;
            }

        try {
            this.isConnecting = true;
            console.log('SignalR bağlantısı başlatılıyor...');

            // API'nin doğru adresi ve endpoint'i
            const signalRUrl = `${this.apiBaseUrl}/machinestatus`;
            
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(signalRUrl)
                .withAutomaticReconnect([0, 2000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();
            
            // Bağlantı durumu değişikliklerini dinle
            this.connection.onreconnecting((error) => {
                console.warn('SignalR bağlantısı yeniden kuruluyor...', error);
                this.updateVisualizationLog('⚠️ Makine bağlantısı yeniden kuruluyor...');
                this.updateConnectionStatus(false);
                this.isConnected = false;
            });

            this.connection.onreconnected((connectionId) => {
                console.log('SignalR bağlantısı yeniden kuruldu:', connectionId);
                this.updateVisualizationLog('✅ Makine bağlantısı yeniden kuruldu');
                this.updateConnectionStatus(true);
                this.isConnected = true;
                this.reconnectAttempts = 0;
            });

            this.connection.onclose((error) => {
                console.error('SignalR bağlantısı kapandı:', error);
                this.updateVisualizationLog('❌ Makine bağlantısı kesildi');
                this.updateConnectionStatus(false);
                this.isConnected = false;
                
                // Yeniden bağlanma denemesi
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    this.reconnectAttempts++;
                    setTimeout(() => this.initSignalR(), 5000);
                } else {
                    this.showError('Maksimum yeniden bağlanma denemesi aşıldı. Sayfayı yenileyin.');
                }
            });

            // Event handlers - Büyük/küçük harf duyarlı
            this.connection.on("MachineStatusUpdate", (status) => {
                this.updateVisualizationLog('📊 Makine durumu güncellendi');
                this.handleMachineStatus(status);
            });
            
            this.connection.on("PistonStatusUpdate", (pistons) => {
                this.updateVisualizationLog('⚙️ Piston durumu güncellendi');
                this.updatePistonStatus(pistons);
            });

            this.connection.on("PistonMoved", (data) => {
                this.updateVisualizationLog(`⚙️ ${data.PistonType} hareket etti: ${data.Motion}`);
            });

            this.connection.on("AlarmRaised", (data) => {
                this.handleAlarmEvent(data);
            });
            
            this.connection.on("SafetyViolation", (data) => {
                this.handleSafetyViolation(data);
            });
            
            this.connection.on("Error", (message) => {
                console.error('SignalR hatası:', message);
                this.showError(message);
            });

            this.connection.on("SystemMessage", (data) => {
                this.updateVisualizationLog(`💬 ${data.Message} (${data.Level})`);
            });

            this.connection.on("EncoderStatusUpdate", (data) => {
                this.handleEncoderStatus(data);
            });

            this.connection.on("RealTimePressureUpdate", (data) => {
                this.handlePressureUpdate(data);
            });
            
            this.connection.on("BendingProgressUpdate", (data) => {
                this.updateVisualizationLog(`📊 Büküm İlerlemesi: ${data.Progress}% - ${data.CurrentOperation}`);
                this.handleAutoBendingStatus({
                    bendingProgress: data.Progress,
                    currentOperation: data.CurrentOperation,
                    isAutoBendingActive: true
                });
            });

            // Bağlantıyı başlat
            await this.connection.start();
            console.log('SignalR bağlantısı başarıyla kuruldu');
            this.updateVisualizationLog('✅ Makine bağlantısı kuruldu');
            this.updateConnectionStatus(true);
            this.isConnected = true;
            this.reconnectAttempts = 0;

            // MachineUsers grubuna katıl
            await this.connection.invoke("JoinMachineUsersGroup");
            console.log('MachineUsers grubuna katıldı');
            
        } catch (error) {
            console.error('SignalR bağlantı hatası:', error);
            this.showError('Bağlantı hatası: ' + error.message);
            this.updateConnectionStatus(false);
            this.isConnected = false;
            
            // Yeniden bağlanma denemesi
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                this.reconnectAttempts++;
                setTimeout(() => this.initSignalR(), 5000);
            }
        } finally {
            this.isConnecting = false;
        }
    }
    
    handleMachineStatus(status) {
        try {
            // Makine durumunu güncelle
            this.updateMachineStatus(status);

            // Otomatik büküm durumunu kontrol et
            if (status.isAutoBendingActive) {
                this.handleAutoBendingStatus(status);
            }

            // Hata durumlarını kontrol et
            if (status.hasError) {
                this.handleMachineError(status);
    }
    
            // Encoder pozisyonlarını güncelle
            if (status.rotationEncoderRaw !== undefined) {
                const currentDistance = Math.round((status.rotationEncoderRaw * Math.PI * 220.0) / 1024.0, 2);
                const encoderStatus = document.getElementById('encoderStatus');
                if (encoderStatus) {
                    encoderStatus.textContent = `${status.rotationEncoderRaw} pulse (${currentDistance}mm)`;
                    encoderStatus.className = 'badge ' + (status.isEncoderHealthy ? 'bg-success' : 'bg-danger');
                }
            }

            // Basınç değerlerini güncelle
            if (status.s1OilPressure !== undefined && status.s2OilPressure !== undefined) {
                const pressureStatus = document.getElementById('pressureStatus');
                if (pressureStatus) {
                    pressureStatus.textContent = `S1: ${status.s1OilPressure.toFixed(1)} bar, S2: ${status.s2OilPressure.toFixed(1)} bar`;
                    pressureStatus.className = 'badge bg-info';
                }
            }

            // Piston durumlarını güncelle
            this.updatePistonStatuses(status);

            // Hidrolik motor durumunu güncelle
            const motorStatus = document.getElementById('motorStatus');
            if (motorStatus) {
                motorStatus.textContent = status.isHydraulicMotorRunning ? 'ÇALIŞIYOR' : 'DURDU';
                motorStatus.className = 'badge ' + (status.isHydraulicMotorRunning ? 'bg-success' : 'bg-danger');
            }

            // Parça sensör durumunu güncelle
            const partSensorStatus = document.getElementById('partSensorStatus');
            if (partSensorStatus) {
                partSensorStatus.textContent = status.isPartPresent ? 'PARÇA VAR' : 'PARÇA YOK';
                partSensorStatus.className = 'badge ' + (status.isPartPresent ? 'bg-success' : 'bg-warning');
        }

        } catch (error) {
            console.error('Makine durum işleme hatası:', error);
            this.updateVisualizationLog(`❌ Makine durum güncelleme hatası: ${error.message}`);
        }
    }
    
    updateMachineStatus(status) {
        // Bağlantı durumunu güncelle
            this.updateConnectionStatus(status.isConnected);
            
        // Hidrolik motor durumunu güncelle
        const motorStatusText = document.getElementById('motorStatus');
        if (motorStatusText) {
            motorStatusText.textContent = status.isHydraulicMotorRunning ? 'ÇALIŞIYOR' : 'DURDU';
            motorStatusText.className = status.isHydraulicMotorRunning ? 'text-success' : 'text-danger';
            }
            
        // Parça sensör durumunu güncelle
        const partSensorText = document.getElementById('partSensorStatus');
        if (partSensorText) {
            partSensorText.textContent = status.isPartPresent ? 'PARÇA VAR' : 'PARÇA YOK';
            partSensorText.className = status.isPartPresent ? 'text-success' : 'text-warning';
        }
    }
    
    handleAutoBendingStatus(status) {
        // Büküm ilerleme durumunu güncelle
        const progressBar = document.getElementById('bendingProgressBar');
        const statusText = document.getElementById('bendingStatusText');
        const progressPanel = document.getElementById('bendingProgressPanel');

        if (progressPanel) {
            progressPanel.style.display = 'block';
        }

        if (progressBar && status.bendingProgress !== undefined) {
            progressBar.style.width = `${status.bendingProgress}%`;
            progressBar.textContent = `${status.bendingProgress}%`;
        }

        if (statusText && status.currentOperation) {
            statusText.textContent = status.currentOperation;
            }
            
        // Buton durumlarını güncelle
        const startButton = document.getElementById('btnStartBending');
        const stopButton = document.getElementById('btnStopBending');

        if (startButton) {
            startButton.disabled = status.isAutoBendingActive;
        }

        if (stopButton) {
            stopButton.disabled = !status.isAutoBendingActive;
        }
    }
    
    handleMachineError(status) {
        // Hata mesajını göster
        this.showError(status.errorMessage || 'Makine hatası oluştu');
        this.updateVisualizationLog(`❌ Makine hatası: ${status.errorMessage}`);

        // Otomatik büküm aktifse durdur
        if (status.isAutoBendingActive) {
            this.stopAutoBending();
        }
    }
    
    updateEncoderPositions(status) {
        // Encoder pozisyonlarını güncelle
        const encoderPos = document.getElementById('encoderPosition');
        if (encoderPos && status.encoderPosition !== undefined) {
            encoderPos.textContent = `${status.encoderPosition} pulse`;
                }
    }
    
    updatePressureValues(status) {
        // S1/S2 basınç değerlerini güncelle
        const s1Pressure = document.getElementById('s1Pressure');
        const s2Pressure = document.getElementById('s2Pressure');
            
        if (s1Pressure && status.s1Pressure !== undefined) {
            s1Pressure.textContent = `${status.s1Pressure.toFixed(1)} bar`;
        }

        if (s2Pressure && status.s2Pressure !== undefined) {
            s2Pressure.textContent = `${status.s2Pressure.toFixed(1)} bar`;
        }
    }
    
    updateConnectionStatus(isConnected) {
        const connectionStatus = document.getElementById('connectionStatus');
        if (connectionStatus) {
            connectionStatus.textContent = isConnected ? 'BAĞLI' : 'BAĞLI DEĞİL';
            connectionStatus.className = isConnected ? 'text-success' : 'text-danger';
    }
    
        // Bağlantı durumuna göre butonları etkinleştir/devre dışı bırak
        const buttons = document.querySelectorAll('.machine-control-btn');
        buttons.forEach(button => {
            button.disabled = !isConnected;
        });
    }
    
    // Makine bağlantı durumunu kontrol et
    async checkMachineConnection() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/Machine/status`);
            
            if (response.ok) {
            const result = await response.json();
                const isConnected = result.data?.isConnected || false;
                this.updateConnectionStatus(isConnected);
            } else {
                this.updateConnectionStatus(false);
            }
        } catch (error) {
            console.error('Makine durumu kontrol hatası:', error);
            this.updateConnectionStatus(false);
        }
    }
    
    renderEmptyCanvas() {
        const canvas = document.getElementById('bendingCanvas');
        const ctx = canvas.getContext('2d');
        
        // Canvas'ı temizle
        ctx.fillStyle = '#f8f9fa';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        // Başlangıç mesajı
        ctx.fillStyle = '#666';
        ctx.font = '16px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('Parametreleri girin ve hesaplama yapın', canvas.width / 2, canvas.height / 2);
        ctx.fillText('Canvas çizimi burada görünecek', canvas.width / 2, canvas.height / 2 + 30);
    }
    
    updateVisualizationLog(message) {
        const log = document.getElementById('visualizationLog');
        if (log) {
            const timestamp = new Date().toLocaleTimeString();
            log.innerHTML += `<div>[${timestamp}] ${message}</div>`;
            log.scrollTop = log.scrollHeight;
        }
    }
    
    // API isteği için yardımcı metod
    async fetchWithTimeout(url, options = {}) {
        const controller = new AbortController();
        const timeout = options.timeout || this.requestTimeout;
        
        const id = setTimeout(() => controller.abort(), timeout);
        
        try {
            const response = await fetch(url, {
                ...options,
                signal: controller.signal
            });
            clearTimeout(id);
            return response;
        } catch (error) {
            clearTimeout(id);
            if (error.name === 'AbortError') {
                throw new Error('İstek zaman aşımına uğradı');
            }
            throw error;
        }
    }

    // Retry mekanizması ile API isteği
    async fetchWithRetry(url, options = {}) {
        let lastError;
        
        for (let i = 0; i < this.maxRetries; i++) {
            try {
                const response = await this.fetchWithTimeout(url, options);
                
                // HTTP hatası varsa
                if (!response.ok) {
                    const result = await response.json();
                    throw new Error(result.message || `HTTP ${response.status}: ${response.statusText}`);
                }
                
                return response;
            } catch (error) {
                console.warn(`Deneme ${i + 1}/${this.maxRetries} başarısız:`, error);
                lastError = error;
                
                // Son deneme değilse bekle
                if (i < this.maxRetries - 1) {
                    await new Promise(resolve => setTimeout(resolve, this.retryDelay));
                }
            }
        }
        
        throw lastError;
    }
    
    async calculateBending() {
        try {
            this.updateVisualizationLog('Büküm hesaplaması başlatılıyor...');
            
            // Form verilerini topla ve doğrula
            const parameters = this.getFormParameters();
            if (!this.validateParameters(parameters)) {
                return;
            }
            
            console.log('Hesaplama parametreleri:', parameters);
            
            // Hesaplama öncesi UI'ı güncelle
            this.updateCalculationUI(true);
            
            // API'ye istek gönder
            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Bending/calculate-bending`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(parameters)
            });
            
            const result = await response.json();
            console.log('Hesaplama sonucu:', result);
            
            if (result.success) {
                this.calculationResult = result.data;
                this.displayResults(result.data);
                await this.visualizeResults(parameters, result.data);
                this.updateLastCalculation();
                this.updateVisualizationLog('✅ Hesaplama başarıyla tamamlandı');
                
                // Otomatik büküm için hedef pozisyonları güncelle
                this.updateTargetPositions(result.data);
                
                // Hesaplama sonrası UI'ı güncelle
                this.updateCalculationUI(false);
                this.showSuccess('Büküm hesaplaması başarıyla tamamlandı');
            } else {
                throw new Error(result.message || 'Hesaplama başarısız');
            }
            
        } catch (error) {
            console.error('Hesaplama hatası:', error);
            this.showError('Hesaplama hatası: ' + error.message);
            this.updateVisualizationLog(`❌ Hesaplama hatası: ${error.message}`);
            this.updateCalculationUI(false);
        }
    }
    
    validateParameters(parameters) {
        const validationRules = {
            profileLength: { min: 500, max: 6000, message: 'Profil uzunluğu 500mm ile 6000mm arasında olmalıdır' },
            bendingAngle: { min: 0, max: 180, message: 'Büküm açısı 0° ile 180° arasında olmalıdır' },
            bendingRadius: { min: 100, max: 2000, message: 'Büküm yarıçapı 100mm ile 2000mm arasında olmalıdır' },
            profileHeight: { min: 20, max: 200, message: 'Profil yüksekliği 20mm ile 200mm arasında olmalıdır' },
            profileThickness: { min: 0.5, max: 10, message: 'Et kalınlığı 0.5mm ile 10mm arasında olmalıdır' }
        };

        for (const [key, rule] of Object.entries(validationRules)) {
            const value = parameters[key];
            if (value === undefined || value < rule.min || value > rule.max) {
                this.showError(rule.message);
                return false;
            }
        }

        return true;
    }

    updateCalculationUI(isCalculating) {
        // Hesaplama butonunu güncelle
        const calculateButton = document.getElementById('btnCalculate');
        if (calculateButton) {
            calculateButton.disabled = isCalculating;
            calculateButton.innerHTML = isCalculating ? 
                '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Hesaplanıyor...' :
                'Hesapla';
        }

        // Form alanlarını devre dışı bırak/etkinleştir
        const formInputs = document.querySelectorAll('.bending-form input, .bending-form select');
        formInputs.forEach(input => {
            input.disabled = isCalculating;
        });
    }

    async visualizeResults(parameters, result) {
        if (!this.bendingVisualizer) {
            console.error('BendingVisualizer başlatılmamış');
            return;
        }

        try {
            // Canvas'ı temizle
            this.bendingVisualizer.clear();

            // Ölçeklendirmeyi hesapla
            this.bendingVisualizer.calculateScale(parameters, result);

            // Büküm merkezini belirle
            const centerX = this.bendingVisualizer.width / 2;
            const centerY = this.bendingVisualizer.height / 2;

            // Büküm yarıçaplarını hesapla
            const radii = this.bendingVisualizer.calculateBendingRadii(parameters, result);

            // Profil çizgilerini çiz
            this.bendingVisualizer.drawProfileLines(centerX, centerY, radii);

            // Top pozisyonlarını çiz
            this.bendingVisualizer.drawBallPositions(centerX, centerY, result);

            // Ölçüleri ve açıları göster
            this.bendingVisualizer.drawMeasurements(centerX, centerY, parameters, result);

            this.updateVisualizationLog('✅ Canvas çizimi tamamlandı');
        } catch (error) {
            console.error('Görselleştirme hatası:', error);
            this.updateVisualizationLog(`❌ Görselleştirme hatası: ${error.message}`);
            this.showError('Görselleştirme hatası: ' + error.message);
        }
    }

    displayResults(result) {
        // Hesaplama sonuçlarını göster
        const resultFields = {
            'leftTargetPosition': result.leftPistonTargetPosition,
            'rightTargetPosition': result.rightPistonTargetPosition,
            'bendingPressure': result.requiredPressure,
            'bendingTolerance': result.pressureTolerance,
            'effectiveBendingRadius': result.effectiveBendingRadius,
            'triangleWidth': result.triangleWidth,
            'triangleHeight': result.triangleHeight
        };

        for (const [id, value] of Object.entries(resultFields)) {
            const element = document.getElementById(id);
            if (element && value !== undefined) {
                element.value = value.toFixed(2);
            }
        }

        // Sonuç panelini göster
        const resultPanel = document.getElementById('calculationResults');
        if (resultPanel) {
            resultPanel.style.display = 'block';
        }
    }

    updateLastCalculation() {
        const now = new Date();
        const timeString = now.toLocaleTimeString('tr-TR');
        const dateString = now.toLocaleDateString('tr-TR');
        
        const lastCalcElement = document.getElementById('lastCalculation');
        if (lastCalcElement) {
            lastCalcElement.textContent = `${dateString} ${timeString}`;
        }
    }
    
    getFormParameters() {
        return {
            TopBallInnerDiameter: parseFloat(document.getElementById('topBallDiameter').value) || 220,
            BottomBallDiameter: parseFloat(document.getElementById('bottomBallDiameter').value) || 220,
            SideBallDiameter: parseFloat(document.getElementById('sideBallDiameter').value) || 220,
            BendingRadius: parseFloat(document.getElementById('bendingRadius').value) || 500,
            ProfileHeight: parseFloat(document.getElementById('profileHeight').value) || 80,
            ProfileLength: parseFloat(document.getElementById('profileLength').value) || 1960,
            ProfileThickness: parseFloat(document.getElementById('profileThickness').value) || 2,
            TriangleWidth: parseFloat(document.getElementById('triangleWidth').value) || 493,
            TriangleAngle: parseFloat(document.getElementById('triangleAngle').value) || 27,
            StepSize: parseFloat(document.getElementById('stepSize').value) || 20,
            TargetPressure: parseFloat(document.getElementById('targetPressure').value) || 50,
            PressureTolerance: parseFloat(document.getElementById('pressureTolerance').value) || 5,
            StageValue: 0,
            MaterialType: 'Aluminum',
            ProfileType: 'Rectangular'
        };
    }
    
    updateTargetPositions(data) {
        const leftTarget = document.getElementById('leftTargetPosition');
        const rightTarget = document.getElementById('rightTargetPosition');
        const bendingPressure = document.getElementById('bendingPressure');
        const bendingTolerance = document.getElementById('bendingTolerance');
        
        if (leftTarget) leftTarget.value = Math.abs(data.leftBallPosition?.x?.toFixed(2)) || 0;
        if (rightTarget) rightTarget.value = Math.abs(data.rightBallPosition?.x?.toFixed(2)) || 0;
        if (bendingPressure) bendingPressure.value = document.getElementById('targetPressure').value;
        if (bendingTolerance) bendingTolerance.value = document.getElementById('pressureTolerance').value;
    }
    
    async compressPart() {
        try {
            this.updateVisualizationLog('Parça sıkıştırma başlatılıyor...');
            
            // ✅ DÜZELTME: Tek istekle tüm kontrolleri yap
            this.updateVisualizationLog('🔧 Makine durumu kontrol ediliyor...');
            const machineStatus = await fetch(`${this.apiBaseUrl}/api/Machine/status`);
            
            if (!machineStatus.ok) {
                this.showError('Makine durumu alınamadı! Makine bağlantısını kontrol edin.');
                return;
            }

                const statusResult = await machineStatus.json();
            
            // Makine bağlantı kontrolü
            if (!statusResult.data?.isConnected) {
                this.showError('Makine bağlı değil! Önce makineye bağlanın.');
                return;
            }
            this.updateVisualizationLog('✅ Makine bağlı');
            
            // Sol parça varlık sensörü kontrolü
                if (!statusResult.data?.leftPartPresent) {
                    this.showError('Sol parça varlık sensörü parçayı görmüyor! Parça sıkıştırma başlatılamaz.');
                    this.updateVisualizationLog('❌ Sol parça varlık sensörü kontrolü başarısız');
                    return;
                }
                this.updateVisualizationLog('✅ Sol parça varlık sensörü parçayı görüyor');

            // Hidrolik motor durumu kontrolü
            const isHydraulicRunning = statusResult.data?.hydraulicMotorRunning || false;
            
            if (!isHydraulicRunning) {
                this.updateVisualizationLog('⚡ Hidrolik motor açık değil, motor başlatılıyor...');
                
                // Hidrolik motoru başlat
                const motorStarted = await this.startHydraulicMotor();
                if (!motorStarted) {
                    this.showError('Hidrolik motor başlatılamadı!');
                    return;
                }
                
                // 3 saniye bekle
                this.updateVisualizationLog('⏳ Hidrolik motor stabilizasyonu için 3 saniye bekleniyor...');
                await this.delay(3000);
                this.updateVisualizationLog('✅ Hidrolik motor hazır, sıkıştırma başlatılıyor...');
            } else {
                this.updateVisualizationLog('✅ Hidrolik motor zaten çalışıyor, sıkıştırma başlatılıyor...');
            }

            const targetPressure = parseFloat(document.getElementById('compressionPressure').value) || 50;
            const toleranceValue = parseFloat(document.getElementById('pressureTolerance').value) || 5;
            
            this.updateVisualizationLog(`🎯 Hedef Basınç: ${targetPressure} bar, Tolerans: ±${toleranceValue} bar`);

            // ✅ DÜZELTME: API model'ine uygun request oluştur
            const requestData = {
                TargetPressure: targetPressure,        // C# property adı büyük harf
                PressureTolerance: toleranceValue      // C# property adı büyük harf
                // TargetPosition kaldırıldı - API'de kullanılmıyor
            };

            const response = await fetch(`${this.apiBaseUrl}/api/Bending/compress-part`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestData)
            });

            // ✅ DÜZELTME: Response kontrolü geliştirildi
            const result = await response.json();

            // ✅ DÜZELTME: API response'u küçük harf "success" kullanıyor
            // HTTP error olsa bile JSON response kontrol et
            if (!response.ok && !result.success) {
                throw new Error(`HTTP ${response.status}: ${result.message || result.error || 'Bilinmeyen hata'}`);
            }

            if (result.success) {
                this.updateVisualizationLog('✅ Parça sıkıştırma tamamlandı');
                this.showSuccess(`Parça ${targetPressure} bar basınçta sıkıştırıldı (Tolerans: ±${toleranceValue} bar)`);
                
                // Başarılı sıkıştırma sonrası önerileri göster
                this.updateVisualizationLog('💡 Sonraki adım: Parça sıfırlama veya otomatik büküm başlatma');
            } else {
                throw new Error(result.message || 'Parça sıkıştırma başarısız');
            }

        } catch (error) {
            console.error('Parça sıkıştırma hatası:', error);
            this.updateVisualizationLog(`❌ Parça sıkıştırma hatası: ${error.message}`);
            this.showError('Parça sıkıştırma sırasında hata oluştu: ' + error.message);
            
            // ✅ DEBUG: Detaylı hata bilgisi
            console.group('🔍 Parça Sıkıştırma Hata Detay');
            console.log('Error Type:', error.constructor.name);
            console.log('Error Message:', error.message);
            console.log('Error Stack:', error.stack);
            try {
                const targetPressure = parseFloat(document.getElementById('compressionPressure').value) || 50;
                const toleranceValue = parseFloat(document.getElementById('pressureTolerance').value) || 5;
                const requestData = {
                    TargetPressure: targetPressure,
                    PressureTolerance: toleranceValue
                };
                console.log('Target Pressure:', targetPressure);
                console.log('Tolerance:', toleranceValue);
                console.log('Request Data:', requestData);
            } catch (debugError) {
                console.log('Debug bilgisi alınamadı:', debugError.message);
            }
            console.groupEnd();
        }
    }
    
    // Hidrolik motor durumunu kontrol et
    async checkHydraulicMotorStatus() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/Machine/status`);
            if (response.ok) {
                const result = await response.json();
                return result.data?.hydraulicMotorRunning || false;
            }
            return false;
        } catch (error) {
            console.error('Hidrolik motor durumu kontrol hatası:', error);
            return false;
        }
    }

    // Hidrolik motoru başlat
    async startHydraulicMotor() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/Motor/hydraulic/start`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.updateVisualizationLog('✅ Hidrolik motor başarıyla başlatıldı');
                    return true;
                } else {
                    this.updateVisualizationLog(`❌ Hidrolik motor başlatılamadı: ${result.message}`);
                    return false;
                }
            } else {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
        } catch (error) {
            console.error('Hidrolik motor başlatma hatası:', error);
            this.updateVisualizationLog(`❌ Hidrolik motor başlatma hatası: ${error.message}`);
            return false;
        }
    }

    // Async delay utility
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
    
    async resetPart() {
        try {
            this.updateVisualizationLog('Parça sıfırlama başlatılıyor...');
            
            // Makine bağlantısını kontrol et
            if (!(await this.isMachineConnected())) {
                this.showError('Makine bağlı değil! Önce makineye bağlanın.');
                return;
            }

            const resetDistance = parseFloat(document.getElementById('resetDistance').value) || 670;
            
            // İlerleme panelini göster
            this.showResetProgress(true);
            this.updateResetProgress(0, 'Parça sıfırlama işlemi başlatılıyor...');
            
            const requestData = {
                resetDistance: resetDistance  // Küçük harf property adı
            };

            this.updateResetProgress(10, 'Hidrolik motor kontrolü yapılıyor...');
            this.updateVisualizationLog('🔧 Hidrolik motor ve güvenlik kontrolleri yapılıyor...');

            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Bending/reset-part`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestData)
            });

            const result = await response.json();

            if (result.success) { // Küçük harf
                // Başarılı tamamlama animasyonu
                this.updateResetProgress(100, 'Parça sıfırlama başarıyla tamamlandı!');
                this.updateVisualizationLog('✅ Parça sıfırlama tamamlandı');
                this.showSuccess(`Parça ${resetDistance}mm mesafe ile sıfırlandı`);
                
                // 3 saniye sonra progress panelini gizle
                setTimeout(() => {
                    this.showResetProgress(false);
                }, 3000);
            } else {
                throw new Error(result.message || 'Parça sıfırlama başarısız');
            }

        } catch (error) {
            console.error('Parça sıfırlama hatası:', error);
            this.updateVisualizationLog(`❌ Parça sıfırlama hatası: ${error.message}`);
            this.showError('Parça sıfırlama sırasında hata oluştu: ' + error.message);
            this.showResetProgress(false);
        }
    }
    
    // Yeni metod: Reset progress panelini göster/gizle
    showResetProgress(show) {
        const panel = document.getElementById('resetProgressPanel');
        if (panel) {
            panel.style.display = show ? 'block' : 'none';
        }
    }

    // Yeni metod: Reset progress güncellemesi
    updateResetProgress(percentage, statusText) {
        const progressBar = document.getElementById('resetProgressBar');
        const statusElement = document.getElementById('resetStatusText');
        
        if (progressBar) {
            progressBar.style.width = percentage + '%';
            progressBar.textContent = percentage + '%';
            
            // İlerleme durumuna göre renk değiştir
            progressBar.className = 'progress-bar progress-bar-striped progress-bar-animated';
            if (percentage === 100) {
                progressBar.classList.add('bg-success');
            } else if (percentage > 50) {
                progressBar.classList.add('bg-info');
            } else {
                progressBar.classList.add('bg-warning');
            }
        }
        
        if (statusElement) {
            statusElement.textContent = statusText;
        }
    }
    
    async setStage(stageValue) {
        try {
            this.updateVisualizationLog(`Stage ${stageValue}mm ayarlanıyor...`);
            
            const request = {
                StageValue: stageValue  // C# property adı büyük harf
            };
            
            const response = await fetch(`${this.apiBaseUrl}/api/Bending/set-stage`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(request)
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog(`Stage ${stageValue}mm başarıyla ayarlandı`);
                this.showSuccess(`Stage ${stageValue}mm ayarlandı`);
            } else {
                throw new Error(result.message || 'Stage ayarlama başarısız');
            }
            
        } catch (error) {
            console.error('Stage ayarlama hatası:', error);
            this.updateVisualizationLog(`Stage hatası: ${error.message}`);
            this.showError('Stage ayarlama başarısız: ' + error.message);
        }
    }
    
    async startAutoBending() {
        try {
            this.updateVisualizationLog('Otomatik büküm başlatılıyor...');
            
            // Makine bağlantısını kontrol et
            if (!this.isMachineConnected()) {
                this.showError('Makine bağlı değil! Önce makineye bağlanın.');
                return;
            }
            
            // Hesaplama sonucu kontrolü
            if (!this.calculationResult) {
                this.showError('Önce büküm hesaplaması yapmalısınız!');
                return;
            }

            // UI güncellemesi
            this.updateBendingUI(true);
            
            const requestData = {
                bendingParameters: this.getFormParameters(),
                calculationResult: this.calculationResult
            };
            
            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Bending/start-auto-bending`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestData)
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog(`✅ Otomatik büküm başlatıldı: ${result.message}`);
                this.showSuccess('Otomatik büküm başarıyla başlatıldı!');
                
                // Progress panel'i göster
                this.showBendingProgress(true);
                
                // Butonları güncelle
                this.updateBendingButtons(true);
            } else {
                throw new Error(result.message || 'Otomatik büküm başlatılamadı');
            }
            
        } catch (error) {
            console.error('Otomatik büküm hatası:', error);
            this.showError('Otomatik büküm hatası: ' + error.message);
            this.updateVisualizationLog(`❌ Otomatik büküm hatası: ${error.message}`);
            
            // UI'ı sıfırla
            this.resetBendingUI();
        }
    }
    
    async stopAutoBending() {
        try {
            this.updateVisualizationLog('Otomatik büküm durduruluyor...');
            
            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Bending/stop`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog('✅ Otomatik büküm durduruldu');
                this.showSuccess('Otomatik büküm işlemi durduruldu');
                
                // UI'ı sıfırla
                this.resetBendingUI();
            } else {
                throw new Error(result.message || 'Otomatik büküm durdurulamadı');
            }
            
        } catch (error) {
            console.error('Büküm durdurma hatası:', error);
            this.showError('Büküm durdurma hatası: ' + error.message);
        }
    }
    
    updateBendingUI(isActive) {
        // Progress panel'i göster/gizle
        const progressPanel = document.getElementById('bendingProgressPanel');
        if (progressPanel) {
            progressPanel.style.display = isActive ? 'block' : 'none';
        }

        // Progress bar'ı sıfırla
        const progressBar = document.getElementById('bendingProgressBar');
        if (progressBar) {
            progressBar.style.width = '0%';
            progressBar.textContent = '0%';
        }

        // Durum metnini güncelle
        const statusText = document.getElementById('bendingStatusText');
        if (statusText) {
            statusText.textContent = isActive ? 'Hazırlanıyor...' : 'Hazır';
        }

        // Butonları güncelle
        this.updateBendingButtons(isActive);

        // Form alanlarını devre dışı bırak/etkinleştir
        const formInputs = document.querySelectorAll('.bending-form input, .bending-form select, .bending-form button');
        formInputs.forEach(input => {
            input.disabled = isActive;
        });
    }

    updateBendingButtons(isActive) {
        const startButton = document.getElementById('btnStartBending');
        const stopButton = document.getElementById('btnStopBending');
        const calculateButton = document.getElementById('btnCalculate');

        if (startButton) {
            startButton.disabled = isActive;
        }

        if (stopButton) {
            stopButton.disabled = !isActive;
        }

        if (calculateButton) {
            calculateButton.disabled = isActive;
        }
    }

    resetBendingUI() {
        this.updateBendingUI(false);
        this.showBendingProgress(false);
    }

    showBendingProgress(show) {
        const progressPanel = document.getElementById('bendingProgressPanel');
        if (progressPanel) {
            progressPanel.style.display = show ? 'block' : 'none';
        }
    }

    // Hata gösterme fonksiyonunu geliştir
    showError(message, timeout = 5000) {
        const errorDiv = document.createElement('div');
        errorDiv.className = 'alert alert-danger alert-dismissible fade show';
        errorDiv.innerHTML = `
            <strong>Hata!</strong> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        const container = document.querySelector('.bending-container');
        if (container) {
            container.insertBefore(errorDiv, container.firstChild);
        }
        
        // Belirli süre sonra otomatik kapat
        setTimeout(() => {
            errorDiv.classList.remove('show');
            setTimeout(() => errorDiv.remove(), 150);
        }, timeout);
    }

    // Başarı mesajı gösterme fonksiyonunu geliştir
    showSuccess(message, timeout = 3000) {
        const successDiv = document.createElement('div');
        successDiv.className = 'alert alert-success alert-dismissible fade show';
        successDiv.innerHTML = `
            <strong>Başarılı!</strong> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        const container = document.querySelector('.bending-container');
        if (container) {
            container.insertBefore(successDiv, container.firstChild);
        }
        
        // Belirli süre sonra otomatik kapat
        setTimeout(() => {
            successDiv.classList.remove('show');
            setTimeout(() => successDiv.remove(), 150);
        }, timeout);
    }
    
    async resetRulers() {
        try {
            this.updateVisualizationLog('Cetvel sıfırlama başlatılıyor...');
            
            // Makine bağlantısını kontrol et
            if (!this.isMachineConnected()) {
                this.showError('Makine bağlı değil! Önce makineye bağlanın.');
                return;
            }
            
            const response = await fetch(`${this.apiBaseUrl}/api/Bending/reset-rulers`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog('✅ Cetveller başarıyla sıfırlandı');
                this.showSuccess('Cetveller başarıyla sıfırlandı');
            } else {
                throw new Error(result.message || 'Cetvel sıfırlama başarısız');
            }
            
        } catch (error) {
            console.error('Cetvel sıfırlama hatası:', error);
            this.updateVisualizationLog(`❌ Cetvel sıfırlama hatası: ${error.message}`);
            this.showError('Cetvel sıfırlama sırasında hata oluştu: ' + error.message);
        }
    }
    
    calculateAll() {
        this.updateVisualizationLog('Otomatik parça yükleme başlatılıyor...');
    }
    
    resetParameters() {
        this.updateVisualizationLog('Parametreler dışa aktarılıyor...');
        const parameters = this.getFormParameters();
        const dataStr = JSON.stringify(parameters, null, 2);
        const dataBlob = new Blob([dataStr], {type: 'application/json'});
        
        const link = document.createElement('a');
        link.href = URL.createObjectURL(dataBlob);
        link.download = 'bending-parameters.json';
        link.click();
    }
    
    openRulerResetDialog() {
        if (confirm('Cetvelleri sıfırlamak istediğinizden emin misiniz?')) {
            this.resetRulers();
        }
    }

    async startPasoTest() {
        try {
            // Form verilerini al
            const sideBallDistance = parseFloat(document.getElementById('pasoSideBallDistance').value) || 40.85;
            const profileLength = parseFloat(document.getElementById('pasoProfileLength').value) || 670;
            const stepSize = parseFloat(document.getElementById('pasoStepSize').value) || 20;
            const evacuationTime = parseInt(document.getElementById('pasoEvacuationTime').value) || 10;

            // Validasyon
            if (sideBallDistance <= 0 || sideBallDistance > 200) {
                this.showError('Yan top hareket mesafesi 0-200mm arasında olmalı');
                return;
            }

            if (profileLength <= 0 || profileLength > 10000) {
                this.showError('Profil uzunluğu 0-10000mm arasında olmalı');
                return;
            }

            if (stepSize <= 0 || stepSize > 100) {
                this.showError('Adım büyüklüğü 0-100mm arasında olmalı');
                return;
            }

            // Makine bağlantı kontrolü
            if (!this.isMachineConnected()) {
                this.showError('Makine bağlantısı aktif değil!');
                return;
            }

            // Paso test isteği hazırla
            const request = {
                sideBallTravelDistance: sideBallDistance,
                profileLength: profileLength,
                stepSize: stepSize,
                evacuationTimeSeconds: evacuationTime
            };

            console.log('🧪 Paso test başlatılıyor:', request);

            // UI güncellemeleri
            const btnStartPasoTest = document.getElementById('btnStartPasoTest');
            btnStartPasoTest.disabled = true;
            btnStartPasoTest.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Paso Test Çalışıyor...';

            this.showPasoTestProgress(true);
            this.updatePasoTestProgress(10, 'Paso test başlatılıyor...');

            // API çağrısı
            const response = await fetch(`${this.apiBaseUrl}/api/Bending/test-paso`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(request)
            });

            const result = await response.json();

            if (result.success) {
                this.updatePasoTestProgress(100, 'Paso test başarıyla tamamlandı!');
                this.showSuccess('Paso test başarıyla tamamlandı!');
                
                // Test sonuçlarını göster
                if (result.data) {
                    const data = result.data;
                    const resultMessage = `
                        <strong>Test Sonuçları:</strong><br>
                        • Toplam Paso: ${data.totalSteps}<br>
                        • Sol/Sağ Mesafe: ${data.totalLeftDistance}/${data.totalRightDistance}mm<br>
                        • Aktif Sensör: ${data.activeSensor}<br>
                        • İlk Büküm: ${data.firstBendingSide}<br>
                        • Ters Hareket: ${data.initialReverseDistance}mm<br>
                        • Rotasyon: ${data.rotationDistance}mm
                    `;
                    this.updatePasoTestProgress(100, resultMessage);
                }

                console.log('✅ Paso test başarılı:', result);
            } else {
                this.updatePasoTestProgress(0, 'Paso test başarısız!');
                this.showError(`Paso test başarısız: ${result.message}`);
                console.error('❌ Paso test başarısız:', result);
            }

        } catch (error) {
            this.updatePasoTestProgress(0, 'Paso test hatası!');
            this.showError(`Paso test hatası: ${error.message}`);
            console.error('❌ Paso test hatası:', error);
        } finally {
            // UI'ı eski haline getir
            const btnStartPasoTest = document.getElementById('btnStartPasoTest');
            btnStartPasoTest.disabled = false;
            btnStartPasoTest.innerHTML = '<i class="fas fa-flask"></i> Paso Test Başlat';

            // Progress panel'i 3 saniye sonra gizle
            setTimeout(() => {
                this.showPasoTestProgress(false);
            }, 3000);
        }
    }

    showPasoTestProgress(show) {
        const panel = document.getElementById('pasoTestProgressPanel');
        if (panel) {
            panel.style.display = show ? 'block' : 'none';
        }
    }

    updatePasoTestProgress(percentage, statusText) {
        const progressBar = document.getElementById('pasoTestProgressBar');
        const statusTextElement = document.getElementById('pasoTestStatusText');
        
        if (progressBar) {
            progressBar.style.width = `${percentage}%`;
            progressBar.textContent = `${percentage}%`;
        }
        
        if (statusTextElement) {
            statusTextElement.innerHTML = statusText;
        }
    }
    
    isMachineConnected() {
        // ✅ SADECE SignalR kullanılacak - API status çekme kaldırıldı
        // Makine bağlantı durumu SignalR connection durumundan alınıyor
        return this.isConnected && this.connection?.state === signalR.HubConnectionState.Connected;
    }

    // Yeni metod: Cetvel durumunu kontrol et
    async checkRulerStatus() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/Bending/ruler-status`);
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.updateRulerStatusDisplay(result.data);
                    this.updateVisualizationLog('✅ Cetvel durumları güncellendi');
                } else {
                    this.updateVisualizationLog(`❌ Cetvel durumu alınamadı: ${result.message}`);
                }
            } else {
                // Simülasyon - gerçek API response'u yoksa
                const mockData = {
                    rulerResetM13toM16: Math.random() > 0.5 ? 2570 : 1234,
                    rulerResetM17toM20: Math.random() > 0.5 ? 2570 : 5678,
                    rulerResetPneumaticValve: Math.random() > 0.5 ? 2570 : 9876,
                    rulerResetRotation: Math.random() > 0.5 ? 2570 : 4321
                };
                this.updateRulerStatusDisplay(mockData);
                this.updateVisualizationLog('✅ Cetvel durumları güncellendi (simülasyon)');
            }
        } catch (error) {
            console.error('Cetvel durumu kontrolü hatası:', error);
            this.updateVisualizationLog(`❌ Cetvel durumu kontrol hatası: ${error.message}`);
        }
    }

    // Yeni metod: Cetvel durumunu güncelle
    updateRulerStatusDisplay(data) {
        const rulerM13M16 = document.getElementById('rulerM13M16');
        const rulerM17M20 = document.getElementById('rulerM17M20');
        const rulerPneumatic = document.getElementById('rulerPneumatic');
        const rulerRotation = document.getElementById('rulerRotation');

        if (rulerM13M16) {
            const isReset = data.rulerResetM13toM16 === 2570;
            rulerM13M16.className = `badge ${isReset ? 'bg-success' : 'bg-warning'}`;
            rulerM13M16.textContent = `M13-M16: ${isReset ? 'Sıfır' : data.rulerResetM13toM16}`;
        }

        if (rulerM17M20) {
            const isReset = data.rulerResetM17toM20 === 2570;
            rulerM17M20.className = `badge ${isReset ? 'bg-success' : 'bg-warning'}`;
            rulerM17M20.textContent = `M17-M20: ${isReset ? 'Sıfır' : data.rulerResetM17toM20}`;
        }

        if (rulerPneumatic) {
            const isReset = data.rulerResetPneumaticValve === 2570;
            rulerPneumatic.className = `badge ${isReset ? 'bg-success' : 'bg-warning'}`;
            rulerPneumatic.textContent = `Pnömatik: ${isReset ? 'Sıfır' : data.rulerResetPneumaticValve}`;
        }

        if (rulerRotation) {
            const isReset = data.rulerResetRotation === 2570;
            rulerRotation.className = `badge ${isReset ? 'bg-success' : 'bg-warning'}`;
            rulerRotation.textContent = `Rotasyon: ${isReset ? 'Sıfır' : data.rulerResetRotation}`;
        }

        // Genel durum kontrolü
        const allReset = data.rulerResetM13toM16 === 2570 && 
                        data.rulerResetM17toM20 === 2570 && 
                        data.rulerResetPneumaticValve === 2570 && 
                        data.rulerResetRotation === 2570;

        if (allReset) {
            this.updateVisualizationLog('✅ Tüm cetveller sıfırlanmış durumda');
        } else {
            this.updateVisualizationLog('⚠️ Bazı cetveller sıfırlama gerektiriyor');
        }
    }

    // Alarm event handler
    handleAlarmEvent(alarm) {
        console.warn('ALARM:', alarm);
        
        const severityColor = {
            'Critical': 'danger',
            'Warning': 'warning', 
            'Info': 'info'
        }[alarm.Severity] || 'danger';
        
        this.updateVisualizationLog(`🚨 ALARM [${alarm.Severity}]: ${alarm.Message}`);
        this.showError(`ALARM: ${alarm.Message}`, severityColor);
        
        // Kritik alarm'da tüm işlemleri durdur
        if (alarm.Severity === 'Critical') {
            this.showResetProgress(false);
            this.updateVisualizationLog('🛑 Kritik alarm nedeniyle tüm işlemler durduruldu');
        }
    }

    // Güvenlik ihlali event handler
    handleSafetyViolation(violation) {
        console.error('GÜVENLİK İHLALİ:', violation);
        
        this.updateVisualizationLog(`🚨 GÜVENLİK İHLALİ: ${violation.ViolationType}`);
        this.showError(`GÜVENLİK İHLALİ: ${violation.ViolationType}`, 'danger');
        
        if (violation.RequiresEmergencyStop) {
            this.updateVisualizationLog('🚨 ACİL DURDURMA GEREKTİRİYOR!');
            this.showResetProgress(false);
        }
    }

    // Encoder uyarı event handler
    handleEncoderStatus(data) {
        try {
            const encoderStatus = document.getElementById('encoderStatus');
            if (encoderStatus) {
                encoderStatus.textContent = `${data.CurrentPosition} pulse (${data.CurrentDistance.toFixed(2)}mm)`;
                encoderStatus.className = 'badge ' + (data.IsHealthy ? 'bg-success' : 'bg-danger');
            }
        } catch (error) {
            console.error('Encoder durum güncelleme hatası:', error);
        }
    }

    handlePressureUpdate(data) {
        try {
            const pressureStatus = document.getElementById('pressureStatus');
            if (pressureStatus) {
                pressureStatus.textContent = `S1: ${data.S1Pressure.toFixed(1)} bar, S2: ${data.S2Pressure.toFixed(1)} bar`;
                pressureStatus.className = 'badge bg-info';
            }
        } catch (error) {
            console.error('Basınç durum güncelleme hatası:', error);
        }
    }

    // Yeni metodlar - SignalR event handler'ları
    updatePistonStatus(pistons) {
        try {
            // Piston durumlarını güncelle
            pistons.forEach(piston => {
                const statusElement = document.getElementById(`${piston.type}Status`);
                if (statusElement) {
                    statusElement.textContent = `${piston.currentPosition.toFixed(2)}mm`;
                    statusElement.className = 'badge ' + (piston.isMoving ? 'bg-warning' : 'bg-info');
                }
            });
        } catch (error) {
            console.error('Piston durum güncelleme hatası:', error);
        }
    }

    updatePistonStatuses(status) {
        try {
            // Top Piston
            const topPistonStatus = document.getElementById('TopPistonStatus');
            if (topPistonStatus && status.topPistonPosition !== undefined) {
                topPistonStatus.textContent = `${status.topPistonPosition.toFixed(2)}mm`;
                topPistonStatus.className = 'badge ' + (status.isTopPistonMoving ? 'bg-warning' : 'bg-info');
            }

            // Bottom Piston
            const bottomPistonStatus = document.getElementById('BottomPistonStatus');
            if (bottomPistonStatus && status.bottomPistonPosition !== undefined) {
                bottomPistonStatus.textContent = `${status.bottomPistonPosition.toFixed(2)}mm`;
                bottomPistonStatus.className = 'badge ' + (status.isBottomPistonMoving ? 'bg-warning' : 'bg-info');
            }

            // Left Piston
            const leftPistonStatus = document.getElementById('LeftPistonStatus');
            if (leftPistonStatus && status.leftPistonPosition !== undefined) {
                leftPistonStatus.textContent = `${status.leftPistonPosition.toFixed(2)}mm`;
                leftPistonStatus.className = 'badge ' + (status.isLeftPistonMoving ? 'bg-warning' : 'bg-info');
            }

            // Right Piston
            const rightPistonStatus = document.getElementById('RightPistonStatus');
            if (rightPistonStatus && status.rightPistonPosition !== undefined) {
                rightPistonStatus.textContent = `${status.rightPistonPosition.toFixed(2)}mm`;
                rightPistonStatus.className = 'badge ' + (status.isRightPistonMoving ? 'bg-warning' : 'bg-info');
    }
        } catch (error) {
            console.error('Piston durumları güncelleme hatası:', error);
        }
    }

    async connect() {
        try {
            this.updateVisualizationLog('Makineye bağlanılıyor...');
            
            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Machine/connect`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog('✅ Makineye başarıyla bağlanıldı');
                this.showSuccess('Makine bağlantısı başarılı');
                this.updateConnectionStatus(true);
            } else {
                throw new Error(result.message || 'Makine bağlantısı başarısız');
            }
            
        } catch (error) {
            console.error('Makine bağlantı hatası:', error);
            this.showError('Makine bağlantı hatası: ' + error.message);
            this.updateConnectionStatus(false);
        }
    }

    async disconnect() {
        try {
            this.updateVisualizationLog('Makine bağlantısı kesiliyor...');
            
            const response = await this.fetchWithRetry(`${this.apiBaseUrl}/api/Machine/disconnect`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.updateVisualizationLog('✅ Makine bağlantısı kesildi');
                this.showSuccess('Makine bağlantısı başarıyla kesildi');
                this.updateConnectionStatus(false);
            } else {
                throw new Error(result.message || 'Makine bağlantısı kesilemedi');
            }
            
        } catch (error) {
            console.error('Makine bağlantısını kesme hatası:', error);
            this.showError('Makine bağlantısını kesme hatası: ' + error.message);
        }
    }
}

class BendingVisualizer {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) {
            console.error('Canvas elementi bulunamadı:', canvasId);
            return;
        }

        this.ctx = this.canvas.getContext('2d');
        this.width = this.canvas.width;
        this.height = this.canvas.height;
        this.scale = 0.8;
        this.margin = 40;
            
        // High-DPI optimizasyonu
        this.setupHighDPICanvas();

        // Stil ayarları
        this.styles = {
            profile: { color: '#2196f3', width: 2 },
            balls: { color: '#f44336', radius: 5 },
            measurements: { color: '#4caf50', font: '12px Arial' },
            grid: { color: '#e0e0e0', width: 0.5 },
            arrows: { color: '#9e9e9e', width: 1 }
        };
    }

    setupHighDPICanvas() {
        // Ekran DPI'ını al
        const dpr = window.devicePixelRatio || 1;

        // Canvas boyutlarını kaydet
        const originalWidth = this.canvas.width;
        const originalHeight = this.canvas.height;

        // Canvas'ı DPI'a göre ölçeklendir
        this.canvas.width = originalWidth * dpr;
        this.canvas.height = originalHeight * dpr;
        
        // CSS boyutlarını ayarla
        this.canvas.style.width = originalWidth + 'px';
        this.canvas.style.height = originalHeight + 'px';
        
        // Context'i ölçeklendir
        this.ctx.scale(dpr, dpr);
        
        // Gerçek boyutları kaydet
        this.width = originalWidth;
        this.height = originalHeight;
        }

    clear() {
        this.ctx.clearRect(0, 0, this.width, this.height);
        this.drawGrid();
    }

    drawGrid() {
        this.ctx.save();
        this.ctx.strokeStyle = this.styles.grid.color;
        this.ctx.lineWidth = this.styles.grid.width;
        
        // Yatay çizgiler
        for (let y = this.margin; y < this.height - this.margin; y += 20) {
        this.ctx.beginPath();
            this.ctx.moveTo(this.margin, y);
            this.ctx.lineTo(this.width - this.margin, y);
        this.ctx.stroke();
    }

        // Dikey çizgiler
        for (let x = this.margin; x < this.width - this.margin; x += 20) {
        this.ctx.beginPath();
            this.ctx.moveTo(x, this.margin);
            this.ctx.lineTo(x, this.height - this.margin);
        this.ctx.stroke();
        }
        
        this.ctx.restore();
    }

    drawProfileLines(centerX, centerY, radii) {
        this.ctx.save();
        
        // Profil çizgi stili
        this.ctx.strokeStyle = this.styles.profile.color;
        this.ctx.lineWidth = this.styles.profile.width;
        
        // Dış profil
        this.ctx.beginPath();
        this.ctx.arc(centerX, centerY, radii.outerRadius * this.scale, 0, Math.PI * 2);
        this.ctx.stroke();
        
        // İç profil
        this.ctx.beginPath();
        this.ctx.arc(centerX, centerY, radii.innerRadius * this.scale, 0, Math.PI * 2);
        this.ctx.stroke();
        
        this.ctx.restore();
    }

    drawBallPositions(centerX, centerY, result) {
        this.ctx.save();
        
        // Top stili
        this.ctx.fillStyle = this.styles.balls.color;
        
        // Top pozisyonlarını çiz
        const positions = [
            result.leftBallPosition,
            result.rightBallPosition,
            result.topBallPosition
        ];
        
        positions.forEach(pos => {
            if (pos) {
                const x = centerX + pos.x * this.scale;
                const y = centerY - pos.y * this.scale; // Y ekseni tersine

        this.ctx.beginPath();
                this.ctx.arc(x, y, this.styles.balls.radius, 0, Math.PI * 2);
                this.ctx.fill();
            }
        });
        
        this.ctx.restore();
    }

    drawMeasurements(centerX, centerY, parameters, result) {
        this.ctx.save();
        
        // Ölçü stili
        this.ctx.fillStyle = this.styles.measurements.color;
        this.ctx.font = this.styles.measurements.font;
        this.ctx.textAlign = 'center';
        
        // Yarıçap ölçüsü
        const radius = result.effectiveBendingRadius * this.scale;
        this.ctx.fillText(`R=${result.effectiveBendingRadius.toFixed(1)}mm`, centerX, centerY - radius - 10);
        
        // Üçgen ölçüleri
        if (result.triangleWidth && result.triangleHeight) {
            const width = result.triangleWidth * this.scale;
            const height = result.triangleHeight * this.scale;
            
            this.ctx.fillText(`W=${result.triangleWidth.toFixed(1)}mm`, centerX, centerY + height + 20);
            this.ctx.fillText(`H=${result.triangleHeight.toFixed(1)}mm`, centerX + width + 10, centerY + height/2);
        }
        
        // Açı ölçüsü
        if (parameters.bendingAngle) {
            const angle = parameters.bendingAngle;
            const angleRadius = 30;
            
        this.ctx.beginPath();
            this.ctx.arc(centerX, centerY, angleRadius, 0, angle * Math.PI / 180);
            this.ctx.stroke();
        
            this.ctx.fillText(`${angle}°`, centerX + angleRadius * Math.cos(angle * Math.PI / 360), 
                                        centerY + angleRadius * Math.sin(angle * Math.PI / 360));
        }
        
        this.ctx.restore();
    }

    drawArrow(fromX, fromY, toX, toY) {
        const headLength = 10;
        const angle = Math.atan2(toY - fromY, toX - fromX);
        
        this.ctx.save();
        this.ctx.strokeStyle = this.styles.arrows.color;
        this.ctx.lineWidth = this.styles.arrows.width;
        
        // Ana çizgi
        this.ctx.beginPath();
        this.ctx.moveTo(fromX, fromY);
        this.ctx.lineTo(toX, toY);
        this.ctx.stroke();
        
        // Ok başı
        this.ctx.beginPath();
        this.ctx.moveTo(toX, toY);
        this.ctx.lineTo(toX - headLength * Math.cos(angle - Math.PI/6),
                       toY - headLength * Math.sin(angle - Math.PI/6));
        this.ctx.moveTo(toX, toY);
        this.ctx.lineTo(toX - headLength * Math.cos(angle + Math.PI/6),
                       toY - headLength * Math.sin(angle + Math.PI/6));
        this.ctx.stroke();
        
        this.ctx.restore();
    }
}

// Global AutoBendingController instance
let autoBendingController;

// Sayfa yüklendiğinde çalışacak kod
document.addEventListener('DOMContentLoaded', () => {
    // AutoBendingController'ı başlat
    autoBendingController = new AutoBendingController();
        
    // Global window nesnesine ata (eski kodlarla uyumluluk için)
    window.autoBendingController = autoBendingController;
});

// Global fonksiyonlar - HTML onclick olayları için
function connectMachine() {
    if (autoBendingController) {
        autoBendingController.connect();
    }
}

function disconnectMachine() {
    if (autoBendingController) {
        autoBendingController.disconnect();
    }
}

function calculateBending() {
    if (autoBendingController) {
        autoBendingController.calculateBending();
    }
}

function compressPart() {
    if (autoBendingController) {
        autoBendingController.compressPart();
    }
}

function resetPart() {
    if (autoBendingController) {
        autoBendingController.resetPart();
    }
}

function setStage() {
    if (autoBendingController) {
        const stageValue = parseInt(document.getElementById('stageValue').value) || 60;
        autoBendingController.setStage(stageValue);
    }
}

function startAutoBending() {
    if (autoBendingController) {
        autoBendingController.startAutoBending();
    }
}

function stopAutoBending() {
    if (autoBendingController) {
        autoBendingController.stopAutoBending();
    }
}

function resetRulers() {
    if (autoBendingController) {
        autoBendingController.resetRulers();
    }
}

function calculateAll() {
    if (autoBendingController) {
        autoBendingController.calculateAll();
    }
}

function resetParameters() {
    if (autoBendingController) {
        autoBendingController.resetParameters();
    }
}

function openRulerResetDialog() {
    if (autoBendingController) {
        autoBendingController.openRulerResetDialog();
    }
}

function startPasoTest() {
    if (autoBendingController) {
        autoBendingController.startPasoTest();
    }
}