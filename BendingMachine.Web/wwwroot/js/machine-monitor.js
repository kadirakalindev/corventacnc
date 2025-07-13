// Machine Monitoring ve Manuel Kontrol JavaScript

class MachineMonitor {
    constructor() {
        this.apiBaseUrl = 'http://localhost:5002';
        this.connection = null;
        this.isConnected = false;
        this.lastData = null;
        this.pistonStates = {
            top: { moving: false, direction: null },
            bottom: { moving: false, direction: null },
            left: { moving: false, direction: null },
            right: { moving: false, direction: null }
        };
        this.rotationState = {
            active: false,
            direction: null
        };
        
        // Precision Reset State
        this.precisionResetState = {
            active: false,
            currentPhase: 0,
            progress: 0,
            currentSpeed: 0,
            remainingDistance: 0,
            encoderPosition: 0,
            startTime: null
        };
        
        // Precision Control Configuration (Sabit Optimal Değerler)
        this.precisionConfig = {
            phase1Speed: 70,    // %70 hız - İlk %80 mesafe
            phase2Speed: 40,    // %40 hız - %80-95 arası  
            phase3Speed: 15,    // %15 hız - Son %5 mesafe
            phase1Threshold: 80, // %80'e kadar Phase 1
            phase2Threshold: 95, // %95'e kadar Phase 2
            encoderFreezeTolerance: 2000, // 2 saniye
            overshootProtection: 2, // %2 aşım koruması
            updateInterval: 150 // 150ms güncelleme aralığı
        };
        
        this.initializeSignalR();
        this.bindEvents();
        this.initializeSpeedSliders();
    }

    // SignalR Connection
    async initializeSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(`${this.apiBaseUrl}/machinestatus`)
                .withAutomaticReconnect()
                .build();

            // Event listeners
            this.connection.on('MachineStatusUpdate', (data) => {
                this.updateMachineData(data);
            });

            this.connection.on('AlarmRaised', (message) => {
                this.showToast(`🚨 ALARM: ${message}`, 'danger');
            });

            this.connection.on('SafetyViolation', (message) => {
                this.showToast(`⚠️ GÜVENLİK: ${message}`, 'warning');
            });

            // Precision Reset Events
            this.connection.on('PrecisionResetUpdate', (data) => {
                this.updatePrecisionResetStatus(data);
            });

            this.connection.on('EncoderStatusUpdate', (data) => {
                this.updateEncoderStatus(data);
            });

            // Start connection
            await this.connection.start();
            this.isConnected = true;
            this.updateConnectionStatus('API Bağlı', 'success');
            
            console.log('SignalR bağlantısı kuruldu');
            
            // İlk veri çekme
            this.startDataRefresh();
        } catch (error) {
            console.error('SignalR bağlantı hatası:', error);
            this.updateConnectionStatus('Bağlantı Hatası', 'danger');
        }
    }

    // Periyodik veri güncelleme
    startDataRefresh() {
        // ✅ SADECE SignalR kullanılacak - API status çekme kaldırıldı
        // Tüm makine durumu SignalR üzerinden gerçek zamanlı olarak alınıyor
        console.log('✅ Machine Monitor: SADECE SignalR kullanılarak başlatıldı - API status çekme YOK');
    }

    // Event Binding
    bindEvents() {
        // Bağlantı Kontrolleri
        document.getElementById('btnConnect')?.addEventListener('click', () => this.connectMachine());
        document.getElementById('btnDisconnect')?.addEventListener('click', () => this.disconnectMachine());

        // Ana Piston Kontrolleri - İleri/Geri Butonları
        this.bindPistonButtons('Top');
        this.bindPistonButtons('Bottom');
        this.bindPistonButtons('Left');
        this.bindPistonButtons('Right');

        // Ana Piston Pozisyon Kontrolleri
        document.getElementById('btnTopPistonGo')?.addEventListener('click', () => this.movePistonToPosition('TopPiston'));
        document.getElementById('btnBottomPistonGo')?.addEventListener('click', () => this.movePistonToPosition('BottomPiston'));
        document.getElementById('btnLeftPistonGo')?.addEventListener('click', () => this.movePistonToPosition('LeftPiston'));
        document.getElementById('btnRightPistonGo')?.addEventListener('click', () => this.movePistonToPosition('RightPiston'));

        // Yan Dayama Kontrolleri
        this.bindSideSupportButtons('LeftReel');
        this.bindSideSupportButtons('LeftBody');
        this.bindSideSupportButtons('LeftJoin');
        this.bindSideSupportButtons('RightReel');
        this.bindSideSupportButtons('RightBody');
        this.bindSideSupportButtons('RightJoin');

        // Makine Kontrolleri
        document.getElementById('btnStartHydraulic')?.addEventListener('click', () => this.controlMachine('hydraulic', 'start'));
        document.getElementById('btnStopHydraulic')?.addEventListener('click', () => this.controlMachine('hydraulic', 'stop'));
        document.getElementById('btnStartFan')?.addEventListener('click', () => this.controlMachine('fan', 'start'));
        document.getElementById('btnStopFan')?.addEventListener('click', () => this.controlMachine('fan', 'stop'));
        document.getElementById('btnStartAlarm')?.addEventListener('click', () => this.controlAlarm('start'));
        document.getElementById('btnStopAlarm')?.addEventListener('click', () => this.controlAlarm('stop'));

        // Acil Durdur ve Sistem Sıfırlama
        document.getElementById('btnEmergencyStop')?.addEventListener('click', () => this.emergencyStop());
        document.getElementById('btnResetAlarm')?.addEventListener('click', () => this.resetAlarm());

        // Rotasyon Kontrolleri
        const btnRotationForward = document.getElementById('btnRotationForward');
        const btnRotationBackward = document.getElementById('btnRotationBackward');
        const btnRotationStop = document.getElementById('btnRotationStop');

        if (btnRotationForward) {
            btnRotationForward.addEventListener('mousedown', () => this.controlRotation('forward'));
            btnRotationForward.addEventListener('mouseup', () => this.controlRotation('stop'));
            btnRotationForward.addEventListener('touchstart', () => this.controlRotation('forward'));
            btnRotationForward.addEventListener('touchend', () => this.controlRotation('stop'));
        }

        if (btnRotationBackward) {
            btnRotationBackward.addEventListener('mousedown', () => this.controlRotation('backward'));
            btnRotationBackward.addEventListener('mouseup', () => this.controlRotation('stop'));
            btnRotationBackward.addEventListener('touchstart', () => this.controlRotation('backward'));
            btnRotationBackward.addEventListener('touchend', () => this.controlRotation('stop'));
        }

        if (btnRotationStop) {
            btnRotationStop.addEventListener('click', () => this.controlRotation('stop'));
        }

        // Precision Reset Kontrolleri
        document.getElementById('btnPrecisionReset')?.addEventListener('click', () => this.startPrecisionReset());
        document.getElementById('btnStopPrecisionReset')?.addEventListener('click', () => this.stopPrecisionReset());

        // Cetvel Sıfırlama Kontrolleri
        document.getElementById('btnCheckRulerStatus')?.addEventListener('click', () => this.checkRulerStatus());
        document.getElementById('btnResetRulers')?.addEventListener('click', () => this.startRulerReset());
        document.getElementById('btnStopRulerReset')?.addEventListener('click', () => this.stopRulerReset());
    }

    bindPistonButtons(pistonType) {
        const forwardBtn = document.getElementById(`btn${pistonType}PistonForward`);
        const backwardBtn = document.getElementById(`btn${pistonType}PistonBackward`);

        if (forwardBtn) {
            forwardBtn.addEventListener('mousedown', () => this.startPistonJog(pistonType, 'forward'));
            forwardBtn.addEventListener('mouseup', () => this.stopPiston(pistonType));
            forwardBtn.addEventListener('touchstart', () => this.startPistonJog(pistonType, 'forward'));
            forwardBtn.addEventListener('touchend', () => this.stopPiston(pistonType));
        }

        if (backwardBtn) {
            backwardBtn.addEventListener('mousedown', () => this.startPistonJog(pistonType, 'backward'));
            backwardBtn.addEventListener('mouseup', () => this.stopPiston(pistonType));
            backwardBtn.addEventListener('touchstart', () => this.startPistonJog(pistonType, 'backward'));
            backwardBtn.addEventListener('touchend', () => this.stopPiston(pistonType));
        }
    }

    bindSideSupportButtons(supportType) {
        const forwardBtn = document.getElementById(`btn${supportType}Forward`);
        const backwardBtn = document.getElementById(`btn${supportType}Backward`);

        if (forwardBtn) {
            forwardBtn.addEventListener('mousedown', () => this.startSideSupportJog(supportType, 'forward'));
            forwardBtn.addEventListener('mouseup', () => this.stopSideSupport(supportType));
            forwardBtn.addEventListener('touchstart', () => this.startSideSupportJog(supportType, 'forward'));
            forwardBtn.addEventListener('touchend', () => this.stopSideSupport(supportType));
        }

        if (backwardBtn) {
            backwardBtn.addEventListener('mousedown', () => this.startSideSupportJog(supportType, 'backward'));
            backwardBtn.addEventListener('mouseup', () => this.stopSideSupport(supportType));
            backwardBtn.addEventListener('touchstart', () => this.startSideSupportJog(supportType, 'backward'));
            backwardBtn.addEventListener('touchend', () => this.stopSideSupport(supportType));
        }
    }

    // Hız Slider'larını Initialize Et
    initializeSpeedSliders() {
        // Pozitif hız slider'ı
        const positiveSlider = document.getElementById('positiveSpeedSlider');
        const positiveDisplay = document.getElementById('positiveSpeedValue');
        
        if (positiveSlider && positiveDisplay) {
            positiveSlider.addEventListener('input', (e) => {
                const value = parseFloat(e.target.value);
                positiveDisplay.textContent = `+${value.toFixed(1)} V`;
            });
        }

        // Negatif hız slider'ı
        const negativeSlider = document.getElementById('negativeSpeedSlider');
        const negativeDisplay = document.getElementById('negativeSpeedValue');
        
        if (negativeSlider && negativeDisplay) {
            negativeSlider.addEventListener('input', (e) => {
                const value = parseFloat(e.target.value);
                negativeDisplay.textContent = `-${value.toFixed(1)} V`;
            });
        }
    }

    // API Calls
    async connectMachine() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/machine/connect`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('Makine bağlantısı başarılı', 'success');
                document.getElementById('machineConnectionStatus').className = 'badge bg-success';
                document.getElementById('machineConnectionStatus').textContent = 'Makine Bağlı';
            } else {
                this.showToast(`Bağlantı hatası: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Bağlantı hatası:', error);
            this.showToast(`Bağlantı hatası: ${error.message}`, 'danger');
        }
    }

    async disconnectMachine() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/machine/disconnect`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('Makine bağlantısı kesildi', 'info');
                document.getElementById('machineConnectionStatus').className = 'badge bg-danger';
                document.getElementById('machineConnectionStatus').textContent = 'Makine Bağlı Değil';
            }
        } catch (error) {
            console.error('Bağlantı kesme hatası:', error);
            this.showToast(`Bağlantı kesme hatası: ${error.message}`, 'danger');
        }
    }

    async startPistonJog(pistonType, direction) {
        try {
            const voltage = this.getVoltageForDirection(direction);
            // Direction'a göre voltajı ayarla (ileri için negatif, geri için pozitif)
            const finalVoltage = direction === 'forward' ? -Math.abs(voltage) : Math.abs(voltage);
            
            const response = await fetch(`${this.apiBaseUrl}/api/piston/${pistonType}Piston/move`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    Voltage: finalVoltage
                })
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.pistonStates[pistonType.toLowerCase()] = { moving: true, direction: direction };
                this.showToast(`${pistonType} piston ${direction === 'forward' ? 'ileri' : 'geri'} hareket ediyor`, 'info');
            } else {
                this.showToast(`Piston hareket hatası: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Piston hareket hatası:', error);
            this.showToast(`Piston hareket hatası: ${error.message}`, 'danger');
        }
    }

    async stopPiston(pistonType) {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/piston/${pistonType}Piston/stop`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.pistonStates[pistonType.toLowerCase()] = { moving: false, direction: null };
                this.showToast(`${pistonType} piston durduruldu`, 'info');
            }
        } catch (error) {
            console.error('Piston durdurma hatası:', error);
            this.showToast(`Piston durdurma hatası: ${error.message}`, 'danger');
        }
    }

    async movePistonToPosition(pistonType) {
        try {
            const inputId = pistonType.charAt(0).toLowerCase() + pistonType.slice(1) + 'TargetPosition';
            const targetPosition = parseFloat(document.getElementById(inputId).value);
            
            if (isNaN(targetPosition) || targetPosition < 0) {
                this.showToast('Geçerli bir pozisyon değeri girin', 'warning');
                return;
            }

            const response = await fetch(`${this.apiBaseUrl}/api/piston/${pistonType}/move-to-position`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    TargetPosition: targetPosition,
                    Speed: 5.0
                })
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`${pistonType} ${targetPosition}mm pozisyonuna hareket ediyor`, 'success');
            } else {
                this.showToast(`Piston hareketi başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Piston pozisyon hatası:', error);
            this.showToast(`Piston pozisyon hatası: ${error.message}`, 'danger');
        }
    }

    async startSideSupportJog(supportType, direction) {
        try {
            // UI sadece komutu gönderir, valve mantığı backend'de
            const response = await fetch(`${this.apiBaseUrl}/api/piston/${supportType}/jog-side-support`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    Direction: direction  // API modelinde büyük harf Direction bekliyor
                })
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`${supportType} ${direction === 'forward' ? 'ileri' : 'geri'} hareket ediyor`, 'info');
            } else {
                this.showToast(`Yan dayama hatası: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Yan dayama hatası:', error);
            this.showToast(`Yan dayama hatası: ${error.message}`, 'danger');
        }
    }

    async stopSideSupport(supportType) {
        try {
            // UI sadece durdur komutu gönderir
            const response = await fetch(`${this.apiBaseUrl}/api/piston/${supportType}/stop-side-support`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast(`${supportType} durduruldu`, 'info');
            }
        } catch (error) {
            console.error('Yan dayama durdurma hatası:', error);
            this.showToast(`Yan dayama durdurma hatası: ${error.message}`, 'danger');
        }
    }

    async controlMachine(controlType, action) {
        try {
            let endpoint;
            
            if (controlType === 'hydraulic') {
                endpoint = `/api/motor/hydraulic/${action}`;
            } else if (controlType === 'fan') {
                endpoint = `/api/motor/fan/${action}`;
            }
            
            const response = await fetch(`${this.apiBaseUrl}${endpoint}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                const actionText = action === 'start' ? 'başlatıldı' : 'durduruldu';
                this.showToast(`${controlType.toUpperCase()} motor ${actionText}`, 'success');
                
                // Status güncelle
                const statusId = `${controlType}Status`;
                const statusElement = document.getElementById(statusId);
                if (statusElement) {
                    statusElement.className = action === 'start' ? 'badge bg-success' : 'badge bg-secondary';
                    statusElement.textContent = action === 'start' ? 'Açık' : 'Kapalı';
                }
            } else {
                this.showToast(`${controlType} kontrolü başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Makine kontrol hatası:', error);
            this.showToast(`Makine kontrol hatası: ${error.message}`, 'danger');
        }
    }

    async controlAlarm(action) {
        try {
            if (action === 'start') {
                // Alarm açma işlevi - bu özellik daha sonra eklenebilir
                this.showToast('Alarm çalıştırma özelliği henüz mevcut değil', 'warning');
                return;
            }
            
            // Alarm sıfırlama
            const response = await fetch(`${this.apiBaseUrl}/api/safety/reset-alarm`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('Alarm sıfırlandı', 'success');
                
                const statusElement = document.getElementById('alarmStatus');
                if (statusElement) {
                    statusElement.className = 'badge bg-secondary';
                    statusElement.textContent = 'Pasif';
                }
            } else {
                this.showToast(`Alarm kontrolü başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Alarm kontrol hatası:', error);
            this.showToast(`Alarm kontrol hatası: ${error.message}`, 'danger');
        }
    }

    async emergencyStop() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/api/safety/emergency-stop`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('🚨 ACİL DURDUR AKTİF! Tüm operasyonlar durduruldu.', 'danger');
            } else {
                this.showToast(`Acil durdur başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Acil durdur hatası:', error);
            this.showToast(`Acil durdur hatası: ${error.message}`, 'danger');
        }
    }

    async resetAlarm() {
        try {
            // Kullanıcı onayı iste
            if (!confirm('🔄 SİSTEMİ SIFIRLAMAK İSTİYOR MUSUNUZ?\n\n✅ Rotasyon coil\'leri sıfırlanacak\n✅ Alarmlar temizlenecek\n✅ Hidrolik motor başlatılacak\n✅ Sistem normale dönecek')) {
                return;
            }

            this.showToast('🔄 Sistem sıfırlanıyor...', 'info');

            const response = await fetch(`${this.apiBaseUrl}/api/safety/reset-alarm`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('✅ SİSTEM SIFIRLANDI! Rotasyon artık çalışabilir.', 'success');
            } else {
                this.showToast(`❌ Sistem sıfırlama başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Sistem sıfırlama hatası:', error);
            this.showToast(`❌ Sistem sıfırlama hatası: ${error.message}`, 'danger');
        }
    }

    async controlRotation(action) {
        try {
            // Toggle mantığı: Eğer aynı direction'a basıldıysa durdur, değilse başlat
            if (this.rotationState.active && this.rotationState.direction === action) {
                // Aynı yöne basıldı - durdur
                action = 'stop';
            }
            
            let endpoint;
            let body = {};
            
            if (action === 'forward') {
                endpoint = '/api/motor/rotation/start';
                body = {
                    Direction: "forward",
                    Speed: 50.0
                };
            } else if (action === 'backward') {
                endpoint = '/api/motor/rotation/start';
                body = {
                    Direction: "backward", 
                    Speed: 50.0
                };
            } else if (action === 'stop') {
                endpoint = '/api/motor/rotation/stop';
                body = {};
            }
            
            // UI sadece komutu gönderir, S1/S2 valve mantığı backend'de yapılır
            const response = await fetch(`${this.apiBaseUrl}${endpoint}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const result = await response.json();
            
            if (result.success) {
                this.updateRotationUI(action);
            } else {
                this.showToast(`Rotasyon kontrolü başarısız: ${result.message || 'Bilinmeyen hata'}`, 'danger');
            }
        } catch (error) {
            console.error('Rotasyon kontrol hatası:', error);
            this.showToast(`Rotasyon kontrol hatası: ${error.message}`, 'danger');
        }
    }

    updateRotationUI(action) {
        // Tüm butonları reset et
        document.getElementById('btnRotationForward')?.classList.remove('active', 'btn-outline-success');
        document.getElementById('btnRotationBackward')?.classList.remove('active', 'btn-outline-warning');
        
        let actionText;
        let statusClass;
        let statusText;
        
        if (action === 'forward') {
            actionText = 'İleri rotasyon başlatıldı';
            statusClass = 'badge bg-success';
            statusText = 'İleri Aktif';
            this.rotationState = { active: true, direction: 'forward' };
            
            // Forward butonunu aktif göster
            const forwardBtn = document.getElementById('btnRotationForward');
            if (forwardBtn) {
                forwardBtn.classList.add('active');
                forwardBtn.innerHTML = '<i class="fas fa-rotate-right"></i> İleri Rotasyon (AKTİF)';
            }
        } else if (action === 'backward') {
            actionText = 'Geri rotasyon başlatıldı';
            statusClass = 'badge bg-warning';
            statusText = 'Geri Aktif';
            this.rotationState = { active: true, direction: 'backward' };
            
            // Backward butonunu aktif göster
            const backwardBtn = document.getElementById('btnRotationBackward');
            if (backwardBtn) {
                backwardBtn.classList.add('active');
                backwardBtn.innerHTML = '<i class="fas fa-rotate-left"></i> Geri Rotasyon (AKTİF)';
            }
        } else {
            actionText = 'Rotasyon durduruldu';
            statusClass = 'badge bg-secondary';
            statusText = 'Pasif';
            this.rotationState = { active: false, direction: null };
            
            // Buton metinlerini reset et
            const forwardBtn = document.getElementById('btnRotationForward');
            const backwardBtn = document.getElementById('btnRotationBackward');
            if (forwardBtn) {
                forwardBtn.innerHTML = '<i class="fas fa-rotate-right"></i> İleri Rotasyon';
            }
            if (backwardBtn) {
                backwardBtn.innerHTML = '<i class="fas fa-rotate-left"></i> Geri Rotasyon';
            }
        }
        
        this.showToast(actionText, action === 'stop' ? 'info' : 'success');
        
        // Rotasyon status güncelle
        const statusElement = document.getElementById('rotationStatus');
        if (statusElement) {
            statusElement.className = statusClass;
            statusElement.textContent = statusText;
        }
    }

    getVoltageForDirection(direction) {
        if (direction === 'forward') {
            const slider = document.getElementById('positiveSpeedSlider');
            return slider ? parseFloat(slider.value) : 5.0;
        } else {
            const slider = document.getElementById('negativeSpeedSlider');
            return slider ? -parseFloat(slider.value) : -5.0;
        }
    }

    // UI Updates
    updateMachineData(data) {
        if (!data) return;
        
        // Piston Positions
        this.updateElement('topPistonPosition', data.topPistonPosition.toFixed(2));
        this.updateElement('bottomPistonPosition', data.bottomPistonPosition.toFixed(2));
        this.updateElement('leftPistonPosition', data.leftPistonPosition.toFixed(2));
        this.updateElement('rightPistonPosition', data.rightPistonPosition.toFixed(2));
        this.updateElement('leftReelPistonPosition', data.leftReelPistonPosition.toFixed(2));
        this.updateElement('rightReelPistonPosition', data.rightReelPistonPosition.toFixed(2));
        this.updateElement('leftBodyPistonPosition', data.leftBodyPistonPosition.toFixed(2));
        this.updateElement('rightBodyPistonPosition', data.rightBodyPistonPosition.toFixed(2));
        this.updateElement('leftJoinPistonPosition', data.leftJoinPistonPosition.toFixed(2));
        this.updateElement('rightJoinPistonPosition', data.rightJoinPistonPosition.toFixed(2));
        
        // Oil System
        this.updateElement('s1Pressure', data.s1OilPressure.toFixed(1));
        this.updateElement('s2Pressure', data.s2OilPressure.toFixed(1));
        this.updateElement('s1FlowRate', data.s1OilFlowRate.toFixed(1));
        this.updateElement('s2FlowRate', data.s2OilFlowRate.toFixed(1));
        this.updateElement('oilTemperature', data.oilTemperature.toFixed(1));
        this.updateElement('oilHumidity', data.oilHumidity.toFixed(1));
        this.updateElement('oilLevel', data.oilLevel.toFixed(1));
        
        // Rotation System
        this.updateElement('rotationPosition', data.rotationPosition.toFixed(1));
        this.updateElement('rotationSpeed', data.rotationSpeed.toFixed(1));
        this.updateElement('rotationDirection', data.rotationDirection);
        
        // Safety Status
        this.updateElement('emergencyStop', data.emergencyStop ? '⚠️ AKTİF' : '✅ PASİF');
        this.updateElement('hydraulicThermal', data.hydraulicThermalError ? '⚠️ HATA' : '✅ NORMAL');
        this.updateElement('fanThermal', data.fanThermalError ? '⚠️ HATA' : '✅ NORMAL');
        this.updateElement('phaseSequence', data.phaseSequenceError ? '⚠️ HATA' : '✅ NORMAL');
        
        // Motor Status
        this.updateElement('hydraulicMotor', data.hydraulicMotorRunning ? '✅ ÇALIŞIYOR' : '⚫ DURDU');
        this.updateElement('fanMotor', data.fanMotorRunning ? '✅ ÇALIŞIYOR' : '⚫ DURDU');
        this.updateElement('alarm', data.alarmActive ? '🚨 AKTİF' : '✅ PASİF');
        
        // Valve Status
        this.updateElement('s1Valve', data.s1ValveOpen ? '🟢 AÇIK' : '🔴 KAPALI');
        this.updateElement('s2Valve', data.s2ValveOpen ? '🟢 AÇIK' : '🔴 KAPALI');
        
        // Part Presence - ✅ DÜZELTME: HTML'deki doğru element ID'leri kullan
        this.updateElement('sensorLeftPart', data.leftPartPresent ? '✅ VAR' : '❌ YOK');
        this.updateElement('sensorRightPart', data.rightPartPresent ? '✅ VAR' : '❌ YOK');
        
        // Pollution Sensors
        this.updateElement('pollutionSensor1', data.pollutionSensor1 ? '⚠️ KİRLİ' : '✅ TEMİZ');
        this.updateElement('pollutionSensor2', data.pollutionSensor2 ? '⚠️ KİRLİ' : '✅ TEMİZ');
        this.updateElement('pollutionSensor3', data.pollutionSensor3 ? '⚠️ KİRLİ' : '✅ TEMİZ');
        
        // Last Update Time
        const lastUpdate = new Date(data.lastUpdateTime);
        this.updateElement('lastUpdateTime', lastUpdate.toLocaleTimeString());
    }

    updateConnectionStatus(message, type) {
        const statusElement = document.getElementById('connectionStatus');
        if (statusElement) {
            statusElement.className = `badge bg-${type}`;
            statusElement.textContent = message;
        }
    }

    updateElement(elementId, value) {
        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = value;
        }
    }

    showToast(message, type = 'info') {
        const toastElement = document.getElementById('alertToast');
        const messageElement = document.getElementById('toastMessage');
        
        if (toastElement && messageElement) {
            toastElement.className = `toast border-${type}`;
            messageElement.innerHTML = `<div class="alert alert-${type} mb-0">${message}</div>`;
            
            const toast = new bootstrap.Toast(toastElement);
            toast.show();
        } else {
            console.log(`Toast: ${message}`);
        }
    }

    // ==================== PRECISION RESET FUNCTIONS ====================
    
    async startPrecisionReset() {
        try {
            this.showToast('🎯 Hassas parça sıfırlama başlatılıyor...', 'info');
            
            // UI'ı güncelle
            this.showPrecisionControlPanel(true);
            this.resetPrecisionDisplay();
            
            // API'ye precision reset başlat komutu gönder
            const response = await fetch(`${this.apiBaseUrl}/api/bending/reset-part`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    usePrecisionControl: true,
                    precisionConfig: this.precisionConfig
                })
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.precisionResetState.active = true;
                this.precisionResetState.startTime = Date.now();
                
                // Real-time monitoring başlat
                this.startPrecisionMonitoring();
                
                this.showToast('✅ Hassas parça sıfırlama başlatıldı', 'success');
                this.updatePrecisionDisplay();
            } else {
                this.showToast(`❌ Hassas sıfırlama başlatılamadı: ${result.message}`, 'danger');
                this.showPrecisionControlPanel(false);
            }
        } catch (error) {
            console.error('Precision reset başlatma hatası:', error);
            this.showToast(`❌ Hassas sıfırlama hatası: ${error.message}`, 'danger');
            this.showPrecisionControlPanel(false);
        }
    }

    async stopPrecisionReset() {
        try {
            this.showToast('🛑 Hassas parça sıfırlama durduruluyor...', 'warning');
            
            // API'ye stop komutu gönder
            const response = await fetch(`${this.apiBaseUrl}/api/bending/rotation/stop`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.precisionResetState.active = false;
                this.stopPrecisionMonitoring();
                this.showPrecisionControlPanel(false);
                this.showToast('✅ Hassas parça sıfırlama durduruldu', 'success');
            } else {
                this.showToast(`❌ Durdurma işlemi başarısız: ${result.message}`, 'danger');
            }
        } catch (error) {
            console.error('Precision reset durdurma hatası:', error);
            this.showToast(`❌ Durdurma hatası: ${error.message}`, 'danger');
        }
    }

    startPrecisionMonitoring() {
        if (this.precisionMonitorInterval) {
            clearInterval(this.precisionMonitorInterval);
        }
        
        this.precisionMonitorInterval = setInterval(() => {
            if (!this.precisionResetState.active) {
                this.stopPrecisionMonitoring();
                return;
            }
            
            // Encoder durumu iste
            this.requestEncoderStatus();
            
            // Progress simulation (gerçek değerler API'den gelecek)
            this.simulatePrecisionProgress();
            
        }, this.precisionConfig.updateInterval);
    }

    stopPrecisionMonitoring() {
        if (this.precisionMonitorInterval) {
            clearInterval(this.precisionMonitorInterval);
            this.precisionMonitorInterval = null;
        }
    }

    async requestEncoderStatus() {
        try {
            if (this.connection && this.isConnected) {
                await this.connection.invoke('RequestEncoderStatus');
            }
        } catch (error) {
            console.error('Encoder status request hatası:', error);
        }
    }

    simulatePrecisionProgress() {
        // Gerçek makine bağlantısı - API'den gelen gerçek veriler kullanılıyor
        // Simülasyon kodu kaldırıldı, encoder ve progress verileri SignalR ile gelecek
        if (!this.precisionResetState.active) return;
        
        // Sadece UI güncellemesi yap - gerçek veriler SignalR'dan gelecek
        this.updatePrecisionDisplay();
    }

    updatePrecisionResetStatus(data) {
        if (data) {
            this.precisionResetState.progress = data.progress || this.precisionResetState.progress;
            this.precisionResetState.currentPhase = data.currentPhase || this.precisionResetState.currentPhase;
            this.precisionResetState.currentSpeed = data.currentSpeed || this.precisionResetState.currentSpeed;
            this.precisionResetState.remainingDistance = data.remainingDistance || this.precisionResetState.remainingDistance;
            
            this.updatePrecisionDisplay();
        }
    }

    updateEncoderStatus(data) {
        if (data) {
            this.precisionResetState.encoderPosition = data.position || 0;
            const encoderElement = document.getElementById('encoderPosition');
            if (encoderElement) {
                encoderElement.textContent = `${data.position || 0} pulse`;
            }
        }
    }

    showPrecisionControlPanel(show) {
        const panel = document.getElementById('precisionControlPanel');
        const button = document.getElementById('btnPrecisionReset');
        
        if (panel) {
            panel.style.display = show ? 'block' : 'none';
        }
        
        if (button) {
            button.disabled = show;
        }
    }

    resetPrecisionDisplay() {
        this.precisionResetState.progress = 0;
        this.precisionResetState.currentPhase = 0;
        this.precisionResetState.currentSpeed = 0;
        this.precisionResetState.remainingDistance = 140; // 140mm ball diameter
        this.precisionResetState.encoderPosition = 0;
        
        this.updatePrecisionDisplay();
    }

    updatePrecisionDisplay() {
        // Phase indicator
        const phaseElement = document.getElementById('currentPhase');
        if (phaseElement) {
            const phase = this.precisionResetState.currentPhase;
            let phaseText = 'Bekleme';
            let phaseClass = 'badge bg-secondary';
            
            if (phase === 1) {
                phaseText = 'Faz 1 - Hızlı Yaklaşım';
                phaseClass = 'badge bg-success';
            } else if (phase === 2) {
                phaseText = 'Faz 2 - Orta Hız';
                phaseClass = 'badge bg-warning';
            } else if (phase === 3) {
                phaseText = 'Faz 3 - Hassas Konum';
                phaseClass = 'badge bg-danger';
            }
            
            phaseElement.className = phaseClass + ' ms-2';
            phaseElement.textContent = phaseText;
        }
        
        // Progress bar
        const progressBar = document.getElementById('resetProgressBar');
        if (progressBar) {
            const progress = Math.round(this.precisionResetState.progress);
            progressBar.style.width = `${progress}%`;
            progressBar.textContent = `${progress}%`;
            
            // Progress bar rengi faza göre değişsin
            const phase = this.precisionResetState.currentPhase;
            progressBar.className = 'progress-bar progress-bar-striped progress-bar-animated';
            if (phase === 1) progressBar.className += ' bg-success';
            else if (phase === 2) progressBar.className += ' bg-warning';
            else if (phase === 3) progressBar.className += ' bg-danger';
        }
        
        // Current speed
        const speedElement = document.getElementById('currentSpeed');
        if (speedElement) {
            speedElement.textContent = `${this.precisionResetState.currentSpeed}%`;
        }
        
        // Remaining distance
        const distanceElement = document.getElementById('remainingDistance');
        if (distanceElement) {
            distanceElement.textContent = `${this.precisionResetState.remainingDistance.toFixed(1)} mm`;
        }
        
        // Encoder position
        const encoderElement = document.getElementById('encoderPosition');
        if (encoderElement) {
            encoderElement.textContent = `${this.precisionResetState.encoderPosition} pulse`;
        }
    }

    // ==================== CETVEL SIFIRLAMA FUNCTIONS ====================
    
    async checkRulerStatus() {
        try {
            this.showToast('🔍 Cetvel durumları kontrol ediliyor...', 'info');
            
            // API'den cetvel durumlarını al
            const response = await fetch(`${this.apiBaseUrl}/api/bending/ruler-status`);
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.updateRulerStatusDisplay(result.data);
                    this.showToast('✅ Cetvel durumları güncellendi', 'success');
                } else {
                    this.showToast(`❌ Cetvel durumu alınamadı: ${result.message}`, 'danger');
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
                this.showToast('✅ Cetvel durumları güncellendi (simülasyon)', 'info');
            }
        } catch (error) {
            console.error('Cetvel durumu kontrolü hatası:', error);
            this.showToast(`❌ Cetvel durumu kontrol hatası: ${error.message}`, 'danger');
        }
    }

    updateRulerStatusDisplay(data) {
        const statusMappings = [
            { id: 'rulerStatusM13M16', key: 'rulerResetM13toM16', name: 'M13-M16' },
            { id: 'rulerStatusM17M20', key: 'rulerResetM17toM20', name: 'M17-M20' },
            { id: 'rulerStatusPneumatic', key: 'rulerResetPneumaticValve', name: 'Pnömatik' },
            { id: 'rulerStatusRotation', key: 'rulerResetRotation', name: 'Rotasyon' }
        ];

        statusMappings.forEach(mapping => {
            const element = document.getElementById(mapping.id);
            if (element && data[mapping.key] !== undefined) {
                const value = data[mapping.key];
                
                if (value === 2570) {
                    element.className = 'badge bg-success';
                    element.textContent = `✅ Sıfırlanmış (${value})`;
                } else {
                    element.className = 'badge bg-warning';
                    element.textContent = `⚠️ Sıfırlama Gerekli (${value})`;
                }
            }
        });
    }

    async startRulerReset() {
        try {
            // Kullanıcıdan onay al
            if (!confirm('�� UYARI: Cetvel sıfırlama işlemi tüm pistonları hareket ettirir ve yaklaşık 2-3 dakika sürer.\n\nGüvenlik önlemlerini aldığınızdan emin misiniz?\n\nDevam etmek istiyor musunuz?')) {
                return;
            }

            this.showToast('🔧 Cetvel sıfırlama işlemi başlatılıyor...', 'info');
            
            // UI'ı güncelle
            this.showRulerResetPanel(true);
            this.updateRulerResetStep('Adres Kontrolü', 10);
            this.updateRulerResetStatus('Reset adresleri kontrol ediliyor...');
            
            // API'ye cetvel sıfırlama başlat komutu gönder
            const response = await fetch(`${this.apiBaseUrl}/api/bending/reset-rulers`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showToast('✅ Cetvel sıfırlama başlatıldı', 'success');
                
                // Simülasyon ile adımları göster
                this.simulateRulerResetProgress();
            } else {
                this.showToast(`❌ Cetvel sıfırlama başlatılamadı: ${result.message}`, 'danger');
                this.showRulerResetPanel(false);
            }
        } catch (error) {
            console.error('Cetvel sıfırlama başlatma hatası:', error);
            this.showToast(`❌ Cetvel sıfırlama hatası: ${error.message}`, 'danger');
            this.showRulerResetPanel(false);
        }
    }

    async stopRulerReset() {
        try {
            this.showToast('🛑 Cetvel sıfırlama durduruluyor...', 'warning');
            
            // API'ye stop komutu gönder
            const response = await fetch(`${this.apiBaseUrl}/api/machine/emergency-stop`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showRulerResetPanel(false);
                this.showToast('✅ Cetvel sıfırlama durduruldu', 'success');
            } else {
                this.showToast(`❌ Durdurma işlemi başarısız: ${result.message}`, 'danger');
            }
        } catch (error) {
            console.error('Cetvel sıfırlama durdurma hatası:', error);
            this.showToast(`❌ Durdurma hatası: ${error.message}`, 'danger');
        }
    }

    simulateRulerResetProgress() {
        // GERÇEK MAKİNE: Bu simülasyon kodu devre dışı bırakıldı
        // Progress bilgileri SignalR üzerinden MachineDriver'dan gelecek
        // Aşağıdaki kod sadece test/geliştirme amaçlı referans olarak saklanıyor
        
        console.log('🔧 Cetvel sıfırlama başlatıldı - Gerçek progress SignalR\'dan gelecek');
        
        /* SIMÜLASYON KODU - DEVRE DIŞI
        const steps = [
            { name: 'Adres Kontrolü', progress: 10, duration: 1000, info: '4 adet reset adresi kontrol ediliyor...' },
            // ... diğer adımlar
        ];
        // Simülasyon kodu gerçek makine için devre dışı bırakıldı
        */
    }

    showRulerResetPanel(show) {
        const panel = document.getElementById('rulerResetPanel');
        const resetButton = document.getElementById('btnResetRulers');
        const stopButton = document.getElementById('btnStopRulerReset');
        
        if (panel) {
            panel.style.display = show ? 'block' : 'none';
        }
        
        if (resetButton) {
            resetButton.disabled = show;
        }
        
        if (stopButton) {
            stopButton.disabled = !show;
        }
    }

    updateRulerResetStep(stepName, progress) {
        const stepElement = document.getElementById('resetCurrentStep');
        const progressBar = document.getElementById('rulerResetProgressBar');
        
        if (stepElement) {
            stepElement.textContent = stepName;
        }
        
        if (progressBar) {
            progressBar.style.width = `${progress}%`;
            progressBar.textContent = `${progress}%`;
        }
    }

    updateRulerResetStatus(statusText) {
        const statusElement = document.getElementById('resetStatusInfo');
        if (statusElement) {
            statusElement.textContent = statusText;
        }
    }
}

// Initialize when DOM loaded
document.addEventListener('DOMContentLoaded', () => {
    window.machineMonitor = new MachineMonitor();
});