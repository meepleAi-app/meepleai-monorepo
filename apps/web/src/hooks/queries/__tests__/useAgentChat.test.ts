/**
 * useAgentChat Hook Tests (Task #6)
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { api } from '@/lib/api';

import { useAgentChat } from '../useAgentChat';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      chat: vi.fn(),
    },
  },
}));

describe('useAgentChat', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('initializes with empty state', () => {
    const { result } = renderHook(() => useAgentChat('agent-1'));

    expect(result.current.messages).toEqual([]);
    expect(result.current.isStreaming).toBe(false);
    expect(result.current.error).toBeNull();
  });

  it('sends message and handles SSE stream', async () => {
    const mockStream = async function* () {
      yield { type: 'Token', data: { text: 'Hello' } };
      yield { type: 'Token', data: { text: ' World' } };
      yield { type: 'Complete', data: {} };
    };

    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(mockStream());

    const onToken = vi.fn();
    const onComplete = vi.fn();

    const { result } = renderHook(() =>
      useAgentChat('agent-1', { onToken, onComplete })
    );

    // Send message
    result.current.sendMessage('Test message');

    // Should add user message immediately
    await waitFor(() => {
      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0]).toMatchObject({
        role: 'user',
        content: 'Test message',
      });
    });

    // Should stream tokens
    await waitFor(() => {
      expect(onToken).toHaveBeenCalledWith('Hello');
      expect(onToken).toHaveBeenCalledWith(' World');
    });

    // Should add agent message after complete
    await waitFor(() => {
      expect(result.current.messages).toHaveLength(2);
      expect(result.current.messages[1]).toMatchObject({
        role: 'agent',
        content: 'Hello World',
      });
    });

    expect(onComplete).toHaveBeenCalled();
    expect(result.current.isStreaming).toBe(false);
  });

  it('handles SSE errors', async () => {
    const mockStream = async function* () {
      yield { type: 'Token', data: { text: 'Start' } };
      yield { type: 'Error', data: { message: 'Connection lost' } };
    };

    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(mockStream());

    const onError = vi.fn();

    const { result } = renderHook(() => useAgentChat('agent-1', { onError }));

    result.current.sendMessage('Test');

    await waitFor(() => {
      expect(result.current.error).not.toBeNull();
      expect(onError).toHaveBeenCalled();
      expect(result.current.isStreaming).toBe(false);
    });
  });

  it('handles network failures', async () => {
    (api.agents.chat as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Network error')
    );

    const onError = vi.fn();

    const { result } = renderHook(() => useAgentChat('agent-1', { onError }));

    result.current.sendMessage('Test');

    await waitFor(() => {
      expect(result.current.error).not.toBeNull();
      expect(onError).toHaveBeenCalled();
    });
  });

  it('maintains message history across multiple sends', async () => {
    const { result } = renderHook(() => useAgentChat('agent-1'));

    // Send first message
    const mockStream1 = async function* () {
      yield { type: 'Token', data: { text: 'Response 1' } };
      yield { type: 'Complete', data: {} };
    };
    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(mockStream1());

    result.current.sendMessage('Message 1');

    await waitFor(() => {
      expect(result.current.messages).toHaveLength(2); // user + agent
    });

    // Send second message
    const mockStream2 = async function* () {
      yield { type: 'Token', data: { text: 'Response 2' } };
      yield { type: 'Complete', data: {} };
    };
    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(mockStream2());

    result.current.sendMessage('Message 2');

    await waitFor(() => {
      expect(result.current.messages).toHaveLength(4); // 2 user + 2 agent
    });
  });
});
