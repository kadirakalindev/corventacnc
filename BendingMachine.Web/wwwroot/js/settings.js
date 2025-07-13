// Settings.js - Makine Ayarları Yönetimi
class SettingsManager {
    constructor() {
        this.apiBaseUrl = 'http://localhost:5002';
        this.configuration = null;
        this.init();
    }

    async init() {
        await this.loadConfiguration();
        this.generatePistonConfiguration();
        this.setupEventListeners();
    }

    setupEventListeners() {
        // Save configuration event
        document.getElementById('saveBtn')?.addEventListener('click', () => this.saveConfiguration());
    }

    async loadConfiguration() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/configuration`);
            const result = await response.json();

                    if (result.success) {
            this.configuration = result.data;
                this.populateForm();
                this.showToast('Konfigürasyon başarıyla yüklendi', 'success');
            } else {
                this.showToast('Konfigürasyon yüklenemedi', 'error');
            }
        } catch (error) {
            console.error('Configuration load error:', error);
            this.showToast('Konfigürasyon yüklenirken hata oluştu', 'error');
            this.loadDefaultConfiguration();
        }
    }

    populateForm() {
        if (!this.configuration) return;

        // Modbus settings
        const modbus = this.configuration.modbus;
        document.getElementById('modbusIpAddress').value = modbus?.ipAddress || '192.168.1.100';
        document.getElementById('modbusPort').value = modbus?.port || 502;
        document.getElementById('modbusSlaveId').value = modbus?.slaveId || 1;
        document.getElementById('modbusTimeout').value = modbus?.timeoutMs || 3000;
        document.getElementById('modbusRetryCount').value = modbus?.retryCount || 3;
        document.getElementById('modbusUpdateInterval').value = modbus?.updateIntervalMs || 100;

        // Ball settings
        const balls = this.configuration.balls;
        document.getElementById('topBallDiameter').value = balls?.topBallDiameter || 220;
        document.getElementById('bottomBallDiameter').value = balls?.bottomBallDiameter || 220;
        document.getElementById('leftBallDiameter').value = balls?.leftBallDiameter || 220;
        document.getElementById('rightBallDiameter').value = balls?.rightBallDiameter || 220;
        document.getElementById('topBallReferenceMaxHeight').value = balls?.topBallReferenceMaxHeight || 473;

        // Geometry settings
        const geometry = this.configuration.geometry;
        document.getElementById('triangleWidth').value = geometry?.triangleWidth || 493;
        document.getElementById('triangleAngle').value = geometry?.triangleAngle || 27;
        document.getElementById('defaultProfileHeight').value = geometry?.defaultProfileHeight || 80;
        document.getElementById('defaultBendingRadius').value = geometry?.defaultBendingRadius || 500;
        document.getElementById('stepSize').value = geometry?.stepSize || 20;

        // Safety settings
        const safety = this.configuration.safety;
        document.getElementById('maxPressure').value = safety?.maxPressure || 400;
        document.getElementById('defaultTargetPressure').value = safety?.defaultTargetPressure || 50;
        document.getElementById('pressureTolerance').value = safety?.pressureTolerance || 5;
        document.getElementById('workingOilTemperature').value = safety?.workingOilTemperature || 40;
        document.getElementById('maxOilTemperature').value = safety?.maxOilTemperature || 80;
        document.getElementById('minOilLevel').value = safety?.minOilLevel || 20;
        document.getElementById('fanOnTemperature').value = safety?.fanOnTemperature || 50;
        document.getElementById('fanOffTemperature').value = safety?.fanOffTemperature || 40;

        // Oil System settings
        const oilSystem = this.configuration.oilSystem;
        if (document.getElementById('s1MaxPressure')) {
            document.getElementById('s1MaxPressure').value = oilSystem?.s1MaxPressure || 400;
        }
        if (document.getElementById('s2MaxPressure')) {
            document.getElementById('s2MaxPressure').value = oilSystem?.s2MaxPressure || 400;
        }
        if (document.getElementById('maxFlowRate')) {
            document.getElementById('maxFlowRate').value = oilSystem?.maxFlowRate || 297;
        }
        if (document.getElementById('minFlowRate')) {
            document.getElementById('minFlowRate').value = oilSystem?.minFlowRate || 0;
        }

        // Generate stages
        this.generateStageConfiguration();
    }

    generateStageConfiguration() {
        const container = document.getElementById('stagesContainer');
        if (!container) return;

        container.innerHTML = '';
        const stages = this.configuration?.stages?.stages || [];

        stages.forEach((stage, index) => {
            const stageElement = this.createStageElement(stage, index);
            container.appendChild(stageElement);
        });

        if (stages.length === 0) {
            this.addDefaultStages();
        }
    }

    createStageElement(stage, index) {
        const div = document.createElement('div');
        div.className = 'stage-item border rounded p-3 mb-3';
        div.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2">
                <h6><i class="fas fa-layer-group"></i> ${stage.name}</h6>
                <button class="btn btn-danger btn-sm" onclick="settingsManager.removeStage(${index})">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
            <div class="row">
                <div class="col-md-4">
                    <label class="form-label">Stage İsmi:</label>
                    <input type="text" class="form-control stage-name" value="${stage.name}" data-index="${index}">
                </div>
                <div class="col-md-2">
                    <label class="form-label">Değer:</label>
                    <input type="number" class="form-control stage-value" value="${stage.value}" data-index="${index}" step="0.1">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Sol Piston Offset:</label>
                    <input type="number" class="form-control stage-left-offset" value="${stage.leftPistonOffset}" data-index="${index}" step="0.01">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Sağ Piston Offset:</label>
                    <input type="number" class="form-control stage-right-offset" value="${stage.rightPistonOffset}" data-index="${index}" step="0.01">
                </div>
            </div>
        `;
        return div;
    }

    generatePistonConfiguration() {
        const container = document.getElementById('pistonsContainer');
        if (!container) return;

        const pistons = this.configuration?.pistons || {};
        container.innerHTML = '';

        Object.keys(pistons).forEach(pistonKey => {
            const piston = pistons[pistonKey];
            const pistonElement = this.createPistonElement(pistonKey, piston);
            container.appendChild(pistonElement);
        });
    }

    createPistonElement(pistonKey, piston) {
        const div = document.createElement('div');
        div.className = 'piston-item border rounded p-3 mb-3';
        div.innerHTML = `
            <h6><i class="fas fa-arrows-alt"></i> ${piston.name}</h6>
            <div class="row">
                <div class="col-md-3">
                    <label class="form-label">Stroke Length (mm):</label>
                    <input type="number" class="form-control piston-stroke" value="${piston.strokeLength}" data-piston="${pistonKey}" step="0.1">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Register Count:</label>
                    <input type="number" class="form-control piston-register" value="${piston.registerCount}" data-piston="${pistonKey}">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Position Tolerance:</label>
                    <input type="number" class="form-control piston-tolerance" value="${piston.positionTolerance}" data-piston="${pistonKey}" step="0.1">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Max Speed:</label>
                    <input type="number" class="form-control piston-maxspeed" value="${piston.maxSpeed}" data-piston="${pistonKey}" step="0.1">
                </div>
            </div>
        `;
        return div;
    }

    async saveConfiguration() {
        try {
            const updatedConfig = this.collectFormData();
            
            const response = await fetch(`${this.apiBaseUrl}/configuration`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(updatedConfig)
            });

            const result = await response.json();

            if (result.Success) {
                this.configuration = updatedConfig;
                this.showToast('Konfigürasyon başarıyla kaydedildi', 'success');
                
                // Also save to file
                await this.saveToFile();
            } else {
                this.showToast('Konfigürasyon kaydedilemedi: ' + result.Message, 'error');
            }
        } catch (error) {
            console.error('Configuration save error:', error);
            this.showToast('Konfigürasyon kaydedilirken hata oluştu', 'error');
        }
    }

    async saveToFile() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/configuration/save`, {
                method: 'POST'
            });
            
            const result = await response.json();
            
                    if (result.success) {
                this.showToast('Konfigürasyon dosyaya başarıyla kaydedildi', 'success');
            }
        } catch (error) {
            console.error('File save error:', error);
            this.showToast('Dosya kaydetme hatası', 'warning');
        }
    }

    async loadFromFile() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/configuration/load`, {
                method: 'POST'
            });
            
            const result = await response.json();
            
            if (result.Success) {
                this.showToast('Konfigürasyon dosyadan başarıyla yüklendi', 'success');
                await this.loadConfiguration(); // Reload the configuration
            } else {
                this.showToast('Dosyadan yükleme başarısız', 'error');
            }
        } catch (error) {
            console.error('File load error:', error);
            this.showToast('Dosya yükleme hatası', 'error');
        }
    }

    collectFormData() {
        return {
            modbus: {
                ipAddress: document.getElementById('modbusIpAddress').value,
                port: parseInt(document.getElementById('modbusPort').value),
                slaveId: parseInt(document.getElementById('modbusSlaveId').value),
                timeoutMs: parseInt(document.getElementById('modbusTimeout').value),
                retryCount: parseInt(document.getElementById('modbusRetryCount').value),
                updateIntervalMs: parseInt(document.getElementById('modbusUpdateInterval').value)
            },
            stages: {
                stages: this.collectStageData()
            },
            balls: {
                topBallDiameter: parseFloat(document.getElementById('topBallDiameter').value),
                bottomBallDiameter: parseFloat(document.getElementById('bottomBallDiameter').value),
                leftBallDiameter: parseFloat(document.getElementById('leftBallDiameter').value),
                rightBallDiameter: parseFloat(document.getElementById('rightBallDiameter').value),
                topBallReferenceMaxHeight: parseFloat(document.getElementById('topBallReferenceMaxHeight').value)
            },
            geometry: {
                triangleWidth: parseFloat(document.getElementById('triangleWidth').value),
                triangleAngle: parseFloat(document.getElementById('triangleAngle').value),
                defaultProfileHeight: parseFloat(document.getElementById('defaultProfileHeight').value),
                defaultBendingRadius: parseFloat(document.getElementById('defaultBendingRadius').value),
                stepSize: parseFloat(document.getElementById('stepSize').value)
            },
            safety: {
                maxPressure: parseFloat(document.getElementById('maxPressure').value),
                defaultTargetPressure: parseFloat(document.getElementById('defaultTargetPressure').value),
                pressureTolerance: parseFloat(document.getElementById('pressureTolerance').value),
                workingOilTemperature: parseFloat(document.getElementById('workingOilTemperature').value),
                maxOilTemperature: parseFloat(document.getElementById('maxOilTemperature').value),
                minOilLevel: parseFloat(document.getElementById('minOilLevel').value),
                fanOnTemperature: parseFloat(document.getElementById('fanOnTemperature').value),
                fanOffTemperature: parseFloat(document.getElementById('fanOffTemperature').value)
            },
            oilSystem: {
                s1MaxPressure: parseFloat(document.getElementById('s1MaxPressure')?.value || 400),
                s2MaxPressure: parseFloat(document.getElementById('s2MaxPressure')?.value || 400),
                maxFlowRate: parseFloat(document.getElementById('maxFlowRate')?.value || 297),
                minFlowRate: parseFloat(document.getElementById('minFlowRate')?.value || 0)
            },
            pistons: this.collectPistonData()
        };
    }

    collectStageData() {
        const stages = [];
        document.querySelectorAll('.stage-item').forEach((item, index) => {
            stages.push({
                name: item.querySelector('.stage-name').value,
                value: parseFloat(item.querySelector('.stage-value').value),
                leftPistonOffset: parseFloat(item.querySelector('.stage-left-offset').value),
                rightPistonOffset: parseFloat(item.querySelector('.stage-right-offset').value)
            });
        });
        return stages;
    }

    collectPistonData() {
        const pistons = {};
        document.querySelectorAll('.piston-item').forEach(item => {
            const pistonKey = item.querySelector('[data-piston]').dataset.piston;
            pistons[pistonKey] = {
                name: this.configuration.pistons[pistonKey].name,
                strokeLength: parseFloat(item.querySelector('.piston-stroke').value),
                registerCount: parseInt(item.querySelector('.piston-register').value),
                positionTolerance: parseFloat(item.querySelector('.piston-tolerance').value),
                maxSpeed: parseFloat(item.querySelector('.piston-maxspeed').value),
                defaultSpeed: this.configuration.pistons[pistonKey].defaultSpeed
            };
        });
        return pistons;
    }

    addStage() {
        const stages = this.configuration?.stages?.stages || [];
        const newStage = {
            name: `Stage ${stages.length * 60}`,
            value: stages.length * 60,
            leftPistonOffset: 0,
            rightPistonOffset: 0
        };
        
        if (!this.configuration.stages) {
            this.configuration.stages = { stages: [] };
        }
        this.configuration.stages.stages.push(newStage);
        this.generateStageConfiguration();
    }

    removeStage(index) {
        if (this.configuration?.stages?.stages) {
            this.configuration.stages.stages.splice(index, 1);
            this.generateStageConfiguration();
        }
    }

    addDefaultStages() {
        this.configuration.stages = {
            stages: [
                { name: "Stage 0", value: 0, leftPistonOffset: 0, rightPistonOffset: 0 },
                { name: "Stage 60", value: 60, leftPistonOffset: 67.34, rightPistonOffset: 67.34 },
                { name: "Stage 120", value: 120, leftPistonOffset: 134.68, rightPistonOffset: 134.68 }
            ]
        };
        this.generateStageConfiguration();
    }

    loadDefaultConfiguration() {
        this.configuration = {
            modbus: {
                ipAddress: "192.168.1.100",
                port: 502,
                slaveId: 1,
                timeoutMs: 3000,
                retryCount: 3,
                updateIntervalMs: 100
            },
            stages: {
                stages: [
                    { name: "Stage 0", value: 0, leftPistonOffset: 0, rightPistonOffset: 0 },
                    { name: "Stage 60", value: 60, leftPistonOffset: 67.34, rightPistonOffset: 67.34 },
                    { name: "Stage 120", value: 120, leftPistonOffset: 134.68, rightPistonOffset: 134.68 }
                ]
            },
            balls: {
                topBallDiameter: 220,
                bottomBallDiameter: 220,
                leftBallDiameter: 220,
                rightBallDiameter: 220,
                topBallReferenceMaxHeight: 473
            },
            geometry: {
                triangleWidth: 493,
                triangleAngle: 27,
                defaultProfileHeight: 80,
                defaultBendingRadius: 500,
                stepSize: 20
            },
            safety: {
                maxPressure: 400,
                defaultTargetPressure: 50,
                pressureTolerance: 5,
                workingOilTemperature: 40,
                maxOilTemperature: 80,
                minOilLevel: 20,
                fanOnTemperature: 50,
                fanOffTemperature: 40
            },
            oilSystem: {
                s1MaxPressure: 400,
                s2MaxPressure: 400,
                maxFlowRate: 297,
                minFlowRate: 0
            },
            pistons: {
                "topPiston": { name: "Top Piston", strokeLength: 160, registerCount: 32767, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "bottomPiston": { name: "Bottom Piston", strokeLength: 195, registerCount: 32767, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "leftPiston": { name: "Left Piston", strokeLength: 422, registerCount: 32767, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "rightPiston": { name: "Right Piston", strokeLength: 422, registerCount: 32767, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "leftReelPiston": { name: "Left Reel Piston", strokeLength: 352, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "rightReelPiston": { name: "Right Reel Piston", strokeLength: 352, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "leftBodyPiston": { name: "Left Body Piston", strokeLength: 129, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "rightBodyPiston": { name: "Right Body Piston", strokeLength: 129, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "leftJoinPiston": { name: "Left Join Piston", strokeLength: 187, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 },
                "rightJoinPiston": { name: "Right Join Piston", strokeLength: 187, registerCount: 4095, positionTolerance: 1, maxSpeed: 10, defaultSpeed: 5 }
            }
        };
        this.populateForm();
    }

    showToast(message, type = 'info') {
        const toast = document.getElementById('settingsToast');
        const toastMessage = document.getElementById('toastMessage');
        
        if (toast && toastMessage) {
            toastMessage.textContent = message;
            
            // Remove previous type classes
            toast.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');
            
            // Add new type class
            switch (type) {
                case 'success':
                    toast.classList.add('bg-success', 'text-white');
                    break;
                case 'error':
                    toast.classList.add('bg-danger', 'text-white');
                    break;
                case 'warning':
                    toast.classList.add('bg-warning');
                    break;
                default:
                    toast.classList.add('bg-info', 'text-white');
            }
            
            const bsToast = new bootstrap.Toast(toast);
            bsToast.show();
        }
    }
}

// Global settings manager instance
let settingsManager;

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    settingsManager = new SettingsManager();
});

// Global functions for HTML onclick events
function saveConfiguration() {
    if (settingsManager) {
        settingsManager.saveConfiguration();
    }
}

function loadConfiguration() {
    if (settingsManager) {
        settingsManager.loadFromFile();
    }
}

function resetToDefaults() {
    if (settingsManager && confirm('Tüm ayarları varsayılan değerlere sıfırlamak istediğinizden emin misiniz?')) {
        settingsManager.loadDefaultConfiguration();
        settingsManager.showToast('Ayarlar varsayılan değerlere sıfırlandı', 'warning');
    }
}

function addStage() {
    if (settingsManager) {
        settingsManager.addStage();
    }
} 