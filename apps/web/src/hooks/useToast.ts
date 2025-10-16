/**
 * Toast notification hook
 */

import { useState, useCallback } from 'react';
import { ToastProps, ToastType } from '../components/Toast';

let toastIdCounter = 0;

export function useToast() {
  const [toasts, setToasts] = useState<ToastProps[]>([]);

  const addToast = useCallback((
    type: ToastType,
    title: string,
    message?: string,
    duration?: number
  ) => {
    const id = `toast-${++toastIdCounter}`;

    const newToast: ToastProps = {
      id,
      type,
      title,
      message,
      duration,
      onDismiss: () => {} // Will be set below
    };

    setToasts((prev) => [...prev, newToast]);

    return id;
  }, []);

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((toast) => toast.id !== id));
  }, []);

  const success = useCallback((title: string, message?: string, duration?: number) => {
    return addToast('success', title, message, duration);
  }, [addToast]);

  const error = useCallback((title: string, message?: string, duration?: number) => {
    return addToast('error', title, message, duration);
  }, [addToast]);

  const warning = useCallback((title: string, message?: string, duration?: number) => {
    return addToast('warning', title, message, duration);
  }, [addToast]);

  const info = useCallback((title: string, message?: string, duration?: number) => {
    return addToast('info', title, message, duration);
  }, [addToast]);

  const clearAll = useCallback(() => {
    setToasts([]);
  }, []);

  return {
    toasts: toasts.map((toast) => ({
      ...toast,
      onDismiss: dismissToast
    })),
    success,
    error,
    warning,
    info,
    dismiss: dismissToast,
    clearAll
  };
}
