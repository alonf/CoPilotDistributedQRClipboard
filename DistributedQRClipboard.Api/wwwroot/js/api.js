/**
 * API client for the Distributed QR Clipboard backend
 */

import { retryWithBackoff, Toast } from './utils.js';

// API configuration
const API_CONFIG = {
  baseUrl: window.location.origin,
  timeout: 10000,
  retries: 3
};

// HTTP client with retry logic and error handling
class HttpClient {
  constructor(baseUrl, timeout = 10000) {
    this.baseUrl = baseUrl;
    this.timeout = timeout;
  }
  
  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const config = {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers
      }
    };
    
    // Add timeout using AbortController
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);
    config.signal = controller.signal;
    
    try {
      const response = await fetch(url, config);
      clearTimeout(timeoutId);
      
      if (!response.ok) {
        const errorData = await this.parseErrorResponse(response);
        throw new ApiError(errorData.message || 'Request failed', response.status, errorData);
      }
      
      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('application/json')) {
        return await response.json();
      } else {
        return await response.text();
      }
    } catch (error) {
      clearTimeout(timeoutId);
      
      if (error.name === 'AbortError') {
        throw new ApiError('Request timeout', 408);
      }
      
      if (error instanceof ApiError) {
        throw error;
      }
      
      // Network or other errors
      throw new ApiError('Network error', 0, { originalError: error });
    }
  }
  
  async parseErrorResponse(response) {
    try {
      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('application/json')) {
        return await response.json();
      } else {
        const text = await response.text();
        return { message: text || `HTTP ${response.status} ${response.statusText}` };
      }
    } catch {
      return { message: `HTTP ${response.status} ${response.statusText}` };
    }
  }
  
  get(endpoint, options = {}) {
    return this.request(endpoint, { ...options, method: 'GET' });
  }
  
  post(endpoint, data, options = {}) {
    return this.request(endpoint, {
      ...options,
      method: 'POST',
      body: JSON.stringify(data)
    });
  }
  
  put(endpoint, data, options = {}) {
    return this.request(endpoint, {
      ...options,
      method: 'PUT',
      body: JSON.stringify(data)
    });
  }
  
  delete(endpoint, options = {}) {
    return this.request(endpoint, { ...options, method: 'DELETE' });
  }
}

// Custom error class for API errors
export class ApiError extends Error {
  constructor(message, status, details = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }
  
  get isNetworkError() {
    return this.status === 0;
  }
  
  get isClientError() {
    return this.status >= 400 && this.status < 500;
  }
  
  get isServerError() {
    return this.status >= 500;
  }
  
  get isTimeout() {
    return this.status === 408;
  }
}

// API client class
export class ApiClient {
  constructor() {
    this.http = new HttpClient(API_CONFIG.baseUrl, API_CONFIG.timeout);
  }
  
  // Session Management
  async createSession() {
    return retryWithBackoff(async () => {
      const response = await this.http.post('/api/sessions');
      return response;
    }, API_CONFIG.retries);
  }
  
  async getSession(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.get(`/api/sessions/${sessionId}`);
      return response;
    }, API_CONFIG.retries);
  }
  
  async joinSession(sessionId, deviceName = null) {
    return retryWithBackoff(async () => {
      const data = deviceName ? { deviceName } : {};
      const response = await this.http.post(`/api/sessions/${sessionId}/join`, data);
      return response;
    }, API_CONFIG.retries);
  }
  
  async leaveSession(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.delete(`/api/sessions/${sessionId}/leave`);
      return response;
    }, API_CONFIG.retries);
  }
  
  // Clipboard Operations
  async copyToClipboard(sessionId, content) {
    return retryWithBackoff(async () => {
      const data = { content };
      const response = await this.http.post(`/api/sessions/${sessionId}/clipboard`, data);
      return response;
    }, API_CONFIG.retries);
  }
  
  async getClipboardContent(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.get(`/api/sessions/${sessionId}/clipboard`);
      return response;
    }, API_CONFIG.retries);
  }
  
  async clearClipboard(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.delete(`/api/sessions/${sessionId}/clipboard`);
      return response;
    }, API_CONFIG.retries);
  }
  
  async getClipboardHistory(sessionId, limit = 10) {
    return retryWithBackoff(async () => {
      const response = await this.http.get(`/api/sessions/${sessionId}/clipboard/history?limit=${limit}`);
      return response;
    }, API_CONFIG.retries);
  }
  
  async getClipboardStats(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.get(`/api/sessions/${sessionId}/clipboard/stats`);
      return response;
    }, API_CONFIG.retries);
  }
  
  // QR Code Operations
  async generateQrCode(sessionId) {
    return retryWithBackoff(async () => {
      const response = await this.http.get(`/api/sessions/${sessionId}/qr-code`);
      return response;
    }, API_CONFIG.retries);
  }
  
  // Error handling wrapper for UI operations
  async withErrorHandling(operation, errorPrefix = 'Operation failed') {
    try {
      return await operation();
    } catch (error) {
      this.handleApiError(error, errorPrefix);
      throw error;
    }
  }
  
  handleApiError(error, prefix = 'Error') {
    console.error(`${prefix}:`, error);
    
    if (error instanceof ApiError) {
      if (error.isNetworkError) {
        Toast.error(
          'Please check your internet connection and try again.',
          'Network Error'
        );
      } else if (error.isTimeout) {
        Toast.error(
          'The request took too long to complete. Please try again.',
          'Request Timeout'
        );
      } else if (error.status === 404) {
        Toast.error(
          'The requested resource was not found. The session may have expired.',
          'Not Found'
        );
      } else if (error.status === 429) {
        Toast.error(
          'Too many requests. Please wait a moment before trying again.',
          'Rate Limited'
        );
      } else if (error.isClientError) {
        Toast.error(
          error.message || 'Invalid request. Please check your input.',
          'Client Error'
        );
      } else if (error.isServerError) {
        Toast.error(
          'Server error occurred. Please try again later.',
          'Server Error'
        );
      } else {
        Toast.error(
          error.message || 'An unexpected error occurred.',
          prefix
        );
      }
    } else {
      Toast.error(
        'An unexpected error occurred. Please try again.',
        prefix
      );
    }
  }
}

// Create and export a singleton API client instance
export const apiClient = new ApiClient();

// API response helpers
export const ApiResponse = {
  isSuccess(response) {
    return response && (response.success === true || response.Success === true);
  },
  
  getMessage(response) {
    return response?.message || response?.Message || 'Unknown error';
  },
  
  getData(response) {
    return response?.data || response?.Data || response;
  }
};

// Connection status tracking
export class ConnectionTracker {
  constructor() {
    this.isOnline = navigator.onLine;
    this.lastSuccessfulRequest = Date.now();
    this.failureCount = 0;
    this.maxFailures = 3;
    
    this.init();
  }
  
  init() {
    // Track network status changes
    window.addEventListener('online', () => {
      this.isOnline = true;
      this.failureCount = 0;
      this.emit('connection-restored');
    });
    
    window.addEventListener('offline', () => {
      this.isOnline = false;
      this.emit('connection-lost');
    });
  }
  
  recordSuccess() {
    this.lastSuccessfulRequest = Date.now();
    this.failureCount = 0;
  }
  
  recordFailure() {
    this.failureCount++;
    
    if (this.failureCount >= this.maxFailures) {
      this.emit('connection-issues');
    }
  }
  
  get status() {
    if (!this.isOnline) return 'offline';
    if (this.failureCount >= this.maxFailures) return 'unstable';
    return 'online';
  }
  
  emit(event) {
    window.dispatchEvent(new CustomEvent(`api:${event}`, {
      detail: { status: this.status, failureCount: this.failureCount }
    }));
  }
}

// Create and export connection tracker
export const connectionTracker = new ConnectionTracker();

// Patch the API client to use connection tracking
const originalRequest = apiClient.http.request;
apiClient.http.request = async function(...args) {
  try {
    const result = await originalRequest.apply(this, args);
    connectionTracker.recordSuccess();
    return result;
  } catch (error) {
    connectionTracker.recordFailure();
    throw error;
  }
};

// Export the ApiClient class
export { ApiClient };
