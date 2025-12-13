/**
 * RuleSpecLockStore Tests (Issue #2055)
 *
 * Tests for collaborative editing lock management store.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from '@testing-library/react';
import {
  useRuleSpecLockStore,
  selectHasLock,
  selectIsLockedByOther,
  selectLockHolder,
  selectAcquisitionStatus,
  type LockApiClient,
} from '../RuleSpecLockStore';
import type { EditorLock } from '@/lib/api/schemas';

// Mock lock data
const mockLock: EditorLock = {
  gameId: '123e4567-e89b-12d3-a456-426614174000',
  lockedByUserId: '123e4567-e89b-12d3-a456-426614174001',
  lockedByUserEmail: 't***@e***.com',
  lockedAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
  isLocked: true,
  isCurrentUserLock: true,
};

const mockOtherUserLock: EditorLock = {
  ...mockLock,
  lockedByUserId: '123e4567-e89b-12d3-a456-426614174002',
  lockedByUserEmail: 'o***@e***.com',
  isCurrentUserLock: false,
};

const mockUnlockedStatus: EditorLock = {
  gameId: '123e4567-e89b-12d3-a456-426614174000',
  lockedByUserId: null,
  lockedByUserEmail: null,
  lockedAt: null,
  expiresAt: null,
  isLocked: false,
  isCurrentUserLock: false,
};

// Create mock API client
const createMockApiClient = (overrides?: Partial<LockApiClient>): LockApiClient => ({
  acquireEditorLock: vi.fn().mockResolvedValue({ success: true, lock: mockLock }),
  releaseEditorLock: vi.fn().mockResolvedValue(undefined),
  refreshEditorLock: vi.fn().mockResolvedValue({ success: true, lock: mockLock }),
  getEditorLockStatus: vi.fn().mockResolvedValue(mockLock),
  ...overrides,
});

describe('RuleSpecLockStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useRuleSpecLockStore.getState().reset();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  describe('initializeLock', () => {
    it('should acquire lock successfully', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock } = useRuleSpecLockStore.getState();

      let result: boolean;
      await act(async () => {
        result = await initializeLock('game-123', apiClient);
      });

      expect(result!).toBe(true);
      expect(apiClient.acquireEditorLock).toHaveBeenCalledWith('game-123');

      const state = useRuleSpecLockStore.getState();
      expect(state.acquisitionStatus).toBe('acquired');
      expect(state.lockStatus).toEqual(mockLock);
      expect(state.gameId).toBe('game-123');
    });

    it('should handle lock acquisition failure', async () => {
      const apiClient = createMockApiClient({
        acquireEditorLock: vi.fn().mockResolvedValue({
          success: false,
          lock: mockOtherUserLock,
          message: 'Lock held by another user',
        }),
      });

      const { initializeLock } = useRuleSpecLockStore.getState();

      let result: boolean;
      await act(async () => {
        result = await initializeLock('game-123', apiClient);
      });

      expect(result!).toBe(false);

      const state = useRuleSpecLockStore.getState();
      expect(state.acquisitionStatus).toBe('conflict');
      expect(state.lockError).toBe('Lock held by another user');
    });

    it('should handle API errors gracefully', async () => {
      const apiClient = createMockApiClient({
        acquireEditorLock: vi.fn().mockRejectedValue(new Error('Network error')),
      });

      const { initializeLock } = useRuleSpecLockStore.getState();

      let result: boolean;
      await act(async () => {
        result = await initializeLock('game-123', apiClient);
      });

      expect(result!).toBe(false);

      const state = useRuleSpecLockStore.getState();
      expect(state.acquisitionStatus).toBe('failed');
      expect(state.lockError).toBe('Network error');
    });

    it('should start refresh interval on successful lock', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      const state = useRuleSpecLockStore.getState();
      expect(state.refreshIntervalId).not.toBeNull();
    });

    it('should cleanup previous lock before acquiring new one', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock } = useRuleSpecLockStore.getState();

      // Acquire first lock
      await act(async () => {
        await initializeLock('game-1', apiClient);
      });

      const firstIntervalId = useRuleSpecLockStore.getState().refreshIntervalId;

      // Acquire second lock
      await act(async () => {
        await initializeLock('game-2', apiClient);
      });

      const state = useRuleSpecLockStore.getState();
      expect(state.gameId).toBe('game-2');
      // New interval should be created
      expect(state.refreshIntervalId).not.toBe(firstIntervalId);
    });
  });

  describe('releaseLock', () => {
    it('should release lock and cleanup state', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock, releaseLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      await act(async () => {
        await releaseLock(apiClient);
      });

      expect(apiClient.releaseEditorLock).toHaveBeenCalledWith('game-123');

      const state = useRuleSpecLockStore.getState();
      expect(state.lockStatus).toBeNull();
      expect(state.acquisitionStatus).toBe('idle');
      expect(state.refreshIntervalId).toBeNull();
    });

    it('should handle release errors gracefully', async () => {
      const apiClient = createMockApiClient({
        releaseEditorLock: vi.fn().mockRejectedValue(new Error('Release failed')),
      });

      const { initializeLock, releaseLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      // Should not throw
      await act(async () => {
        await releaseLock(apiClient);
      });

      // State should still be cleaned up
      const state = useRuleSpecLockStore.getState();
      expect(state.acquisitionStatus).toBe('idle');
    });
  });

  describe('refreshLock', () => {
    it('should refresh lock successfully', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock, refreshLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      let result: boolean;
      await act(async () => {
        result = await refreshLock(apiClient);
      });

      expect(result!).toBe(true);
      expect(apiClient.refreshEditorLock).toHaveBeenCalledWith('game-123');

      const state = useRuleSpecLockStore.getState();
      expect(state.lastRefreshAt).not.toBeNull();
    });

    it('should handle refresh failure (lock lost)', async () => {
      const apiClient = createMockApiClient({
        refreshEditorLock: vi.fn().mockResolvedValue({
          success: false,
          lock: mockOtherUserLock,
          message: 'Lock expired',
        }),
      });

      const { initializeLock, refreshLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      // Override the refresh mock after initialization
      apiClient.refreshEditorLock = vi.fn().mockResolvedValue({
        success: false,
        lock: mockOtherUserLock,
        message: 'Lock expired',
      });

      let result: boolean;
      await act(async () => {
        result = await refreshLock(apiClient);
      });

      expect(result!).toBe(false);

      const state = useRuleSpecLockStore.getState();
      expect(state.acquisitionStatus).toBe('conflict');
    });

    it('should not refresh if not acquired', async () => {
      const apiClient = createMockApiClient();
      const { refreshLock } = useRuleSpecLockStore.getState();

      let result: boolean;
      await act(async () => {
        result = await refreshLock(apiClient);
      });

      expect(result!).toBe(false);
      expect(apiClient.refreshEditorLock).not.toHaveBeenCalled();
    });
  });

  describe('conflict handling', () => {
    it('should store conflict information', () => {
      const { handleConflict } = useRuleSpecLockStore.getState();

      const localVersion = {
        id: '1',
        gameId: 'game-123',
        version: '1.0',
        createdAt: new Date().toISOString(),
        createdByUserId: null,
        parentVersionId: null,
        atoms: [{ id: 'atom-1', text: 'Local rule', section: null, page: null, line: null }],
      };

      const remoteVersion = {
        ...localVersion,
        version: '1.1',
        atoms: [{ id: 'atom-2', text: 'Remote rule', section: null, page: null, line: null }],
      };

      act(() => {
        handleConflict(localVersion, remoteVersion, 'Concurrent modification detected');
      });

      const state = useRuleSpecLockStore.getState();
      expect(state.showConflictModal).toBe(true);
      expect(state.conflict).not.toBeNull();
      expect(state.conflict?.localVersion).toEqual(localVersion);
      expect(state.conflict?.remoteVersion).toEqual(remoteVersion);
      expect(state.conflict?.conflictReason).toBe('Concurrent modification detected');
    });

    it('should resolve conflict with local version', () => {
      const { handleConflict, resolveConflict } = useRuleSpecLockStore.getState();

      const localVersion = {
        id: '1',
        gameId: 'game-123',
        version: '1.0',
        createdAt: new Date().toISOString(),
        createdByUserId: null,
        parentVersionId: null,
        atoms: [{ id: 'atom-1', text: 'Local rule', section: null, page: null, line: null }],
      };

      const remoteVersion = {
        ...localVersion,
        version: '1.1',
        atoms: [{ id: 'atom-2', text: 'Remote rule', section: null, page: null, line: null }],
      };

      act(() => {
        handleConflict(localVersion, remoteVersion, 'Test conflict');
      });

      let result: ReturnType<typeof resolveConflict>;
      act(() => {
        result = resolveConflict('local');
      });

      expect(result).toEqual(localVersion);

      const state = useRuleSpecLockStore.getState();
      expect(state.showConflictModal).toBe(false);
      expect(state.conflict).toBeNull();
    });

    it('should resolve conflict with remote version', () => {
      const { handleConflict, resolveConflict } = useRuleSpecLockStore.getState();

      const localVersion = {
        id: '1',
        gameId: 'game-123',
        version: '1.0',
        createdAt: new Date().toISOString(),
        createdByUserId: null,
        parentVersionId: null,
        atoms: [{ id: 'atom-1', text: 'Local rule', section: null, page: null, line: null }],
      };

      const remoteVersion = {
        ...localVersion,
        version: '1.1',
        atoms: [{ id: 'atom-2', text: 'Remote rule', section: null, page: null, line: null }],
      };

      act(() => {
        handleConflict(localVersion, remoteVersion, 'Test conflict');
      });

      let result: ReturnType<typeof resolveConflict>;
      act(() => {
        result = resolveConflict('remote');
      });

      expect(result).toEqual(remoteVersion);
    });

    it('should merge versions by combining unique atoms', () => {
      const { handleConflict, resolveConflict } = useRuleSpecLockStore.getState();

      const localVersion = {
        id: '1',
        gameId: 'game-123',
        version: '1.0',
        createdAt: new Date().toISOString(),
        createdByUserId: null,
        parentVersionId: null,
        atoms: [
          { id: 'atom-1', text: 'Shared rule', section: null, page: null, line: null },
          { id: 'atom-local', text: 'Local only', section: null, page: null, line: null },
        ],
      };

      const remoteVersion = {
        ...localVersion,
        version: '1.1',
        atoms: [
          { id: 'atom-1', text: 'Shared rule', section: null, page: null, line: null },
          { id: 'atom-remote', text: 'Remote only', section: null, page: null, line: null },
        ],
      };

      act(() => {
        handleConflict(localVersion, remoteVersion, 'Test conflict');
      });

      let result: ReturnType<typeof resolveConflict>;
      act(() => {
        result = resolveConflict('merge');
      });

      expect(result).not.toBeNull();
      expect(result!.atoms).toHaveLength(3);
      expect(result!.atoms.map(a => a.id)).toContain('atom-1');
      expect(result!.atoms.map(a => a.id)).toContain('atom-local');
      expect(result!.atoms.map(a => a.id)).toContain('atom-remote');
    });
  });

  describe('selectors', () => {
    it('selectHasLock returns true when user has lock', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      expect(selectHasLock(useRuleSpecLockStore.getState())).toBe(true);
    });

    it('selectHasLock returns false when idle', () => {
      expect(selectHasLock(useRuleSpecLockStore.getState())).toBe(false);
    });

    it('selectIsLockedByOther returns true when another user has lock', async () => {
      const apiClient = createMockApiClient({
        acquireEditorLock: vi.fn().mockResolvedValue({
          success: false,
          lock: mockOtherUserLock,
        }),
      });

      const { initializeLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      expect(selectIsLockedByOther(useRuleSpecLockStore.getState())).toBe(true);
    });

    it('selectLockHolder returns email of lock holder', async () => {
      const apiClient = createMockApiClient({
        acquireEditorLock: vi.fn().mockResolvedValue({
          success: false,
          lock: mockOtherUserLock,
        }),
      });

      const { initializeLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      expect(selectLockHolder(useRuleSpecLockStore.getState())).toBe('o***@e***.com');
    });

    it('selectAcquisitionStatus reflects current state', async () => {
      expect(selectAcquisitionStatus(useRuleSpecLockStore.getState())).toBe('idle');

      const apiClient = createMockApiClient();
      const { initializeLock } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      expect(selectAcquisitionStatus(useRuleSpecLockStore.getState())).toBe('acquired');
    });
  });

  describe('reset and cleanup', () => {
    it('should reset all state to initial values', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock, reset } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      act(() => {
        reset();
      });

      const state = useRuleSpecLockStore.getState();
      expect(state.gameId).toBeNull();
      expect(state.lockStatus).toBeNull();
      expect(state.acquisitionStatus).toBe('idle');
      expect(state.refreshIntervalId).toBeNull();
    });

    it('cleanup should clear intervals', async () => {
      const apiClient = createMockApiClient();
      const { initializeLock, cleanup } = useRuleSpecLockStore.getState();

      await act(async () => {
        await initializeLock('game-123', apiClient);
      });

      expect(useRuleSpecLockStore.getState().refreshIntervalId).not.toBeNull();

      act(() => {
        cleanup();
      });

      expect(useRuleSpecLockStore.getState().refreshIntervalId).toBeNull();
    });
  });

  describe('ETag management', () => {
    it('should store and retrieve ETag', () => {
      const { setCurrentETag } = useRuleSpecLockStore.getState();

      act(() => {
        setCurrentETag('AAAA==');
      });

      expect(useRuleSpecLockStore.getState().currentETag).toBe('AAAA==');
    });

    it('should clear ETag when set to null', () => {
      const { setCurrentETag } = useRuleSpecLockStore.getState();

      act(() => {
        setCurrentETag('AAAA==');
      });

      act(() => {
        setCurrentETag(null);
      });

      expect(useRuleSpecLockStore.getState().currentETag).toBeNull();
    });
  });
});
