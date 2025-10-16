/**
 * Toast notification component with animations and accessibility
 */

import { useEffect, useState } from 'react';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface ToastProps {
  id: string;
  type: ToastType;
  title: string;
  message?: string;
  duration?: number;
  onDismiss: (id: string) => void;
}

/**
 * Individual toast notification
 */
export function Toast({ id, type, title, message, duration = 5000, onDismiss }: ToastProps) {
  const [isVisible, setIsVisible] = useState(false);
  const [isExiting, setIsExiting] = useState(false);

  useEffect(() => {
    // Trigger entrance animation
    const timer = setTimeout(() => setIsVisible(true), 10);

    // Auto-dismiss after duration
    let dismissTimer: NodeJS.Timeout | null = null;
    if (duration > 0) {
      dismissTimer = setTimeout(() => handleDismiss(), duration);
    }

    return () => {
      clearTimeout(timer);
      if (dismissTimer) clearTimeout(dismissTimer);
    };
  }, [duration]);

  const handleDismiss = () => {
    setIsExiting(true);
    setTimeout(() => onDismiss(id), 300); // Match animation duration
  };

  const getStyles = () => {
    const baseStyles = 'border-l-4 shadow-lg';
    const typeStyles = {
      success: 'bg-green-50 border-green-500',
      error: 'bg-red-50 border-red-500',
      warning: 'bg-yellow-50 border-yellow-500',
      info: 'bg-blue-50 border-blue-500'
    };
    return `${baseStyles} ${typeStyles[type]}`;
  };

  const getIconColor = () => {
    const colors = {
      success: 'text-green-500',
      error: 'text-red-500',
      warning: 'text-yellow-500',
      info: 'text-blue-500'
    };
    return colors[type];
  };

  const getIcon = () => {
    switch (type) {
      case 'success':
        return (
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
          </svg>
        );
      case 'error':
        return (
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
          </svg>
        );
      case 'warning':
        return (
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
        );
      case 'info':
        return (
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
          </svg>
        );
    }
  };

  const getTextColor = () => {
    const colors = {
      success: 'text-green-900',
      error: 'text-red-900',
      warning: 'text-yellow-900',
      info: 'text-blue-900'
    };
    return colors[type];
  };

  const animationClass = isExiting
    ? 'translate-x-full opacity-0'
    : isVisible
    ? 'translate-x-0 opacity-100'
    : 'translate-x-full opacity-0';

  return (
    <div
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
      className={`${getStyles()} rounded-lg p-4 mb-3 transition-all duration-300 ease-in-out transform ${animationClass} max-w-md w-full pointer-events-auto`}
    >
      <div className="flex items-start">
        <div className={`flex-shrink-0 ${getIconColor()}`}>
          {getIcon()}
        </div>
        <div className="ml-3 flex-1">
          <h3 className={`text-sm font-semibold ${getTextColor()}`}>
            {title}
          </h3>
          {message && (
            <p className={`mt-1 text-sm ${getTextColor()} opacity-90`}>
              {message}
            </p>
          )}
        </div>
        <button
          type="button"
          onClick={handleDismiss}
          className={`ml-3 flex-shrink-0 ${getTextColor()} hover:opacity-75 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-gray-500 rounded`}
          aria-label="Dismiss notification"
        >
          <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </button>
      </div>
    </div>
  );
}

export interface ToastContainerProps {
  toasts: ToastProps[];
  onDismiss: (id: string) => void;
  position?: 'top-right' | 'top-left' | 'bottom-right' | 'bottom-left' | 'top-center' | 'bottom-center';
}

/**
 * Container for managing multiple toasts
 */
export function ToastContainer({ toasts, onDismiss, position = 'top-right' }: ToastContainerProps) {
  const getPositionClass = () => {
    const positions = {
      'top-right': 'top-4 right-4',
      'top-left': 'top-4 left-4',
      'bottom-right': 'bottom-4 right-4',
      'bottom-left': 'bottom-4 left-4',
      'top-center': 'top-4 left-1/2 transform -translate-x-1/2',
      'bottom-center': 'bottom-4 left-1/2 transform -translate-x-1/2'
    };
    return positions[position];
  };

  return (
    <div
      role="region"
      aria-label="Notifications"
      className={`fixed ${getPositionClass()} z-50 pointer-events-none`}
    >
      <div className="flex flex-col">
        {toasts.map((toast) => (
          <Toast key={toast.id} {...toast} onDismiss={onDismiss} />
        ))}
      </div>
    </div>
  );
}
