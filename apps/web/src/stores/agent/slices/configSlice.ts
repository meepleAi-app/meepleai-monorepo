/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed game IDs */
/**
 * Config Slice (Issue #3188)
 *
 * Manages agent configuration state:
 * - Per-game config storage
 * - Backend sync via /library/games/{id}/agent-config
 * - Loading/error states
 *
 * Dependencies: none (standalone slice)
 */

import { StateCreator } from 'zustand';

import { AgentConfig, AgentStoreError } from '../types';
import { retryWithBackoff } from '../utils/retry';

import type { AgentStore } from '../types/store.types';

export interface ConfigSlice {
  // ============================================================================
  // State
  // ============================================================================

  /** Configurations by game ID */
  gameConfigs: Record<string, AgentConfig>;

  /** Loading state for config operations */
  loadingConfig: boolean;
  savingConfig: boolean;

  /** Last error for config operations */
  configError: AgentStoreError | null;

  // ============================================================================
  // Actions
  // ============================================================================

  /** Load config from backend */
  loadConfig: (gameId: string) => Promise<AgentConfig | null>;

  /** Save config to backend */
  saveConfig: (gameId: string, config: Partial<AgentConfig>) => Promise<void>;

  /** Update config locally (optimistic) */
  updateConfigLocal: (gameId: string, config: Partial<AgentConfig>) => void;

  /** Clear config error */
  clearConfigError: () => void;
}

export const createConfigSlice: StateCreator<
  AgentStore,
  [['zustand/immer', never]],
  [],
  ConfigSlice
> = set => ({
  // ============================================================================
  // Initial State
  // ============================================================================
  gameConfigs: {},
  loadingConfig: false,
  savingConfig: false,
  configError: null,

  // ============================================================================
  // Actions
  // ============================================================================

  /**
   * Load config from backend with retry
   */
  loadConfig: async gameId => {
    set(state => {
      state.loadingConfig = true;
      state.configError = null;
    });

    try {
      const config = await retryWithBackoff(
        async () => {
          // TODO: Replace with actual API call
          // const response = await fetch(`/api/v1/library/games/${gameId}/agent-config`);
          // if (!response.ok) throw new Error(`HTTP ${response.status}`);
          // return await response.json();

          // Mock implementation
          const config: AgentConfig = {
            gameId,
            mode: 'RulesClarifier',
            temperature: 0.7,
            maxTokens: 2000,
            useRAG: true,
            updatedAt: new Date(),
          };
          return config;
        },
        {
          maxAttempts: 3,
          retryableErrorCodes: ['NETWORK_ERROR', 'TIMEOUT', 'SERVER_ERROR'],
        }
      );

      set(state => {
        state.gameConfigs[gameId] = config;
        state.loadingConfig = false;
      });

      return config;
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to load config';

      set(state => {
        state.loadingConfig = false;
        state.configError = {
          message: errorMessage,
          code: 'LOAD_CONFIG_ERROR',
          timestamp: new Date(),
        };
      });

      return null;
    }
  },

  /**
   * Save config to backend with retry and rollback
   */
  saveConfig: async (gameId, partialConfig) => {
    set(state => {
      state.savingConfig = true;
      state.configError = null;
    });

    // Store original config for rollback
    let originalConfig: AgentConfig | undefined;
    set(state => {
      originalConfig = state.gameConfigs[gameId];
    });

    try {
      // Optimistic update
      set(state => {
        const existingConfig = state.gameConfigs[gameId];
        const updatedConfig: AgentConfig = {
          ...existingConfig,
          ...partialConfig,
          gameId,
          updatedAt: new Date(),
        };
        state.gameConfigs[gameId] = updatedConfig;
      });

      await retryWithBackoff(
        async () => {
          // TODO: Replace with actual API call
          // const response = await fetch(`/api/v1/library/games/${gameId}/agent-config`, {
          //   method: 'PUT',
          //   headers: { 'Content-Type': 'application/json' },
          //   body: JSON.stringify(partialConfig),
          // });
          // if (!response.ok) throw new Error(`HTTP ${response.status}`);

          // Mock delay
          await new Promise(resolve => setTimeout(resolve, 500));
        },
        {
          maxAttempts: 3,
          retryableErrorCodes: ['NETWORK_ERROR', 'TIMEOUT', 'SERVER_ERROR'],
        }
      );

      set(state => {
        state.savingConfig = false;
      });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to save config';

      // Rollback optimistic update
      set(state => {
        if (originalConfig) {
          state.gameConfigs[gameId] = originalConfig;
        }
        state.savingConfig = false;
        state.configError = {
          message: errorMessage,
          code: 'SAVE_CONFIG_ERROR',
          timestamp: new Date(),
        };
      });

      throw error;
    }
  },

  /**
   * Update config locally (without backend sync)
   */
  updateConfigLocal: (gameId, partialConfig) =>
    set(state => {
      const existingConfig = state.gameConfigs[gameId];
      const updatedConfig: AgentConfig = {
        ...existingConfig,
        ...partialConfig,
        gameId,
        updatedAt: new Date(),
      };
      state.gameConfigs[gameId] = updatedConfig;
    }),

  /**
   * Clear config error
   */
  clearConfigError: () =>
    set(state => {
      state.configError = null;
    }),
});
