/**
 * Clipboard Manager for handling clipboard operations
 * Manages local clipboard content, sync with server, and cross-device sharing
 */

import { EventEmitter } from './utils.js';

class ClipboardManager extends EventEmitter {
    constructor(apiClient, signalRClient) {
        super();
        this.apiClient = apiClient;
        this.signalRClient = signalRClient;
        this.currentContent = '';
        this.lastSyncTime = null;
        this.isMonitoring = false;
        this.monitorInterval = null;
        this.monitoringDelay = 1000; // Check every second
        this.lastLocalContent = '';
        this.syncInProgress = false;
        
        this.setupSignalRListeners();
        this.checkClipboardSupport();
    }

    /**
     * Check if Clipboard API is supported
     */
    checkClipboardSupport() {
        this.hasClipboardRead = !!(navigator.clipboard && navigator.clipboard.readText);
        this.hasClipboardWrite = !!(navigator.clipboard && navigator.clipboard.writeText);
        
        if (!this.hasClipboardRead || !this.hasClipboardWrite) {
            console.warn('Clipboard API not fully supported');
            this.emit('clipboardNotSupported', {
                read: this.hasClipboardRead,
                write: this.hasClipboardWrite
            });
        }
    }

    /**
     * Setup SignalR event listeners
     */
    setupSignalRListeners() {
        this.signalRClient.on('clipboardReceived', this.handleRemoteClipboardContent.bind(this));
        this.signalRClient.on('connected', () => {
            console.log('SignalR connected, clipboard sync enabled');
        });
        this.signalRClient.on('disconnected', () => {
            console.log('SignalR disconnected, clipboard sync disabled');
        });
    }

    /**
     * Start monitoring local clipboard for changes
     */
    startMonitoring() {
        if (this.isMonitoring || !this.hasClipboardRead) {
            return;
        }

        this.isMonitoring = true;
        this.emit('monitoringStarted');
        
        console.log('Started clipboard monitoring');
        
        // Initial read
        this.checkLocalClipboard();
        
        // Set up interval monitoring
        this.monitorInterval = setInterval(() => {
            this.checkLocalClipboard();
        }, this.monitoringDelay);
    }

    /**
     * Stop monitoring local clipboard
     */
    stopMonitoring() {
        if (!this.isMonitoring) {
            return;
        }

        this.isMonitoring = false;
        
        if (this.monitorInterval) {
            clearInterval(this.monitorInterval);
            this.monitorInterval = null;
        }
        
        this.emit('monitoringStopped');
        console.log('Stopped clipboard monitoring');
    }

    /**
     * Check local clipboard for changes
     */
    async checkLocalClipboard() {
        if (!this.hasClipboardRead || this.syncInProgress) {
            return;
        }

        try {
            const content = await navigator.clipboard.readText();
            
            // Check if content has changed
            if (content !== this.lastLocalContent && content.trim() !== '') {
                this.lastLocalContent = content;
                this.handleLocalClipboardChange(content);
            }
            
        } catch (error) {
            // Ignore permission errors during monitoring (common when tab is not focused)
            if (error.name !== 'NotAllowedError') {
                console.warn('Error reading clipboard:', error);
            }
        }
    }

    /**
     * Handle local clipboard content change
     */
    async handleLocalClipboardChange(content) {
        console.log('Local clipboard changed');
        
        this.currentContent = content;
        this.lastSyncTime = new Date();
        
        this.emit('localClipboardChanged', {
            content,
            timestamp: this.lastSyncTime
        });

        // Sync to server if in a session
        if (this.signalRClient.getCurrentSessionId()) {
            await this.syncToRemote(content);
        }
    }

    /**
     * Handle remote clipboard content received
     */
    async handleRemoteClipboardContent(data) {
        const { content, deviceName, timestamp, isFromOtherDevice } = data;
        
        if (!isFromOtherDevice) {
            return; // Ignore our own content
        }

        console.log(`Received clipboard content from ${deviceName}`);
        
        this.currentContent = content;
        this.lastLocalContent = content; // Prevent triggering our own change detection
        this.lastSyncTime = timestamp;
        
        this.emit('remoteClipboardReceived', {
            content,
            deviceName,
            timestamp
        });

        // Write to local clipboard
        await this.writeToLocalClipboard(content);
    }

    /**
     * Sync clipboard content to remote devices
     */
    async syncToRemote(content) {
        if (this.syncInProgress || !this.signalRClient.isConnected()) {
            return;
        }

        try {
            this.syncInProgress = true;
            this.emit('syncStarted', { content });
            
            await this.signalRClient.sendClipboardContent(content);
            
            this.emit('syncCompleted', { 
                content, 
                timestamp: new Date() 
            });
            
        } catch (error) {
            console.error('Failed to sync clipboard to remote:', error);
            this.emit('syncFailed', { content, error });
        } finally {
            this.syncInProgress = false;
        }
    }

    /**
     * Manually read clipboard content
     */
    async readClipboard() {
        if (!this.hasClipboardRead) {
            throw new Error('Clipboard read not supported');
        }

        try {
            const content = await navigator.clipboard.readText();
            this.currentContent = content;
            this.lastLocalContent = content;
            
            this.emit('clipboardRead', { content });
            return content;
            
        } catch (error) {
            console.error('Failed to read clipboard:', error);
            
            if (error.name === 'NotAllowedError') {
                this.emit('permissionDenied', { operation: 'read' });
            }
            
            throw error;
        }
    }

    /**
     * Write content to local clipboard
     */
    async writeToLocalClipboard(content) {
        if (!this.hasClipboardWrite) {
            throw new Error('Clipboard write not supported');
        }

        try {
            await navigator.clipboard.writeText(content);
            
            this.currentContent = content;
            this.lastLocalContent = content; // Prevent triggering change detection
            
            this.emit('clipboardWritten', { content });
            console.log('Content written to clipboard');
            
        } catch (error) {
            console.error('Failed to write to clipboard:', error);
            
            if (error.name === 'NotAllowedError') {
                this.emit('permissionDenied', { operation: 'write' });
            }
            
            throw error;
        }
    }

    /**
     * Manually set clipboard content (with sync)
     */
    async setClipboardContent(content) {
        await this.writeToLocalClipboard(content);
        
        // Sync to remote if in session
        if (this.signalRClient.getCurrentSessionId()) {
            await this.syncToRemote(content);
        }
    }

    /**
     * Get current clipboard content
     */
    getCurrentContent() {
        return this.currentContent;
    }

    /**
     * Get last sync time
     */
    getLastSyncTime() {
        return this.lastSyncTime;
    }

    /**
     * Check if monitoring is active
     */
    isMonitoringActive() {
        return this.isMonitoring;
    }

    /**
     * Get clipboard support status
     */
    getSupport() {
        return {
            read: this.hasClipboardRead,
            write: this.hasClipboardWrite,
            full: this.hasClipboardRead && this.hasClipboardWrite
        };
    }

    /**
     * Clear clipboard content
     */
    async clearClipboard() {
        await this.writeToLocalClipboard('');
    }

    /**
     * Check if content is valid for clipboard
     */
    isValidContent(content) {
        return typeof content === 'string' && content.trim().length > 0;
    }

    /**
     * Get clipboard statistics
     */
    getStats() {
        return {
            currentContentLength: this.currentContent.length,
            lastSyncTime: this.lastSyncTime,
            isMonitoring: this.isMonitoring,
            isConnected: this.signalRClient.isConnected(),
            inSession: !!this.signalRClient.getCurrentSessionId()
        };
    }

    /**
     * Request clipboard permissions
     */
    async requestPermissions() {
        try {
            // Try to read and write to test permissions
            if (this.hasClipboardRead) {
                await navigator.clipboard.readText();
            }
            
            if (this.hasClipboardWrite) {
                await navigator.clipboard.writeText('');
            }
            
            this.emit('permissionsGranted');
            return true;
            
        } catch (error) {
            console.warn('Clipboard permissions not granted:', error);
            this.emit('permissionDenied', { operation: 'test' });
            return false;
        }
    }

    /**
     * Cleanup resources
     */
    cleanup() {
        this.stopMonitoring();
        this.removeAllListeners();
        this.currentContent = '';
        this.lastLocalContent = '';
        console.log('Clipboard manager cleaned up');
    }
}

// Export for use in other modules
export { ClipboardManager };
