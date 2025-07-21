/**
 * SignalR Client for real-time clipboard synchronization
 * Handles connection management, event dispatching, and error recovery
 */

import { EventEmitter } from './utils.js';

class SignalRClient extends EventEmitter {
    constructor() {
        super();
        this.connection = null;
        this.connectionState = 'Disconnected';
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectDelay = 1000; // Start with 1 second
        this.isManualDisconnect = false;
        this.sessionId = null;
        
        this.setupConnection();
    }

    /**
     * Initialize SignalR connection
     */
    setupConnection() {
        try {
            // Check if SignalR is available
            if (typeof signalR === 'undefined') {
                console.warn('SignalR library not yet loaded, waiting...');
                // Wait for SignalR to be available
                const checkSignalR = () => {
                    if (typeof signalR !== 'undefined') {
                        console.log('SignalR library is now available');
                        this.createConnection();
                    } else {
                        setTimeout(checkSignalR, 100);
                    }
                };
                checkSignalR();
                return;
            }
            
            console.log('SignalR library is available, creating connection');
            this.createConnection();
        } catch (error) {
            console.error('Failed to setup SignalR connection:', error);
            this.emit('error', { type: 'setup', error });
        }
    }

    /**
     * Create the actual SignalR connection
     */
    createConnection() {
        try {
            console.log('Creating SignalR connection...');
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/clipboardhub', {
                    withCredentials: false,
                    transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
                })
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        // Exponential backoff with jitter
                        const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                        const jitter = Math.random() * 1000;
                        return delay + jitter;
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            console.log('SignalR connection created successfully');
            this.setupEventHandlers();
            this.setupHubMethods();
        } catch (error) {
            console.error('Failed to create SignalR connection:', error);
            this.emit('error', { type: 'setup', error });
        }
    }

    /**
     * Setup connection event handlers
     */
    setupEventHandlers() {
        // Connection state changes
        this.connection.onclose(async (error) => {
            this.connectionState = 'Disconnected';
            this.emit('connectionStateChanged', this.connectionState);
            
            if (error) {
                console.error('SignalR connection closed with error:', error);
                this.emit('error', { type: 'connection', error });
            }

            // Auto-reconnect if not manually disconnected
            if (!this.isManualDisconnect) {
                await this.attemptReconnect();
            }
        });

        this.connection.onreconnecting((error) => {
            this.connectionState = 'Reconnecting';
            this.emit('connectionStateChanged', this.connectionState);
            
            if (error) {
                console.warn('SignalR reconnecting due to error:', error);
            }
        });

        this.connection.onreconnected((connectionId) => {
            this.connectionState = 'Connected';
            this.reconnectAttempts = 0;
            this.reconnectDelay = 1000;
            this.emit('connectionStateChanged', this.connectionState);
            this.emit('reconnected', connectionId);
            
            console.log('SignalR reconnected with ID:', connectionId);
            
            // Rejoin session if we have one
            if (this.sessionId) {
                this.joinSession(this.sessionId);
            }
        });
    }

    /**
     * Setup hub method handlers
     */
    setupHubMethods() {
        // Handle clipboard content received from other devices
        this.connection.on('ClipboardContentReceived', (content, deviceName, timestamp) => {
            console.log('Received clipboard content from:', deviceName);
            this.emit('clipboardReceived', {
                content,
                deviceName,
                timestamp: new Date(timestamp),
                isFromOtherDevice: true
            });
        });

        // Handle device joined notifications
        this.connection.on('DeviceJoined', (deviceName) => {
            console.log('Device joined session:', deviceName);
            this.emit('deviceJoined', { deviceName });
        });

        // Handle device left notifications
        this.connection.on('DeviceLeft', (deviceName) => {
            console.log('Device left session:', deviceName);
            this.emit('deviceLeft', { deviceName });
        });

        // Handle session info updates
        this.connection.on('SessionInfoUpdated', (sessionInfo) => {
            console.log('Session info updated:', sessionInfo);
            this.emit('sessionInfoUpdated', sessionInfo);
        });

        // Handle errors from hub
        this.connection.on('Error', (message) => {
            console.error('Hub error:', message);
            this.emit('error', { type: 'hub', message });
        });
    }

    /**
     * Start the SignalR connection
     */
    async start() {
        if (!this.connection) {
            console.error('SignalR connection not initialized. Cannot start.');
            this.connectionState = 'Disconnected';
            this.emit('connectionStateChanged', this.connectionState);
            return;
        }

        if (this.connection.state === signalR.HubConnectionState.Connected) {
            console.log('SignalR already connected');
            this.connectionState = 'Connected';
            this.emit('connectionStateChanged', this.connectionState);
            return;
        }

        try {
            console.log('Starting SignalR connection...');
            this.connectionState = 'Connecting';
            this.emit('connectionStateChanged', this.connectionState);
            
            await this.connection.start();
            
            this.connectionState = 'Connected';
            this.reconnectAttempts = 0;
            this.reconnectDelay = 1000;
            this.emit('connectionStateChanged', this.connectionState);
            this.emit('connected');
            
            console.log('SignalR connected successfully');
        } catch (error) {
            this.connectionState = 'Disconnected';
            this.emit('connectionStateChanged', this.connectionState);
            console.error('Failed to start SignalR connection:', error);
            this.emit('error', { type: 'start', error });
            
            // Attempt reconnect after delay
            setTimeout(() => this.attemptReconnect(), this.reconnectDelay);
        }
    }

    /**
     * Stop the SignalR connection
     */
    async stop() {
        if (this.connection.state === signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            this.isManualDisconnect = true;
            await this.connection.stop();
            this.connectionState = 'Disconnected';
            this.emit('connectionStateChanged', this.connectionState);
            console.log('SignalR connection stopped');
        } catch (error) {
            console.error('Error stopping SignalR connection:', error);
            this.emit('error', { type: 'stop', error });
        } finally {
            this.isManualDisconnect = false;
        }
    }

    /**
     * Attempt to reconnect with exponential backoff
     */
    async attemptReconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error('Max reconnect attempts reached');
            this.emit('maxReconnectAttemptsReached');
            return;
        }

        this.reconnectAttempts++;
        console.log(`Reconnect attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts} in ${this.reconnectDelay}ms`);
        
        setTimeout(async () => {
            try {
                await this.start();
            } catch (error) {
                // Exponential backoff
                this.reconnectDelay = Math.min(this.reconnectDelay * 2, 30000);
                await this.attemptReconnect();
            }
        }, this.reconnectDelay);
    }

    /**
     * Join a clipboard session
     * @param {string} sessionId - Session ID to join
     * @param {string} deviceName - Name of this device
     */
    async joinSession(sessionId, deviceName = null) {
        if (this.connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Not connected to SignalR hub');
        }

        try {
            this.sessionId = sessionId;
            await this.connection.invoke('JoinSession', sessionId, deviceName);
            console.log('Joined session:', sessionId);
            this.emit('sessionJoined', { sessionId, deviceName });
        } catch (error) {
            console.error('Failed to join session:', error);
            this.emit('error', { type: 'joinSession', error });
            throw error;
        }
    }

    /**
     * Leave the current session
     */
    async leaveSession() {
        if (this.connection.state !== signalR.HubConnectionState.Connected || !this.sessionId) {
            return;
        }

        try {
            await this.connection.invoke('LeaveSession');
            const previousSessionId = this.sessionId;
            this.sessionId = null;
            console.log('Left session:', previousSessionId);
            this.emit('sessionLeft', { sessionId: previousSessionId });
        } catch (error) {
            console.error('Failed to leave session:', error);
            this.emit('error', { type: 'leaveSession', error });
            throw error;
        }
    }

    /**
     * Send clipboard content to other devices in session
     * @param {string} content - Clipboard content to send
     */
    async sendClipboardContent(content) {
        if (this.connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Not connected to SignalR hub');
        }

        if (!this.sessionId) {
            throw new Error('Not in a session');
        }

        try {
            await this.connection.invoke('SendClipboardContent', content);
            console.log('Sent clipboard content to session');
            this.emit('clipboardSent', { content });
        } catch (error) {
            console.error('Failed to send clipboard content:', error);
            this.emit('error', { type: 'sendClipboard', error });
            throw error;
        }
    }

    /**
     * Get current connection state
     */
    getConnectionState() {
        return this.connectionState;
    }

    /**
     * Check if connected
     */
    isConnected() {
        return this.connection && this.connection.state === signalR.HubConnectionState.Connected;
    }

    /**
     * Get current session ID
     */
    getCurrentSessionId() {
        return this.sessionId;
    }

    /**
     * Force a manual reconnect
     */
    async reconnect() {
        this.reconnectAttempts = 0;
        this.reconnectDelay = 1000;
        await this.stop();
        await this.start();
    }
}

// Export for use in other modules
export { SignalRClient };
