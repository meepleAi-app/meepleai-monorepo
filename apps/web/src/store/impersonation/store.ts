/**
 * Impersonation Store (Issue #3349)
 *
 * Zustand store for managing admin impersonation state.
 * Persists across page navigations but not browser sessions (security).
 */

import { create } from 'zustand';

import { api } from '@/lib/api';
import { logger } from '@/lib/logger';

// ============================================================================
// Types
// ============================================================================

export interface ImpersonatedUser {
  id: string;
  displayName: string;
  email: string;
}

export interface ImpersonationState {
  /** Whether admin is currently impersonating a user */
  isImpersonating: boolean;

  /** The user being impersonated */
  impersonatedUser: ImpersonatedUser | null;

  /** Session ID for the impersonation session */
  sessionId: string | null;

  /** Original admin session token (to restore after ending impersonation) */
  originalSessionToken: string | null;

  /** Impersonation session expiry */
  expiresAt: string | null;

  /** Loading state for impersonation operations */
  isLoading: boolean;

  /** Error message if operation fails */
  error: string | null;
}

export interface ImpersonationActions {
  /** Start impersonating a user */
  startImpersonation: (
    userId: string,
    userInfo: { displayName: string; email: string }
  ) => Promise<boolean>;

  /** End the current impersonation session */
  endImpersonation: () => Promise<boolean>;

  /** Clear any error */
  clearError: () => void;

  /** Reset store to initial state */
  reset: () => void;
}

export type ImpersonationStore = ImpersonationState & ImpersonationActions;

// ============================================================================
// Initial State
// ============================================================================

const initialState: ImpersonationState = {
  isImpersonating: false,
  impersonatedUser: null,
  sessionId: null,
  originalSessionToken: null,
  expiresAt: null,
  isLoading: false,
  error: null,
};

// ============================================================================
// Store
// ============================================================================

export const useImpersonationStore = create<ImpersonationStore>((set, get) => ({
  ...initialState,

  startImpersonation: async (userId, userInfo) => {
    set({ isLoading: true, error: null });

    try {
      // Store original session token from cookie (for restoration)
      const originalToken =
        document.cookie
          .split('; ')
          .find(row => row.startsWith('session_token='))
          ?.split('=')[1] || null;

      // Call API to start impersonation
      const response = await api.admin.impersonateUser(userId);

      // Set the new session token cookie
      // TODO: Migrate to backend HttpOnly Set-Cookie header for full cookie security
      document.cookie = `session_token=${response.sessionToken}; path=/; samesite=strict; secure; max-age=3600`;

      set({
        isImpersonating: true,
        impersonatedUser: {
          id: response.impersonatedUserId,
          displayName: userInfo.displayName,
          email: userInfo.email,
        },
        sessionId: response.impersonatedUserId, // Use as session identifier
        originalSessionToken: originalToken,
        expiresAt: response.expiresAt,
        isLoading: false,
      });

      logger.info('Started impersonation', {
        component: 'ImpersonationStore',
        metadata: { userId, displayName: userInfo.displayName },
      });

      // Reload to apply new session
      window.location.reload();

      return true;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to start impersonation';
      logger.error(
        'Failed to start impersonation',
        err instanceof Error ? err : new Error(errorMessage),
        {
          component: 'ImpersonationStore',
          metadata: { userId },
        }
      );
      set({ isLoading: false, error: errorMessage });
      return false;
    }
  },

  endImpersonation: async () => {
    const state = get();
    if (!state.isImpersonating) return true;

    set({ isLoading: true, error: null });

    try {
      // Call API to end impersonation
      if (state.sessionId) {
        await api.admin.endImpersonation(state.sessionId);
      }

      // Restore original session token
      if (state.originalSessionToken) {
        document.cookie = `session_token=${state.originalSessionToken}; path=/; samesite=strict; secure; max-age=3600`;
      } else {
        // Clear session cookie if no original token
        document.cookie = 'session_token=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT';
      }

      logger.info('Ended impersonation', {
        component: 'ImpersonationStore',
        metadata: { impersonatedUserId: state.impersonatedUser?.id },
      });

      set(initialState);

      // Reload to restore admin session
      window.location.href = '/admin/users';

      return true;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to end impersonation';
      logger.error(
        'Failed to end impersonation',
        err instanceof Error ? err : new Error(errorMessage),
        {
          component: 'ImpersonationStore',
        }
      );
      set({ isLoading: false, error: errorMessage });
      return false;
    }
  },

  clearError: () => set({ error: null }),

  reset: () => set(initialState),
}));

// ============================================================================
// Selectors (for optimized re-renders)
// ============================================================================

export const selectIsImpersonating = (state: ImpersonationStore) => state.isImpersonating;
export const selectImpersonatedUser = (state: ImpersonationStore) => state.impersonatedUser;
export const selectImpersonationLoading = (state: ImpersonationStore) => state.isLoading;
export const selectImpersonationError = (state: ImpersonationStore) => state.error;
