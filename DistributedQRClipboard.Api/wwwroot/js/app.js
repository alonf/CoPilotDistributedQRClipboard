/**
 * Main Application Logic
 * Orchestrates all components and handles user interactions
 */

import { ApiClient } from './api.js';
import { SignalRClient } from './signalr-client.js';
import { QRScanner } from './qr-scanner.js';
import { ClipboardManager } from './clipboard.js';
import { 
    Toast, 
    Loading, 
    escapeHtml, 
    generateUUID,
    Theme 
} from './utils.js';

console.log('DistributedQRClipboard app.js loading...');

class DistributedQRClipboardApp {
    constructor() {
        console.log('DistributedQRClipboardApp constructor called');
        this.apiClient = null;
        this.signalRClient = null;
        this.qrScanner = null;
        this.clipboardManager = null;
        this.currentSession = null;
        this.deviceName = '';
        this.isInitialized = false;
        this.preloadedQRCode = null; // Store QR code from session creation
        
        // UI elements
        this.elements = {};
        
        // State
        this.connectedDevices = new Set();
        this.lastQRGenerated = null;
        this.sessionHistory = new Map();
        
        console.log('About to bind events...');
        this.bindEvents();
        console.log('Events bound successfully');
    }

    /**
     * Initialize the application
     */
    async initialize() {
        if (this.isInitialized) {
            return;
        }

        try {
            Loading.show('Initializing application...');
            
            // Initialize UI elements
            this.initializeUIElements();
            
            // Set device name
            this.deviceName = this.generateDeviceName();
            
            // Initialize API client
            this.apiClient = new ApiClient();
            
            // Initialize SignalR client
            this.signalRClient = new SignalRClient();
            this.setupSignalREventHandlers();
            
            // Initialize clipboard manager
            this.clipboardManager = new ClipboardManager(this.apiClient, this.signalRClient);
            this.setupClipboardEventHandlers();
            
            // Initialize QR scanner
            this.qrScanner = new QRScanner();
            this.setupQRScannerEventHandlers();
            await this.qrScanner.initialize();
            
            // Start SignalR connection
            await this.signalRClient.start();
            
            // Update UI state
            this.updateConnectionStatus();
            this.updateDeviceInfo();
            this.checkClipboardSupport();
            
            // Set up periodic connection status checks
            setInterval(() => {
                this.updateConnectionStatus();
            }, 2000);
            
            this.isInitialized = true;
            Loading.hide();
            
            Toast.show('Application initialized successfully', 'success');
            console.log('Distributed QR Clipboard app initialized');
            
        } catch (error) {
            console.error('Failed to initialize app:', error);
            Loading.hide();
            Toast.show('Failed to initialize application', 'error');
            throw error;
        }
    }

    /**
     * Initialize UI element references
     */
    initializeUIElements() {
        console.log('Initializing UI elements...');
        try {
            this.elements = {
                // Connection status
                connectionStatus: document.getElementById('connection-status'),
                connectionText: document.getElementById('connection-text'),
                retryConnection: document.getElementById('retry-connection'),
                
                // Session info
                sessionId: document.getElementById('session-id'),
                deviceName: document.getElementById('device-name'),
                connectedDevicesCount: document.getElementById('connected-devices'),
                
                // Actions
                createSessionBtn: document.getElementById('create-session'),
                joinSessionBtn: document.getElementById('join-session'),
                leaveSessionBtn: document.getElementById('leave-session'),
                scanQRBtn: document.getElementById('scan-qr'),
                generateQRBtn: document.getElementById('generate-qr'),
                
                // Clipboard
                clipboardContent: document.getElementById('clipboard-content'),
                clipboardSend: document.getElementById('clipboard-send'),
                clipboardClear: document.getElementById('clipboard-clear'),
                clipboardRead: document.getElementById('clipboard-read'),
                clipboardMonitor: document.getElementById('clipboard-monitor'),
                
                // QR Code
                qrCodeContainer: document.getElementById('qr-code'),
                qrReader: document.getElementById('qr-reader'),
                joinUrlInput: document.getElementById('join-url-input'),
                copyUrlBtn: document.getElementById('copy-url-btn'),
                
                // Modals
                joinSessionModal: document.getElementById('join-session-modal'),
                joinSessionInput: document.getElementById('join-session-input'),
                joinSessionConfirm: document.getElementById('join-session-confirm'),
                scanQRModal: document.getElementById('scan-qr-modal'),
                qrCodeModal: document.getElementById('qr-code-modal'),
                
                // Device activity
                deviceList: document.getElementById('device-list'),
                activityList: document.getElementById('activity-list'),
                
                // Theme toggle
                themeToggle: document.getElementById('theme-toggle')
            };
            
            // Check if critical elements exist
            if (!this.elements.connectionStatus) {
                console.error('Critical UI element missing: connection-status');
            }
            if (!this.elements.connectionText) {
                console.error('Critical UI element missing: connection-text');
            }
            if (!this.elements.createSessionBtn) {
                console.error('Critical UI element missing: create-session');
            }
            
            console.log('✅ UI elements initialized successfully');
        } catch (error) {
            console.error('Failed to initialize UI elements:', error);
            throw error;
        }
    }

    /**
     * Bind UI event handlers
     */
    bindEvents() {
        // Check if DOM is already loaded
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                this.initialize();
                this.setupEventHandlers();
            });
        } else {
            // DOM is already loaded
            setTimeout(() => {
                this.initialize();
                this.setupEventHandlers();
            }, 0);
        }
    }

    /**
     * Set up event handlers after DOM is ready
     */
    setupEventHandlers() {
        // Use event delegation for dynamic elements
        document.addEventListener('click', this.handleClick.bind(this));
        document.addEventListener('input', this.handleInput.bind(this));
        document.addEventListener('change', this.handleChange.bind(this));
        
        // Keyboard shortcuts
        document.addEventListener('keydown', this.handleKeyDown.bind(this));
        
        // Window events
        window.addEventListener('beforeunload', this.handleBeforeUnload.bind(this));
        window.addEventListener('focus', this.handleWindowFocus.bind(this));
        window.addEventListener('blur', this.handleWindowBlur.bind(this));
        
        console.log('Event handlers set up successfully');
    }

    /**
     * Handle click events
     */
    async handleClick(event) {
        const target = event.target;
        const action = target.dataset.action || target.id;
        
        console.log('Click event captured:', action, 'on element:', target);
        
        try {
            switch (action) {
                case 'create-session':
                    console.log('Creating session...');
                    alert('Button clicked! About to create session...');
                    event.preventDefault();
                    try {
                        await this.createSession();
                    } catch (error) {
                        console.error('Failed to create session:', error);
                        alert('Failed to create session: ' + error.message);
                    }
                    break;
                    
                case 'join-session':
                    this.showJoinSessionModal();
                    break;
                    
                case 'join-session-confirm':
                    await this.joinSessionFromModal();
                    break;
                    
                case 'leave-session':
                    await this.leaveSession();
                    break;
                    
                case 'scan-qr':
                    this.showScanQRModal();
                    break;
                    
                case 'generate-qr':
                    this.showQRCodeModal();
                    break;
                    
                case 'clipboard-send':
                    await this.sendClipboardContent();
                    break;
                    
                case 'clipboard-clear':
                    await this.clearClipboard();
                    break;
                    
                case 'clipboard-read':
                    await this.readClipboard();
                    break;
                    
                case 'clipboard-monitor':
                    this.toggleClipboardMonitoring();
                    break;
                    
                case 'retry-connection':
                    await this.retryConnection();
                    break;
                    
                case 'theme-toggle':
                    toggleTheme();
                    break;
                    
                case 'copy-url-btn':
                    await this.copyJoinUrl();
                    break;
                    
                default:
                    // Handle modal close buttons
                    if (target.classList.contains('modal-close') || target.classList.contains('modal-overlay')) {
                        this.closeModals();
                    }
                    break;
            }
        } catch (error) {
            console.error('Error handling click:', error);
            Toast.show('An error occurred', 'error');
        }
    }

    /**
     * Handle input events
     */
    handleInput(event) {
        const target = event.target;
        
        if (target.id === 'clipboard-content') {
            // Auto-resize textarea
            target.style.height = 'auto';
            target.style.height = target.scrollHeight + 'px';
        }
    }

    /**
     * Handle change events
     */
    handleChange(event) {
        // Handle any form changes if needed
    }

    /**
     * Handle keyboard shortcuts
     */
    handleKeyDown(event) {
        // Ctrl/Cmd + Enter to send clipboard
        if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
            if (this.elements.clipboardContent === document.activeElement) {
                event.preventDefault();
                this.sendClipboardContent();
            }
        }
        
        // Escape to close modals
        if (event.key === 'Escape') {
            this.closeModals();
        }
    }

    /**
     * Handle window focus
     */
    handleWindowFocus() {
        // Re-check clipboard when window regains focus
        if (this.clipboardManager && !this.clipboardManager.isMonitoringActive()) {
            this.clipboardManager.checkLocalClipboard();
        }
    }

    /**
     * Handle window blur
     */
    handleWindowBlur() {
        // Could pause some activities when window loses focus
    }

    /**
     * Handle before unload
     */
    handleBeforeUnload() {
        this.cleanup();
    }

    /**
     * Setup SignalR event handlers
     */
    setupSignalREventHandlers() {
        this.signalRClient.on('connectionStateChanged', (state) => {
            this.updateConnectionStatus(state);
        });

        this.signalRClient.on('sessionJoined', (data) => {
            this.currentSession = data.sessionId;
            this.updateSessionInfo();
            this.addActivity(`Joined session: ${data.sessionId}`, 'session');
            Toast.show(`Joined session successfully`, 'success');
        });

        this.signalRClient.on('sessionLeft', (data) => {
            this.currentSession = null;
            this.connectedDevices.clear();
            this.updateSessionInfo();
            this.updateDeviceList();
            this.addActivity(`Left session: ${data.sessionId}`, 'session');
            Toast.show('Left session', 'info');
        });

        this.signalRClient.on('deviceJoined', (data) => {
            this.connectedDevices.add(data.deviceName);
            this.updateDeviceList();
            this.addActivity(`${data.deviceName} joined`, 'device');
            Toast.show(`${data.deviceName} joined the session`, 'info');
        });

        this.signalRClient.on('deviceLeft', (data) => {
            this.connectedDevices.delete(data.deviceName);
            this.updateDeviceList();
            this.addActivity(`${data.deviceName} left`, 'device');
            Toast.show(`${data.deviceName} left the session`, 'info');
        });

        this.signalRClient.on('error', (error) => {
            console.error('SignalR error:', error);
            Toast.show('Connection error occurred', 'error');
        });
    }

    /**
     * Setup clipboard event handlers
     */
    setupClipboardEventHandlers() {
        this.clipboardManager.on('localClipboardChanged', (data) => {
            this.updateClipboardUI(data.content);
            this.addActivity('Clipboard content changed', 'clipboard');
        });

        this.clipboardManager.on('remoteClipboardReceived', (data) => {
            this.updateClipboardUI(data.content);
            this.addActivity(`Received from ${data.deviceName}`, 'clipboard');
            Toast.show(`Clipboard updated from ${data.deviceName}`, 'success');
        });

        this.clipboardManager.on('syncCompleted', () => {
            Toast.show('Clipboard synced to devices', 'success');
        });

        this.clipboardManager.on('syncFailed', (data) => {
            Toast.show('Failed to sync clipboard', 'error');
        });

        this.clipboardManager.on('permissionDenied', () => {
            Toast.show('Clipboard permission required', 'warning');
        });
    }

    /**
     * Setup QR scanner event handlers
     */
    setupQRScannerEventHandlers() {
        this.qrScanner.on('qrCodeDetected', async (data) => {
            this.closeModals();
            await this.joinSession(data.sessionId);
        });

        this.qrScanner.on('invalidQrCode', () => {
            Toast.show('Invalid QR code format', 'warning');
        });

        this.qrScanner.on('error', (error) => {
            console.error('QR Scanner error:', error);
            if (error.type === 'permissionDenied') {
                Toast.show('Camera permission required for QR scanning', 'error');
            } else {
                Toast.show('QR scanner error', 'error');
            }
        });
    }

    /**
     * Create a new session
     */
    async createSession() {
        try {
            Loading.show('Creating session...');
            
            const response = await this.apiClient.createSession();
            console.log('Create session response:', response);
            
            // Extract session ID from the response structure
            const sessionId = response.sessionInfo?.sessionId;
            if (!sessionId) {
                throw new Error('No session ID returned from server');
            }
            
            // If QR code is already provided in the response, use it
            if (response.qrCodeBase64) {
                this.preloadedQRCode = response.qrCodeBase64;
                console.log('QR code received from session creation');
            }
            
            await this.joinSession(sessionId);
            
        } catch (error) {
            console.error('Failed to create session:', error);
            Toast.show('Failed to create session', 'error');
        } finally {
            Loading.hide();
        }
    }

    /**
     * Join a session
     */
    async joinSession(sessionId) {
        try {
            Loading.show('Joining session...');
            
            await this.signalRClient.joinSession(sessionId, this.deviceName);
            
            // Start clipboard monitoring
            this.clipboardManager.startMonitoring();
            
        } catch (error) {
            console.error('Failed to join session:', error);
            Toast.show('Failed to join session', 'error');
        } finally {
            Loading.hide();
        }
    }

    /**
     * Leave current session
     */
    async leaveSession() {
        try {
            // Stop clipboard monitoring
            this.clipboardManager.stopMonitoring();
            
            await this.signalRClient.leaveSession();
            
        } catch (error) {
            console.error('Failed to leave session:', error);
            Toast.show('Failed to leave session', 'error');
        }
    }

    /**
     * Show join session modal
     */
    showJoinSessionModal() {
        this.elements.joinSessionModal.classList.add('active');
        this.elements.joinSessionInput.focus();
    }

    /**
     * Join session from modal input
     */
    async joinSessionFromModal() {
        const sessionId = this.elements.joinSessionInput.value.trim();
        
        if (!sessionId) {
            Toast.show('Please enter a session ID', 'warning');
            return;
        }
        
        this.closeModals();
        await this.joinSession(sessionId);
    }

    /**
     * Show scan QR modal
     */
    async showScanQRModal() {
        this.elements.scanQRModal.classList.add('active');
        
        try {
            await this.qrScanner.startScanning();
        } catch (error) {
            this.closeModals();
            Toast.show('Failed to start QR scanner', 'error');
        }
    }

    /**
     * Show QR code modal
     */
    async showQRCodeModal() {
        if (!this.currentSession) {
            Toast.show('Not in a session', 'warning');
            return;
        }

        try {
            const qrCode = await this.apiClient.generateQrCode(this.currentSession);
            this.elements.qrCodeContainer.innerHTML = `<img src="data:image/png;base64,${qrCode}" alt="Session QR Code">`;
            this.elements.qrCodeModal.classList.add('active');
            this.lastQRGenerated = new Date();
            
        } catch (error) {
            console.error('Failed to generate QR code:', error);
            Toast.show('Failed to generate QR code', 'error');
        }
    }

    /**
     * Close all modals
     */
    closeModals() {
        document.querySelectorAll('.modal').forEach(modal => {
            modal.classList.remove('active');
        });
        
        // Stop QR scanning if active
        if (this.qrScanner && this.qrScanner.getIsScanning()) {
            this.qrScanner.stopScanning();
        }
    }

    /**
     * Send clipboard content
     */
    async sendClipboardContent() {
        const content = this.elements.clipboardContent.value.trim();
        
        if (!content) {
            Toast.show('No content to send', 'warning');
            return;
        }

        if (!this.currentSession) {
            Toast.show('Not in a session', 'warning');
            return;
        }

        try {
            await this.clipboardManager.setClipboardContent(content);
            this.addActivity('Sent clipboard content', 'clipboard');
            
        } catch (error) {
            console.error('Failed to send clipboard:', error);
            Toast.show('Failed to send clipboard content', 'error');
        }
    }

    /**
     * Clear clipboard content
     */
    async clearClipboard() {
        try {
            await this.clipboardManager.clearClipboard();
            this.elements.clipboardContent.value = '';
            this.addActivity('Cleared clipboard', 'clipboard');
            Toast.show('Clipboard cleared', 'info');
            
        } catch (error) {
            console.error('Failed to clear clipboard:', error);
            Toast.show('Failed to clear clipboard', 'error');
        }
    }

    /**
     * Read clipboard content
     */
    async readClipboard() {
        try {
            const content = await this.clipboardManager.readClipboard();
            this.updateClipboardUI(content);
            this.addActivity('Read clipboard content', 'clipboard');
            
        } catch (error) {
            console.error('Failed to read clipboard:', error);
            Toast.show('Failed to read clipboard', 'error');
        }
    }

    /**
     * Toggle clipboard monitoring
     */
    toggleClipboardMonitoring() {
        if (this.clipboardManager.isMonitoringActive()) {
            this.clipboardManager.stopMonitoring();
            this.elements.clipboardMonitor.textContent = 'Start Monitoring';
            this.elements.clipboardMonitor.classList.remove('active');
            Toast.show('Clipboard monitoring stopped', 'info');
        } else {
            this.clipboardManager.startMonitoring();
            this.elements.clipboardMonitor.textContent = 'Stop Monitoring';
            this.elements.clipboardMonitor.classList.add('active');
            Toast.show('Clipboard monitoring started', 'success');
        }
    }

    /**
     * Retry connection
     */
    async retryConnection() {
        try {
            await this.signalRClient.reconnect();
        } catch (error) {
            console.error('Failed to reconnect:', error);
            Toast.show('Failed to reconnect', 'error');
        }
    }

    /**
     * Update connection status UI
     */
    updateConnectionStatus(state = null) {
        const currentState = state || this.signalRClient?.getConnectionState() || 'Disconnected';
        
        console.log('Updating connection status to:', currentState);
        
        this.elements.connectionStatus.className = `connection-status ${currentState.toLowerCase()}`;
        this.elements.connectionText.textContent = currentState;
        
        if (currentState === 'Disconnected') {
            this.elements.retryConnection.style.display = 'inline-block';
        } else {
            this.elements.retryConnection.style.display = 'none';
        }
        
        // Force update to Connected if SignalR reports connected
        if (this.signalRClient && this.signalRClient.isConnected && this.signalRClient.isConnected()) {
            console.log('SignalR reports connected, updating UI to Connected');
            this.elements.connectionStatus.className = 'connection-status connected';
            this.elements.connectionText.textContent = 'Connected';
            this.elements.retryConnection.style.display = 'none';
        }
    }

    /**
     * Update session info UI
     */
    updateSessionInfo() {
        console.log('Updating session info, current session:', this.currentSession);
        
        if (this.currentSession) {
            this.elements.sessionId.textContent = this.currentSession;
            this.elements.connectedDevicesCount.textContent = this.connectedDevices.size;
            
            // Show session controls
            document.querySelectorAll('.session-active').forEach(el => el.style.display = 'block');
            document.querySelectorAll('.session-inactive').forEach(el => el.style.display = 'none');
            
            // Automatically generate and show QR code for the session
            console.log('Calling generateAndShowQRCode...');
            this.generateAndShowQRCode();
        } else {
            this.elements.sessionId.textContent = 'Not connected';
            this.elements.connectedDevicesCount.textContent = '0';
            
            // Hide session controls
            document.querySelectorAll('.session-active').forEach(el => el.style.display = 'none');
            document.querySelectorAll('.session-inactive').forEach(el => el.style.display = 'block');
            
            // Clear QR code and URL when no session
            this.clearQRCode();
        }
    }

    /**
     * Generate and show QR code for current session
     */
    async generateAndShowQRCode() {
        if (!this.currentSession) {
            console.log('No current session, skipping QR generation');
            return;
        }

        console.log('Generating QR code for session:', this.currentSession);

        try {
            let qrCode = this.preloadedQRCode;
            
            // If no preloaded QR code, fetch it from the API
            if (!qrCode) {
                console.log('No preloaded QR code, fetching from API...');
                qrCode = await this.apiClient.generateQrCode(this.currentSession);
            } else {
                console.log('Using preloaded QR code');
                this.preloadedQRCode = null; // Clear it after use
            }
            
            const joinUrl = `${window.location.origin}?session=${this.currentSession}`;
            
            // Update QR code image
            this.elements.qrCodeContainer.innerHTML = `<img src="data:image/png;base64,${qrCode}" alt="Session QR Code" style="max-width: 100%; height: auto;">`;
            console.log('QR code image updated');
            
            // Update join URL
            this.elements.joinUrlInput.value = joinUrl;
            this.elements.copyUrlBtn.style.display = 'inline-flex';
            
            this.lastQRGenerated = new Date();
            Toast.show('QR code generated successfully', 'success');
            
        } catch (error) {
            console.error('Failed to generate QR code:', error);
            Toast.show('Failed to generate QR code', 'error');
            
            // Show error in QR container
            this.elements.qrCodeContainer.innerHTML = `
                <div class="qr-error">
                    <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                        <circle cx="12" cy="12" r="10"/>
                        <line x1="15" y1="9" x2="9" y2="15"/>
                        <line x1="9" y1="9" x2="15" y2="15"/>
                    </svg>
                    <p>Failed to generate QR code</p>
                </div>`;
        }
    }

    /**
     * Clear QR code and related UI elements
     */
    clearQRCode() {
        const placeholder = document.getElementById('qr-placeholder');
        if (placeholder) {
            this.elements.qrCodeContainer.innerHTML = placeholder.outerHTML;
        }
        this.elements.joinUrlInput.value = '';
        this.elements.copyUrlBtn.style.display = 'none';
    }

    /**
     * Copy join URL to clipboard
     */
    async copyJoinUrl() {
        try {
            const url = this.elements.joinUrlInput.value;
            if (!url) {
                Toast.show('No URL to copy', 'warning');
                return;
            }

            await navigator.clipboard.writeText(url);
            Toast.show('Join URL copied to clipboard', 'success');
        } catch (error) {
            console.error('Failed to copy URL:', error);
            
            // Fallback for older browsers
            try {
                this.elements.joinUrlInput.select();
                document.execCommand('copy');
                Toast.show('Join URL copied to clipboard', 'success');
            } catch (fallbackError) {
                console.error('Fallback copy failed:', fallbackError);
                Toast.show('Failed to copy URL', 'error');
            }
        }
    }

    /**
     * Update device info UI
     */
    updateDeviceInfo() {
        this.elements.deviceName.textContent = this.deviceName;
    }

    /**
     * Update clipboard UI
     */
    updateClipboardUI(content) {
        this.elements.clipboardContent.value = content;
        
        // Auto-resize textarea
        this.elements.clipboardContent.style.height = 'auto';
        this.elements.clipboardContent.style.height = this.elements.clipboardContent.scrollHeight + 'px';
    }

    /**
     * Update device list UI
     */
    updateDeviceList() {
        const deviceListHtml = Array.from(this.connectedDevices).map(device => 
            `<div class="device-item">
                <span class="device-name">${escapeHtml(device)}</span>
                <span class="device-status online">Online</span>
            </div>`
        ).join('');
        
        this.elements.deviceList.innerHTML = deviceListHtml || '<div class="no-devices">No other devices connected</div>';
    }

    /**
     * Add activity to the activity list
     */
    addActivity(message, type = 'info') {
        const timestamp = new Date().toLocaleTimeString();
        const activityHtml = `
            <div class="activity-item ${type}">
                <span class="activity-time">${timestamp}</span>
                <span class="activity-message">${escapeHtml(message)}</span>
            </div>
        `;
        
        this.elements.activityList.insertAdjacentHTML('afterbegin', activityHtml);
        
        // Limit to 50 activities
        const activities = this.elements.activityList.children;
        while (activities.length > 50) {
            activities[activities.length - 1].remove();
        }
    }

    /**
     * Check clipboard support and show warnings
     */
    checkClipboardSupport() {
        const support = this.clipboardManager.getSupport();
        
        if (!support.full) {
            const message = !support.read && !support.write 
                ? 'Clipboard access not supported in this browser'
                : `Clipboard ${!support.read ? 'read' : 'write'} not supported`;
                
            Toast.show(message, 'warning');
        }
    }

    /**
     * Generate a device name
     */
    generateDeviceName() {
        const platform = navigator.platform || 'Unknown';
        const browser = this.getBrowserName();
        const random = Math.random().toString(36).substr(2, 5);
        
        return `${platform}-${browser}-${random}`;
    }

    /**
     * Get browser name
     */
    getBrowserName() {
        const userAgent = navigator.userAgent;
        
        if (userAgent.includes('Chrome')) return 'Chrome';
        if (userAgent.includes('Firefox')) return 'Firefox';
        if (userAgent.includes('Safari')) return 'Safari';
        if (userAgent.includes('Edge')) return 'Edge';
        
        return 'Browser';
    }

    /**
     * Cleanup resources
     */
    cleanup() {
        if (this.clipboardManager) {
            this.clipboardManager.cleanup();
        }
        
        if (this.qrScanner) {
            this.qrScanner.cleanup();
        }
        
        if (this.signalRClient) {
            this.signalRClient.stop();
        }
        
        console.log('Application cleaned up');
    }
}

// Initialize app when DOM is ready
console.log('Creating DistributedQRClipboardApp instance...');
const app = new DistributedQRClipboardApp();

// Export for debugging
window.app = app;

console.log('App instance created and exported to window.app');
console.log('App instance created and exported to window.app');
