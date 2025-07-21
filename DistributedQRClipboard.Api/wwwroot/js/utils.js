/**
 * Utility functions for the Distributed QR Clipboard application
 */

// Theme management
export const Theme = {
  get current() {
    return localStorage.getItem('theme') || 'light';
  },
  
  set(theme) {
    localStorage.setItem('theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
    this.updateIcon();
  },
  
  toggle() {
    const newTheme = this.current === 'light' ? 'dark' : 'light';
    this.set(newTheme);
  },
  
  init() {
    const saved = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme = saved || (prefersDark ? 'dark' : 'light');
    this.set(theme);
  },
  
  updateIcon() {
    // Icon update is handled by CSS
  }
};

// Toast notification system
export const Toast = {
  container: null,
  
  init() {
    this.container = document.getElementById('toast-container');
  },
  
  show(message, type = 'info', title = '', duration = 5000) {
    if (!this.container) this.init();
    
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
      <div class="toast-content">
        ${title ? `<div class="toast-title">${escapeHtml(title)}</div>` : ''}
        <div class="toast-message">${escapeHtml(message)}</div>
      </div>
      <button class="toast-close" aria-label="Close notification">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
          <line x1="18" y1="6" x2="6" y2="18"/>
          <line x1="6" y1="6" x2="18" y2="18"/>
        </svg>
      </button>
    `;
    
    // Close button functionality
    toast.querySelector('.toast-close').addEventListener('click', () => {
      this.remove(toast);
    });
    
    // Auto-remove after duration
    setTimeout(() => {
      this.remove(toast);
    }, duration);
    
    this.container.appendChild(toast);
    
    return toast;
  },
  
  remove(toast) {
    if (toast && toast.parentNode) {
      toast.style.animation = 'toastSlideOut 300ms ease-in forwards';
      setTimeout(() => {
        toast.remove();
      }, 300);
    }
  },
  
  success(message, title = 'Success') {
    return this.show(message, 'success', title);
  },
  
  error(message, title = 'Error') {
    return this.show(message, 'error', title);
  },
  
  warning(message, title = 'Warning') {
    return this.show(message, 'warning', title);
  },
  
  info(message, title = 'Info') {
    return this.show(message, 'info', title);
  }
};

// Loading overlay management
export const Loading = {
  overlay: null,
  
  init() {
    this.overlay = document.getElementById('loading-overlay');
  },
  
  show(text = 'Loading...') {
    if (!this.overlay) this.init();
    
    const textElement = document.getElementById('loading-text');
    if (textElement) {
      textElement.textContent = text;
    }
    
    this.overlay.style.display = 'flex';
    document.body.style.overflow = 'hidden';
  },
  
  hide() {
    if (this.overlay) {
      this.overlay.style.display = 'none';
      document.body.style.overflow = '';
    }
  }
};

// UUID generation
export function generateUUID() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

// HTML escaping for security
export function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// Debounce function for performance
export function debounce(func, wait) {
  let timeout;
  return function executedFunction(...args) {
    const later = () => {
      clearTimeout(timeout);
      func(...args);
    };
    clearTimeout(timeout);
    timeout = setTimeout(later, wait);
  };
}

// Throttle function for performance
export function throttle(func, limit) {
  let inThrottle;
  return function(...args) {
    if (!inThrottle) {
      func.apply(this, args);
      inThrottle = true;
      setTimeout(() => inThrottle = false, limit);
    }
  };
}

// Format timestamp for display
export function formatTimestamp(date) {
  if (!date) return '';
  
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  
  if (diffMins < 1) {
    return 'Just now';
  } else if (diffMins < 60) {
    return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
  } else if (diffHours < 24) {
    return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
  } else if (diffDays < 7) {
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  } else {
    return date.toLocaleDateString();
  }
}

// Format content length
export function formatContentLength(length) {
  if (length < 1024) {
    return `${length} characters`;
  } else {
    const kb = (length / 1024).toFixed(1);
    return `${kb} KB`;
  }
}

// Validate session ID format
export function isValidSessionId(sessionId) {
  if (!sessionId || typeof sessionId !== 'string') return false;
  
  // UUID v4 format validation
  const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return uuidRegex.test(sessionId);
}

// Validate device ID format
export function isValidDeviceId(deviceId) {
  return isValidSessionId(deviceId); // Same format as session ID
}

// Copy text to clipboard with fallback
export async function copyToClipboard(text) {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    } else {
      // Fallback for older browsers or non-secure contexts
      const textArea = document.createElement('textarea');
      textArea.value = text;
      textArea.style.position = 'fixed';
      textArea.style.left = '-999999px';
      textArea.style.top = '-999999px';
      document.body.appendChild(textArea);
      textArea.focus();
      textArea.select();
      
      const success = document.execCommand('copy');
      textArea.remove();
      return success;
    }
  } catch (error) {
    console.error('Failed to copy to clipboard:', error);
    return false;
  }
}

// Read text from clipboard with fallback
export async function readFromClipboard() {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      const text = await navigator.clipboard.readText();
      return text;
    } else {
      // Fallback: prompt user to paste manually
      return prompt('Please paste the content here:') || '';
    }
  } catch (error) {
    console.error('Failed to read from clipboard:', error);
    // Fallback: prompt user to paste manually
    return prompt('Please paste the content here:') || '';
  }
}

// Local storage helpers with error handling
export const Storage = {
  get(key, defaultValue = null) {
    try {
      const item = localStorage.getItem(key);
      return item ? JSON.parse(item) : defaultValue;
    } catch (error) {
      console.error(`Failed to get item from storage: ${key}`, error);
      return defaultValue;
    }
  },
  
  set(key, value) {
    try {
      localStorage.setItem(key, JSON.stringify(value));
      return true;
    } catch (error) {
      console.error(`Failed to set item in storage: ${key}`, error);
      return false;
    }
  },
  
  remove(key) {
    try {
      localStorage.removeItem(key);
      return true;
    } catch (error) {
      console.error(`Failed to remove item from storage: ${key}`, error);
      return false;
    }
  }
};

// Event emitter for custom events
export class EventEmitter {
  constructor() {
    this.events = {};
  }
  
  on(event, callback) {
    if (!this.events[event]) {
      this.events[event] = [];
    }
    this.events[event].push(callback);
  }
  
  off(event, callback) {
    if (!this.events[event]) return;
    
    this.events[event] = this.events[event].filter(cb => cb !== callback);
  }
  
  emit(event, ...args) {
    if (!this.events[event]) return;
    
    this.events[event].forEach(callback => {
      try {
        callback(...args);
      } catch (error) {
        console.error(`Error in event callback for ${event}:`, error);
      }
    });
  }
}

// Retry mechanism with exponential backoff
export async function retryWithBackoff(
  fn, 
  maxAttempts = 3, 
  baseDelay = 1000, 
  backoffFactor = 2
) {
  let attempt = 1;
  
  while (attempt <= maxAttempts) {
    try {
      return await fn();
    } catch (error) {
      if (attempt === maxAttempts) {
        throw error;
      }
      
      const delay = baseDelay * Math.pow(backoffFactor, attempt - 1);
      console.warn(`Attempt ${attempt} failed, retrying in ${delay}ms:`, error.message);
      
      await new Promise(resolve => setTimeout(resolve, delay));
      attempt++;
    }
  }
}

// Network status detection
export const NetworkStatus = {
  isOnline: navigator.onLine,
  
  init() {
    window.addEventListener('online', () => {
      this.isOnline = true;
      this.emit('online');
    });
    
    window.addEventListener('offline', () => {
      this.isOnline = false;
      this.emit('offline');
    });
  },
  
  emit(event) {
    window.dispatchEvent(new CustomEvent(`network:${event}`));
  }
};

// Initialize utilities
document.addEventListener('DOMContentLoaded', () => {
  Theme.init();
  Toast.init();
  Loading.init();
  NetworkStatus.init();
});
