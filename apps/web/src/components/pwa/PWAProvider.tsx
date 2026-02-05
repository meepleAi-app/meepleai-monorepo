'use client';

/**
 * PWA Provider Component (Issue #3346)
 *
 * Wraps the app with PWA functionality including:
 * - Offline indicator
 * - Install prompt
 * - Update prompt
 */

import { OfflineIndicator } from './OfflineIndicator';
import { InstallPrompt } from './InstallPrompt';
import { UpdatePrompt } from './UpdatePrompt';

// ============================================================================
// Types
// ============================================================================

export interface PWAProviderProps {
  children: React.ReactNode;

  /** Whether to show the offline indicator */
  showOfflineIndicator?: boolean;

  /** Whether to show the install prompt */
  showInstallPrompt?: boolean;

  /** Whether to show the update prompt */
  showUpdatePrompt?: boolean;

  /** Install prompt variant */
  installPromptVariant?: 'dialog' | 'banner' | 'button';

  /** Install prompt delay in ms */
  installPromptDelay?: number;
}

// ============================================================================
// Component
// ============================================================================

export function PWAProvider({
  children,
  showOfflineIndicator = true,
  showInstallPrompt = true,
  showUpdatePrompt = true,
  installPromptVariant = 'banner',
  installPromptDelay = 60000, // 1 minute
}: PWAProviderProps) {
  return (
    <>
      {children}

      {/* Offline indicator */}
      {showOfflineIndicator && (
        <OfflineIndicator position="bottom" showSyncStatus />
      )}

      {/* Install prompt */}
      {showInstallPrompt && (
        <InstallPrompt
          variant={installPromptVariant}
          showDelay={installPromptDelay}
        />
      )}

      {/* Update prompt */}
      {showUpdatePrompt && <UpdatePrompt />}
    </>
  );
}

export default PWAProvider;
