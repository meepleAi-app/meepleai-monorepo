/**
 * useAgentSessionLaunch Tests
 * Issue #3375 - Agent Session Launch API Integration
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useAgentSessionLaunch } from '../useAgentSessionLaunch';

// Use vi.hoisted to define mocks before vi.mock hoisting
const { mockPush, mockToast } = vi.hoisted(() => ({
  mockPush: vi.fn(),
  mockToast: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

// Mock sonner toast - the hook imports directly from 'sonner'
vi.mock('sonner', () => ({
  toast: {
    success: mockToast,
    error: mockToast,
  },
}));

const mockLaunch = vi.fn();

vi.mock('@/lib/api/core/httpClient', () => ({
  createHttpClient: () => ({}),
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

vi.mock('@/lib/api/clients/agentSessionsClient', () => ({
  createAgentSessionsClient: () => ({
    launch: mockLaunch,
  }),
}));

vi.mock('@/stores/agentStore', () => ({
  useAgentStore: () => ({
    selectedTypologyId: 'typology-123',
    selectedModelId: 'model-456',
    selectedGameId: 'game-789',
    selectedStrategyId: 'BALANCED',
  }),
}));

describe('useAgentSessionLaunch', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLaunch.mockResolvedValue({ agentSessionId: 'session-abc' });
  });

  it('initializes with correct default state', () => {
    const { result } = renderHook(() => useAgentSessionLaunch());

    expect(result.current.isLaunching).toBe(false);
    expect(result.current.error).toBeNull();
    expect(typeof result.current.launch).toBe('function');
    expect(typeof result.current.reset).toBe('function');
  });

  it('launches agent session successfully', async () => {
    const onSuccess = vi.fn();
    const { result } = renderHook(() => useAgentSessionLaunch({ onSuccess }));

    let agentSessionId: string | null = null;

    await act(async () => {
      agentSessionId = await result.current.launch('game-session-123');
    });

    expect(agentSessionId).toBe('session-abc');
    expect(mockLaunch).toHaveBeenCalledWith('game-session-123', {
      typologyId: 'typology-123',
      agentId: 'typology-123',
      gameId: 'game-789',
      initialGameStateJson: '{}',
    });
    expect(onSuccess).toHaveBeenCalledWith('session-abc');
    // sonner API: toast.success(title, { description })
    expect(mockToast).toHaveBeenCalledWith('Agent Launched', expect.anything());
  });

  it('redirects to chat page after successful launch', async () => {
    const { result } = renderHook(() => useAgentSessionLaunch({ redirectToChat: true }));

    await act(async () => {
      await result.current.launch('game-session-123');
    });

    expect(mockPush).toHaveBeenCalledWith('/games/game-789/chat?session=session-abc');
  });

  it('does not redirect when redirectToChat is false', async () => {
    const { result } = renderHook(() => useAgentSessionLaunch({ redirectToChat: false }));

    await act(async () => {
      await result.current.launch('game-session-123');
    });

    expect(mockPush).not.toHaveBeenCalled();
  });

  it('handles launch error', async () => {
    const error = new Error('API Error');
    mockLaunch.mockRejectedValue(error);

    const onError = vi.fn();
    const { result } = renderHook(() => useAgentSessionLaunch({ onError }));

    let agentSessionId: string | null = null;

    await act(async () => {
      agentSessionId = await result.current.launch('game-session-123');
    });

    expect(agentSessionId).toBeNull();
    expect(result.current.error).toEqual(error);
    expect(onError).toHaveBeenCalledWith(error);
    // sonner API: toast.error(title, { description })
    expect(mockToast).toHaveBeenCalledWith('Launch Failed', expect.anything());
  });

  it('handles quota exceeded error', async () => {
    mockLaunch.mockRejectedValue(new Error('quota exceeded'));

    const { result } = renderHook(() => useAgentSessionLaunch());

    await act(async () => {
      await result.current.launch('game-session-123');
    });

    // sonner API: toast.error(title, { description })
    expect(mockToast).toHaveBeenCalledWith('Quota Exceeded', expect.anything());
  });

  it('handles invalid configuration error', async () => {
    mockLaunch.mockRejectedValue(new Error('invalid configuration'));

    const { result } = renderHook(() => useAgentSessionLaunch());

    await act(async () => {
      await result.current.launch('game-session-123');
    });

    // sonner API: toast.error(title, { description })
    expect(mockToast).toHaveBeenCalledWith('Invalid Configuration', expect.anything());
  });

  it('uses override values when provided', async () => {
    const { result } = renderHook(() => useAgentSessionLaunch());

    await act(async () => {
      await result.current.launch('game-session-123', {
        typologyId: 'override-typology',
        gameId: 'override-game',
      });
    });

    expect(mockLaunch).toHaveBeenCalledWith('game-session-123', {
      typologyId: 'override-typology',
      agentId: 'override-typology',
      gameId: 'override-game',
      initialGameStateJson: '{}',
    });
  });

  it('resets error state', async () => {
    mockLaunch.mockRejectedValue(new Error('Test error'));

    const { result } = renderHook(() => useAgentSessionLaunch());

    await act(async () => {
      await result.current.launch('game-session-123');
    });

    expect(result.current.error).not.toBeNull();

    act(() => {
      result.current.reset();
    });

    expect(result.current.error).toBeNull();
    expect(result.current.isLaunching).toBe(false);
  });

  it('sets isLaunching during launch', async () => {
    // Use a promise that we can control
    let resolvePromise: (value: { agentSessionId: string }) => void;
    const launchPromise = new Promise<{ agentSessionId: string }>(resolve => {
      resolvePromise = resolve;
    });
    mockLaunch.mockReturnValue(launchPromise);

    const { result } = renderHook(() => useAgentSessionLaunch());

    expect(result.current.isLaunching).toBe(false);

    // Start launch but don't await
    let launchCompletion: Promise<string | null>;
    act(() => {
      launchCompletion = result.current.launch('game-session-123');
    });

    // Should be launching
    await waitFor(() => {
      expect(result.current.isLaunching).toBe(true);
    });

    // Resolve the promise
    await act(async () => {
      resolvePromise!({ agentSessionId: 'session-abc' });
      await launchCompletion;
    });

    // Should no longer be launching
    expect(result.current.isLaunching).toBe(false);
  });
});
