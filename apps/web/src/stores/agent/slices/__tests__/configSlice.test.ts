/**
 * Config Slice Tests
 * Issue #3188: Agent configuration state management
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

import { createConfigSlice, ConfigSlice } from '../configSlice';

// Mock the retry utility
vi.mock('../../utils/retry', () => ({
  retryWithBackoff: vi.fn((fn: () => Promise<unknown>) => fn()),
}));

// Create a test store with only the config slice
function createTestStore() {
  return create<ConfigSlice>()(
    immer((...args) => ({
      ...createConfigSlice(...args),
    }))
  );
}

describe('configSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    vi.clearAllMocks();
    store = createTestStore();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('has empty gameConfigs initially', () => {
      expect(store.getState().gameConfigs).toEqual({});
    });

    it('has loadingConfig set to false initially', () => {
      expect(store.getState().loadingConfig).toBe(false);
    });

    it('has savingConfig set to false initially', () => {
      expect(store.getState().savingConfig).toBe(false);
    });

    it('has configError set to null initially', () => {
      expect(store.getState().configError).toBeNull();
    });
  });

  describe('loadConfig', () => {
    it('sets loadingConfig to true while loading', async () => {
      const loadPromise = store.getState().loadConfig('game-123');

      // Check loading state before resolution
      expect(store.getState().loadingConfig).toBe(true);

      await loadPromise;
    });

    it('sets loadingConfig to false after loading completes', async () => {
      await store.getState().loadConfig('game-123');

      expect(store.getState().loadingConfig).toBe(false);
    });

    it('stores loaded config in gameConfigs', async () => {
      const config = await store.getState().loadConfig('game-123');

      expect(config).not.toBeNull();
      expect(config?.gameId).toBe('game-123');
      expect(store.getState().gameConfigs['game-123']).toBeDefined();
    });

    it('returns config with default values', async () => {
      const config = await store.getState().loadConfig('game-456');

      expect(config?.mode).toBe('RulesClarifier');
      expect(config?.temperature).toBe(0.7);
      expect(config?.maxTokens).toBe(2000);
      expect(config?.useRAG).toBe(true);
    });

    it('clears previous error before loading', async () => {
      // Set an error first
      store.setState({
        configError: {
          message: 'Previous error',
          code: 'PREV_ERROR',
          timestamp: new Date(),
        },
      });

      await store.getState().loadConfig('game-123');

      expect(store.getState().configError).toBeNull();
    });
  });

  describe('loadConfig error handling', () => {
    it('sets configError on failure', async () => {
      const { retryWithBackoff } = await import('../../utils/retry');
      vi.mocked(retryWithBackoff).mockRejectedValueOnce(new Error('Network failed'));

      const result = await store.getState().loadConfig('game-fail');

      expect(result).toBeNull();
      expect(store.getState().configError).not.toBeNull();
      expect(store.getState().configError?.code).toBe('LOAD_CONFIG_ERROR');
    });

    it('sets loadingConfig to false on error', async () => {
      const { retryWithBackoff } = await import('../../utils/retry');
      vi.mocked(retryWithBackoff).mockRejectedValueOnce(new Error('Network failed'));

      await store.getState().loadConfig('game-fail');

      expect(store.getState().loadingConfig).toBe(false);
    });
  });

  describe('saveConfig', () => {
    beforeEach(async () => {
      // Load initial config
      await store.getState().loadConfig('game-123');
    });

    it('sets savingConfig to true while saving', async () => {
      const savePromise = store.getState().saveConfig('game-123', { temperature: 0.5 });

      expect(store.getState().savingConfig).toBe(true);

      await savePromise;
    });

    it('sets savingConfig to false after saving completes', async () => {
      await store.getState().saveConfig('game-123', { temperature: 0.5 });

      expect(store.getState().savingConfig).toBe(false);
    });

    it('updates config with partial values', async () => {
      await store.getState().saveConfig('game-123', { temperature: 0.3, maxTokens: 1500 });

      const config = store.getState().gameConfigs['game-123'];
      expect(config.temperature).toBe(0.3);
      expect(config.maxTokens).toBe(1500);
    });

    it('preserves existing config values not being updated', async () => {
      await store.getState().saveConfig('game-123', { temperature: 0.3 });

      const config = store.getState().gameConfigs['game-123'];
      expect(config.useRAG).toBe(true); // Original value preserved
      expect(config.mode).toBe('RulesClarifier'); // Original value preserved
    });

    it('updates updatedAt timestamp', async () => {
      const beforeSave = new Date();
      await store.getState().saveConfig('game-123', { temperature: 0.3 });

      const config = store.getState().gameConfigs['game-123'];
      expect(config.updatedAt.getTime()).toBeGreaterThanOrEqual(beforeSave.getTime());
    });
  });

  describe('saveConfig error handling', () => {
    it('rolls back optimistic update on failure', async () => {
      // First load config successfully
      await store.getState().loadConfig('game-123');
      const originalTemp = store.getState().gameConfigs['game-123'].temperature;

      // Now mock the retry to fail for saveConfig
      const { retryWithBackoff } = await import('../../utils/retry');
      vi.mocked(retryWithBackoff).mockRejectedValueOnce(new Error('Save failed'));

      try {
        await store.getState().saveConfig('game-123', { temperature: 0.1 });
      } catch {
        // Expected to throw
      }

      // Config should be rolled back
      expect(store.getState().gameConfigs['game-123'].temperature).toBe(originalTemp);
    });

    it('sets configError on save failure', async () => {
      // First load config successfully
      await store.getState().loadConfig('game-123');

      // Now mock the retry to fail for saveConfig
      const { retryWithBackoff } = await import('../../utils/retry');
      vi.mocked(retryWithBackoff).mockRejectedValueOnce(new Error('Save failed'));

      try {
        await store.getState().saveConfig('game-123', { temperature: 0.1 });
      } catch {
        // Expected to throw
      }

      expect(store.getState().configError?.code).toBe('SAVE_CONFIG_ERROR');
    });

    it('throws error on save failure', async () => {
      // First load config successfully
      await store.getState().loadConfig('game-123');

      // Now mock the retry to fail for saveConfig
      const { retryWithBackoff } = await import('../../utils/retry');
      vi.mocked(retryWithBackoff).mockRejectedValueOnce(new Error('Save failed'));

      await expect(
        store.getState().saveConfig('game-123', { temperature: 0.1 })
      ).rejects.toThrow('Save failed');
    });
  });

  describe('updateConfigLocal', () => {
    it('updates config without backend call', () => {
      store.getState().updateConfigLocal('game-new', {
        mode: 'StrategyAdvisor',
        temperature: 0.8,
      });

      const config = store.getState().gameConfigs['game-new'];
      expect(config.mode).toBe('StrategyAdvisor');
      expect(config.temperature).toBe(0.8);
    });

    it('sets gameId in the config', () => {
      store.getState().updateConfigLocal('game-abc', { temperature: 0.5 });

      expect(store.getState().gameConfigs['game-abc'].gameId).toBe('game-abc');
    });

    it('updates updatedAt timestamp', () => {
      const before = new Date();
      store.getState().updateConfigLocal('game-xyz', { temperature: 0.5 });

      const config = store.getState().gameConfigs['game-xyz'];
      expect(config.updatedAt.getTime()).toBeGreaterThanOrEqual(before.getTime());
    });

    it('merges with existing config', async () => {
      await store.getState().loadConfig('game-merge');

      store.getState().updateConfigLocal('game-merge', { maxTokens: 3000 });

      const config = store.getState().gameConfigs['game-merge'];
      expect(config.maxTokens).toBe(3000);
      expect(config.mode).toBe('RulesClarifier'); // Original preserved
    });
  });

  describe('clearConfigError', () => {
    it('clears config error', () => {
      store.setState({
        configError: {
          message: 'Test error',
          code: 'TEST_ERROR',
          timestamp: new Date(),
        },
      });

      store.getState().clearConfigError();

      expect(store.getState().configError).toBeNull();
    });

    it('has no effect when error is already null', () => {
      store.getState().clearConfigError();

      expect(store.getState().configError).toBeNull();
    });
  });

  describe('Multiple Games', () => {
    it('can store configs for multiple games', async () => {
      await store.getState().loadConfig('game-1');
      await store.getState().loadConfig('game-2');
      await store.getState().loadConfig('game-3');

      expect(Object.keys(store.getState().gameConfigs)).toHaveLength(3);
      expect(store.getState().gameConfigs['game-1']).toBeDefined();
      expect(store.getState().gameConfigs['game-2']).toBeDefined();
      expect(store.getState().gameConfigs['game-3']).toBeDefined();
    });

    it('updates specific game config without affecting others', async () => {
      await store.getState().loadConfig('game-1');
      await store.getState().loadConfig('game-2');

      store.getState().updateConfigLocal('game-1', { temperature: 0.1 });

      expect(store.getState().gameConfigs['game-1'].temperature).toBe(0.1);
      expect(store.getState().gameConfigs['game-2'].temperature).toBe(0.7); // Unchanged
    });
  });
});
