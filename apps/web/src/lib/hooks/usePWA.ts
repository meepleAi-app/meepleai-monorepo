'use client';

/**
 * PWA Hook for MeepleAI (Issue #3346)
 *
 * Provides:
 * - Service worker registration
 * - Install prompt handling
 * - Online/offline status
 * - Background sync state
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { initOfflineStorage, getStorageStats, clearAllData } from '@/lib/pwa/offline-storage';
import { syncManager, type SyncState, type SyncResult } from '@/lib/pwa/sync-manager';

// ============================================================================
// Types
// ============================================================================

export interface PWAState {
  /** Whether the service worker is registered and active */
  isInstalled: boolean;

  /** Whether the app can be installed (install prompt available) */
  canInstall: boolean;

  /** Whether the app is running in standalone mode (installed) */
  isStandalone: boolean;

  /** Whether the device is online */
  isOnline: boolean;

  /** Whether a service worker update is available */
  updateAvailable: boolean;

  /** Background sync state */
  syncState: SyncState;

  /** Storage statistics */
  storageStats: {
    sessions: number;
    pendingActions: number;
    cachedGames: number;
  } | null;

  /** Whether the PWA is supported on this browser */
  isSupported: boolean;
}

export interface PWAActions {
  /** Trigger the install prompt */
  install: () => Promise<boolean>;

  /** Apply pending service worker update */
  applyUpdate: () => void;

  /** Manually trigger background sync */
  sync: () => Promise<SyncResult | null>;

  /** Clear all offline data */
  clearOfflineData: () => Promise<void>;

  /** Refresh storage stats */
  refreshStats: () => Promise<void>;
}

export interface UsePWAReturn extends PWAState {
  actions: PWAActions;
}

// ============================================================================
// BeforeInstallPromptEvent Type
// ============================================================================

interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[];
  readonly userChoice: Promise<{
    outcome: 'accepted' | 'dismissed';
    platform: string;
  }>;
  prompt(): Promise<void>;
}

// ============================================================================
// Hook
// ============================================================================

export function usePWA(): UsePWAReturn {
  // State
  const [isInstalled, setIsInstalled] = useState(false);
  const [canInstall, setCanInstall] = useState(false);
  const [isStandalone, setIsStandalone] = useState(false);
  const [isOnline, setIsOnline] = useState(true);
  const [updateAvailable, setUpdateAvailable] = useState(false);
  const [syncState, setSyncState] = useState<SyncState>({
    status: 'idle',
    lastSyncAt: null,
    pendingCount: 0,
    isOnline: true,
  });
  const [storageStats, setStorageStats] = useState<PWAState['storageStats']>(null);
  const [isSupported, setIsSupported] = useState(false);

  // Refs
  const installPromptRef = useRef<BeforeInstallPromptEvent | null>(null);
  const swRegistrationRef = useRef<ServiceWorkerRegistration | null>(null);

  // ==========================================================================
  // Initialize
  // ==========================================================================

  useEffect(() => {
    if (typeof window === 'undefined') return;

    // Check PWA support
    const supported = 'serviceWorker' in navigator;
    setIsSupported(supported);

    // Check online status
    setIsOnline(navigator.onLine);

    // Check standalone mode
    const isStandaloneMode =
      window.matchMedia('(display-mode: standalone)').matches ||
      (window.navigator as Navigator & { standalone?: boolean }).standalone === true;
    setIsStandalone(isStandaloneMode);

    // Initialize offline storage
    initOfflineStorage().catch(console.error);

    // Register service worker
    if (supported) {
      registerServiceWorker();
    }

    // Subscribe to sync manager
    const unsubscribe = syncManager?.subscribe(setSyncState);

    // Event listeners
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    window.addEventListener('beforeinstallprompt', handleInstallPrompt as EventListener);
    window.addEventListener('appinstalled', handleAppInstalled);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      window.removeEventListener('beforeinstallprompt', handleInstallPrompt as EventListener);
      window.removeEventListener('appinstalled', handleAppInstalled);
      unsubscribe?.();
    };
  }, []);

  // ==========================================================================
  // Service Worker Registration
  // ==========================================================================

  const registerServiceWorker = async () => {
    try {
      const registration = await navigator.serviceWorker.register('/sw.js', {
        scope: '/',
      });

      swRegistrationRef.current = registration;
      setIsInstalled(true);

      console.log('[PWA] Service worker registered:', registration.scope);

      // Check for updates
      registration.addEventListener('updatefound', () => {
        const newWorker = registration.installing;
        if (newWorker) {
          newWorker.addEventListener('statechange', () => {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              console.log('[PWA] New service worker available');
              setUpdateAvailable(true);
            }
          });
        }
      });

      // Handle controller change (after update)
      navigator.serviceWorker.addEventListener('controllerchange', () => {
        console.log('[PWA] Service worker controller changed');
        setUpdateAvailable(false);
      });
    } catch (error) {
      console.error('[PWA] Service worker registration failed:', error);
    }
  };

  // ==========================================================================
  // Event Handlers
  // ==========================================================================

  const handleOnline = () => {
    setIsOnline(true);
  };

  const handleOffline = () => {
    setIsOnline(false);
  };

  const handleInstallPrompt = (event: BeforeInstallPromptEvent) => {
    event.preventDefault();
    installPromptRef.current = event;
    setCanInstall(true);
    console.log('[PWA] Install prompt captured');
  };

  const handleAppInstalled = () => {
    setCanInstall(false);
    setIsStandalone(true);
    installPromptRef.current = null;
    console.log('[PWA] App installed');
  };

  // ==========================================================================
  // Actions
  // ==========================================================================

  const install = useCallback(async (): Promise<boolean> => {
    if (!installPromptRef.current) {
      console.warn('[PWA] No install prompt available');
      return false;
    }

    try {
      await installPromptRef.current.prompt();
      const { outcome } = await installPromptRef.current.userChoice;

      console.log('[PWA] Install prompt result:', outcome);

      if (outcome === 'accepted') {
        setCanInstall(false);
        installPromptRef.current = null;
        return true;
      }

      return false;
    } catch (error) {
      console.error('[PWA] Install prompt error:', error);
      return false;
    }
  }, []);

  const applyUpdate = useCallback(() => {
    if (swRegistrationRef.current?.waiting) {
      // Tell the waiting service worker to skip waiting
      swRegistrationRef.current.waiting.postMessage({ type: 'SKIP_WAITING' });

      // Reload the page after the new service worker takes over
      window.location.reload();
    }
  }, []);

  const sync = useCallback(async (): Promise<SyncResult | null> => {
    if (!syncManager) return null;
    return syncManager.syncAll();
  }, []);

  const clearOfflineData = useCallback(async (): Promise<void> => {
    await clearAllData();
    const stats = await getStorageStats();
    setStorageStats(stats);
  }, []);

  const refreshStats = useCallback(async (): Promise<void> => {
    try {
      const stats = await getStorageStats();
      setStorageStats(stats);
    } catch (error) {
      console.error('[PWA] Failed to get storage stats:', error);
    }
  }, []);

  // Initial stats load
  useEffect(() => {
    refreshStats();
  }, [refreshStats]);

  // ==========================================================================
  // Return
  // ==========================================================================

  return {
    isInstalled,
    canInstall,
    isStandalone,
    isOnline,
    updateAvailable,
    syncState,
    storageStats,
    isSupported,
    actions: {
      install,
      applyUpdate,
      sync,
      clearOfflineData,
      refreshStats,
    },
  };
}

export default usePWA;
