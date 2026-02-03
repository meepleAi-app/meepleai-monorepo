/**
 * useGameStateSignalR Hook Tests
 *
 * Tests for SignalR connection hook for real-time game state updates.
 * @see useGameStateSignalR.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

import { useGameStateSignalR } from '../useGameStateSignalR';

// Mock SignalR
const mockConnection = {
  start: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
  invoke: vi.fn().mockResolvedValue(undefined),
  on: vi.fn(),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
};

const mockHubConnectionBuilder = {
  withUrl: vi.fn().mockReturnThis(),
  withAutomaticReconnect: vi.fn().mockReturnThis(),
  configureLogging: vi.fn().mockReturnThis(),
  build: vi.fn().mockReturnValue(mockConnection),
};

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => mockHubConnectionBuilder),
  LogLevel: {
    Information: 1,
  },
}));

// Mock game state store
const mockSetState = vi.fn();
const mockDetectConflict = vi.fn();
const mockSetConnectionStatus = vi.fn();

vi.mock('@/lib/stores/game-state-store', () => ({
  useGameStateStore: () => ({
    setState: mockSetState,
    detectConflict: mockDetectConflict,
    setConnectionStatus: mockSetConnectionStatus,
  }),
}));

describe('useGameStateSignalR', () => {
  const defaultOptions = {
    sessionId: 'test-session-123',
    enabled: true,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    process.env.NEXT_PUBLIC_API_URL = 'http://localhost:8080';
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('connection lifecycle', () => {
    it('should establish connection when enabled', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.start).toHaveBeenCalled();
      });

      expect(mockConnection.invoke).toHaveBeenCalledWith('JoinSession', 'test-session-123');
    });

    it('should not connect when disabled', () => {
      renderHook(() =>
        useGameStateSignalR({
          ...defaultOptions,
          enabled: false,
        })
      );

      expect(mockConnection.start).not.toHaveBeenCalled();
    });

    it('should not connect without sessionId', () => {
      renderHook(() =>
        useGameStateSignalR({
          sessionId: '',
          enabled: true,
        })
      );

      expect(mockConnection.start).not.toHaveBeenCalled();
    });

    it('should cleanup connection on unmount', async () => {
      const { unmount } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.start).toHaveBeenCalled();
      });

      unmount();

      expect(mockConnection.invoke).toHaveBeenCalledWith('LeaveSession', 'test-session-123');
      expect(mockConnection.stop).toHaveBeenCalled();
    });
  });

  describe('connection status', () => {
    it('should report connected status after successful connection', async () => {
      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(true);
      });

      expect(result.current.connectionError).toBeNull();
    });

    it('should handle connection failure', async () => {
      mockConnection.start.mockRejectedValueOnce(new Error('Connection failed'));

      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(result.current.connectionError).toBe('Connection failed');
      });

      expect(result.current.isConnected).toBe(false);
      expect(mockSetConnectionStatus).toHaveBeenCalledWith(false, 'Connection failed');
    });

    it('should update store connection status', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockSetConnectionStatus).toHaveBeenCalledWith(true);
      });
    });
  });

  describe('reconnection handling', () => {
    it('should setup reconnecting handler', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.onreconnecting).toHaveBeenCalled();
      });

      // Simulate reconnecting
      const reconnectingHandler = mockConnection.onreconnecting.mock.calls[0][0];
      act(() => {
        reconnectingHandler(new Error('Network error'));
      });

      expect(mockSetConnectionStatus).toHaveBeenCalledWith(false, 'Reconnecting...');
    });

    it('should rejoin session after reconnection', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.onreconnected).toHaveBeenCalled();
      });

      // Simulate reconnected
      const reconnectedHandler = mockConnection.onreconnected.mock.calls[0][0];
      act(() => {
        reconnectedHandler('new-connection-id');
      });

      expect(mockConnection.invoke).toHaveBeenCalledWith('JoinSession', 'test-session-123');
      expect(mockSetConnectionStatus).toHaveBeenCalledWith(true);
    });

    it('should handle connection close', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.onclose).toHaveBeenCalled();
      });

      // Simulate close
      const closeHandler = mockConnection.onclose.mock.calls[0][0];
      act(() => {
        closeHandler(new Error('Connection lost'));
      });

      expect(mockSetConnectionStatus).toHaveBeenCalledWith(false, 'Connection lost');
    });
  });

  describe('state updates', () => {
    it('should handle state-changed messages', async () => {
      const onStateChanged = vi.fn();

      renderHook(() =>
        useGameStateSignalR({
          ...defaultOptions,
          onStateChanged,
        })
      );

      await waitFor(() => {
        expect(mockConnection.on).toHaveBeenCalledWith('StateChanged', expect.any(Function));
      });

      // Simulate state change message
      const stateChangedHandler = mockConnection.on.mock.calls.find(
        call => call[0] === 'StateChanged'
      )[1];

      const mockState = {
        id: 'state-1',
        version: 1,
        data: { score: 100 },
      };

      act(() => {
        stateChangedHandler({
          sessionId: 'test-session-123',
          type: 'state-changed',
          data: mockState,
        });
      });

      expect(mockSetState).toHaveBeenCalledWith(mockState, 'Remote update');
      expect(onStateChanged).toHaveBeenCalledWith(mockState);
    });

    it('should handle conflict-detected messages', async () => {
      const onConflictDetected = vi.fn();

      renderHook(() =>
        useGameStateSignalR({
          ...defaultOptions,
          onConflictDetected,
        })
      );

      await waitFor(() => {
        expect(mockConnection.on).toHaveBeenCalledWith('StateChanged', expect.any(Function));
      });

      // Simulate conflict message
      const stateChangedHandler = mockConnection.on.mock.calls.find(
        call => call[0] === 'StateChanged'
      )[1];

      const mockConflict = {
        localVersion: 1,
        remoteVersion: 2,
        conflictingFields: ['score'],
      };

      act(() => {
        stateChangedHandler({
          sessionId: 'test-session-123',
          type: 'conflict-detected',
          data: mockConflict,
        });
      });

      expect(mockDetectConflict).toHaveBeenCalledWith(mockConflict);
      expect(onConflictDetected).toHaveBeenCalledWith(mockConflict);
    });

    it('should ignore messages from other sessions', async () => {
      const onStateChanged = vi.fn();

      renderHook(() =>
        useGameStateSignalR({
          ...defaultOptions,
          onStateChanged,
        })
      );

      await waitFor(() => {
        expect(mockConnection.on).toHaveBeenCalled();
      });

      // Simulate message from different session
      const stateChangedHandler = mockConnection.on.mock.calls.find(
        call => call[0] === 'StateChanged'
      )[1];

      act(() => {
        stateChangedHandler({
          sessionId: 'different-session',
          type: 'state-changed',
          data: { id: 'state-1' },
        });
      });

      expect(mockSetState).not.toHaveBeenCalled();
      expect(onStateChanged).not.toHaveBeenCalled();
    });

    it('should handle snapshot-created messages without error', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockConnection.on).toHaveBeenCalled();
      });

      const stateChangedHandler = mockConnection.on.mock.calls.find(
        call => call[0] === 'StateChanged'
      )[1];

      // Should not throw
      act(() => {
        stateChangedHandler({
          sessionId: 'test-session-123',
          type: 'snapshot-created',
          data: { snapshotId: 'snap-1' },
        });
      });
    });
  });

  describe('broadcastChange', () => {
    it('should broadcast state changes when connected', async () => {
      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(true);
      });

      const change = { score: 200 };
      await act(async () => {
        await result.current.broadcastChange(change);
      });

      expect(mockConnection.invoke).toHaveBeenCalledWith(
        'BroadcastStateChange',
        'test-session-123',
        change
      );
    });

    it('should throw error when not connected', async () => {
      mockConnection.start.mockRejectedValueOnce(new Error('Failed'));

      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(false);
      });

      await expect(result.current.broadcastChange({ score: 100 })).rejects.toThrow(
        'SignalR not connected'
      );
    });

    it('should propagate broadcast errors', async () => {
      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(true);
      });

      mockConnection.invoke.mockRejectedValueOnce(new Error('Broadcast failed'));

      await expect(result.current.broadcastChange({ score: 100 })).rejects.toThrow(
        'Broadcast failed'
      );
    });
  });

  describe('configuration', () => {
    it('should use correct hub URL', async () => {
      process.env.NEXT_PUBLIC_API_URL = 'https://api.example.com';

      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockHubConnectionBuilder.withUrl).toHaveBeenCalledWith(
          'https://api.example.com/hubs/gamestate',
          expect.any(Object)
        );
      });
    });

    it('should configure automatic reconnect', async () => {
      renderHook(() => useGameStateSignalR(defaultOptions));

      await waitFor(() => {
        expect(mockHubConnectionBuilder.withAutomaticReconnect).toHaveBeenCalledWith({
          nextRetryDelayInMilliseconds: expect.any(Function),
        });
      });

      // Test exponential backoff
      const reconnectConfig = mockHubConnectionBuilder.withAutomaticReconnect.mock.calls[0][0];
      expect(reconnectConfig.nextRetryDelayInMilliseconds({ previousRetryCount: 0 })).toBe(0);
      expect(reconnectConfig.nextRetryDelayInMilliseconds({ previousRetryCount: 1 })).toBe(2000);
      expect(reconnectConfig.nextRetryDelayInMilliseconds({ previousRetryCount: 2 })).toBe(10000);
      expect(reconnectConfig.nextRetryDelayInMilliseconds({ previousRetryCount: 3 })).toBe(30000);
      expect(reconnectConfig.nextRetryDelayInMilliseconds({ previousRetryCount: 4 })).toBe(60000);
    });
  });

  describe('return value stability', () => {
    it('should return consistent structure', async () => {
      const { result } = renderHook(() => useGameStateSignalR(defaultOptions));

      expect(result.current).toHaveProperty('isConnected');
      expect(result.current).toHaveProperty('connectionError');
      expect(result.current).toHaveProperty('broadcastChange');
      expect(typeof result.current.broadcastChange).toBe('function');
    });
  });
});
