/**
 * QR Code Scanner for reading session IDs from QR codes
 * Handles camera access, scanning, and error recovery
 */

import { EventEmitter } from './utils.js';

class QRScanner extends EventEmitter {
    constructor() {
        super();
        this.html5QrCode = null;
        this.isScanning = false;
        this.lastScanTime = 0;
        this.scanCooldown = 2000; // 2 seconds between scans
        this.cameras = [];
        this.currentCameraId = null;
        this.config = {
            fps: 10,
            qrbox: { width: 250, height: 250 },
            aspectRatio: 1.0,
            formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
            experimentalFeatures: {
                useBarCodeDetectorIfSupported: true
            }
        };
    }

    /**
     * Initialize the QR scanner
     */
    async initialize() {
        try {
            // Check if Html5Qrcode is available
            if (typeof Html5Qrcode === 'undefined') {
                console.warn('Html5Qrcode library not yet loaded, waiting...');
                await this.waitForHtml5Qrcode();
            }
            
            this.html5QrCode = new Html5Qrcode('qr-reader');
            await this.loadCameras();
            this.emit('initialized');
        } catch (error) {
            console.error('Failed to initialize QR scanner:', error);
            this.emit('error', { type: 'initialization', error });
            throw error;
        }
    }

    /**
     * Wait for Html5Qrcode library to be available
     */
    async waitForHtml5Qrcode() {
        return new Promise((resolve) => {
            const checkHtml5Qrcode = () => {
                if (typeof Html5Qrcode !== 'undefined') {
                    resolve();
                } else {
                    setTimeout(checkHtml5Qrcode, 100);
                }
            };
            checkHtml5Qrcode();
        });
    }

    /**
     * Load available cameras
     */
    async loadCameras() {
        try {
            this.cameras = await Html5Qrcode.getCameras();
            
            if (this.cameras.length === 0) {
                throw new Error('No cameras found');
            }

            // Prefer back camera if available
            const backCamera = this.cameras.find(camera => 
                camera.label && camera.label.toLowerCase().includes('back')
            );
            
            this.currentCameraId = backCamera ? backCamera.id : this.cameras[0].id;
            
            console.log(`Found ${this.cameras.length} camera(s):`, this.cameras);
            this.emit('camerasLoaded', this.cameras);
            
        } catch (error) {
            console.error('Failed to load cameras:', error);
            this.emit('error', { type: 'cameraLoad', error });
            throw error;
        }
    }

    /**
     * Start scanning for QR codes
     */
    async startScanning() {
        if (this.isScanning) {
            console.warn('QR scanner is already running');
            return;
        }

        if (!this.html5QrCode) {
            throw new Error('QR scanner not initialized');
        }

        if (this.cameras.length === 0) {
            throw new Error('No cameras available');
        }

        try {
            this.isScanning = true;
            this.emit('scanningStarted');

            await this.html5QrCode.start(
                this.currentCameraId,
                this.config,
                this.onScanSuccess.bind(this),
                this.onScanFailure.bind(this)
            );

            console.log('QR scanning started with camera:', this.currentCameraId);
            
        } catch (error) {
            this.isScanning = false;
            console.error('Failed to start QR scanning:', error);
            this.emit('error', { type: 'startScanning', error });
            
            // Try to provide helpful error messages
            if (error.name === 'NotAllowedError' || error.message.includes('Permission')) {
                this.emit('permissionDenied');
            } else if (error.name === 'NotFoundError') {
                this.emit('cameraNotFound');
            }
            
            throw error;
        }
    }

    /**
     * Stop scanning for QR codes
     */
    async stopScanning() {
        if (!this.isScanning) {
            return;
        }

        try {
            await this.html5QrCode.stop();
            this.isScanning = false;
            this.emit('scanningStopped');
            console.log('QR scanning stopped');
        } catch (error) {
            console.error('Failed to stop QR scanning:', error);
            this.emit('error', { type: 'stopScanning', error });
            // Force reset state even if stop failed
            this.isScanning = false;
        }
    }

    /**
     * Handle successful QR code scan
     */
    onScanSuccess(decodedText, decodedResult) {
        const now = Date.now();
        
        // Prevent duplicate scans within cooldown period
        if (now - this.lastScanTime < this.scanCooldown) {
            return;
        }

        this.lastScanTime = now;
        
        console.log('QR code scanned:', decodedText);
        
        // Validate that it looks like a session ID (basic validation)
        const sessionId = this.validateSessionId(decodedText);
        
        if (sessionId) {
            this.emit('qrCodeDetected', {
                sessionId,
                rawText: decodedText,
                result: decodedResult,
                timestamp: new Date()
            });
            
            // Auto-stop scanning after successful scan
            this.stopScanning();
        } else {
            console.warn('Invalid session ID format:', decodedText);
            this.emit('invalidQrCode', { text: decodedText });
        }
    }

    /**
     * Handle QR code scan failures (silent, happens frequently)
     */
    onScanFailure(error) {
        // Only log if it's not the common "no QR code found" error
        if (!error.includes('No QR code found')) {
            console.debug('QR scan error:', error);
        }
    }

    /**
     * Validate session ID format
     * @param {string} text - The scanned text
     * @returns {string|null} - Valid session ID or null
     */
    validateSessionId(text) {
        try {
            // Remove any whitespace
            const trimmed = text.trim();
            
            // Check if it's a valid GUID format (session IDs should be GUIDs)
            const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
            
            if (guidRegex.test(trimmed)) {
                return trimmed;
            }

            // Check if it might be a URL containing a session ID
            if (trimmed.includes('session=') || trimmed.includes('sessionId=')) {
                const url = new URL(trimmed);
                const sessionFromUrl = url.searchParams.get('session') || url.searchParams.get('sessionId');
                
                if (sessionFromUrl && guidRegex.test(sessionFromUrl)) {
                    return sessionFromUrl;
                }
            }

            return null;
            
        } catch (error) {
            console.warn('Error validating session ID:', error);
            return null;
        }
    }

    /**
     * Switch to a different camera
     * @param {string} cameraId - ID of the camera to switch to
     */
    async switchCamera(cameraId) {
        if (!this.cameras.find(cam => cam.id === cameraId)) {
            throw new Error('Camera not found');
        }

        const wasScanning = this.isScanning;
        
        if (wasScanning) {
            await this.stopScanning();
        }

        this.currentCameraId = cameraId;
        this.emit('cameraSwitched', { cameraId });

        if (wasScanning) {
            await this.startScanning();
        }
    }

    /**
     * Get list of available cameras
     */
    getCameras() {
        return [...this.cameras];
    }

    /**
     * Get current camera ID
     */
    getCurrentCameraId() {
        return this.currentCameraId;
    }

    /**
     * Check if scanner is currently scanning
     */
    getIsScanning() {
        return this.isScanning;
    }

    /**
     * Get scanner capabilities
     */
    async getCapabilities() {
        if (!this.html5QrCode || !this.isScanning) {
            return null;
        }

        try {
            return await this.html5QrCode.getRunningTrackCapabilities();
        } catch (error) {
            console.warn('Failed to get scanner capabilities:', error);
            return null;
        }
    }

    /**
     * Apply scanner settings
     * @param {Object} settings - Settings to apply
     */
    async applySettings(settings) {
        if (!this.html5QrCode || !this.isScanning) {
            return;
        }

        try {
            await this.html5QrCode.applyVideoConstraints(settings);
            this.emit('settingsApplied', settings);
        } catch (error) {
            console.warn('Failed to apply scanner settings:', error);
            this.emit('error', { type: 'applySettings', error });
        }
    }

    /**
     * Clean up resources
     */
    async cleanup() {
        try {
            if (this.isScanning) {
                await this.stopScanning();
            }
            
            if (this.html5QrCode) {
                await this.html5QrCode.clear();
                this.html5QrCode = null;
            }
            
            this.cameras = [];
            this.currentCameraId = null;
            this.emit('cleanedUp');
            
        } catch (error) {
            console.error('Error during QR scanner cleanup:', error);
            this.emit('error', { type: 'cleanup', error });
        }
    }

    /**
     * Check if QR scanning is supported
     */
    static async isSupported() {
        try {
            return await Html5Qrcode.getCameras().then(cameras => cameras.length > 0);
        } catch (error) {
            console.warn('QR scanning not supported:', error);
            return false;
        }
    }
}

// Export for use in other modules
export { QRScanner };
