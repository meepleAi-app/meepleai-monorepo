/**
 * useSessionInlineChat — inline chat state management for PlayModeMobile
 */
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockApi = vi.hoisted(() => ({
  chat: {
    createThread: vi.fn(),
    addMessage: vi.fn(),
  },
}));

vi.mock('@/lib/api', () => ({ api: mockApi }));

import { useSessionInlineChat } from '../useSessionInlineChat';

const THREAD = {
  id: 'thread-1',
  gameId: 'game-abc',
  title: null,
  createdAt: new Date().toISOString(),
  lastMessageAt: new Date().toISOString(),
  messageCount: 2,
  messages: [
    { content: 'Ciao', role: 'user', timestamp: new Date().toISOString() },
    {
      content: 'Come posso aiutarti?',
      role: 'assistant',
      timestamp: new Date().toISOString(),
      backendMessageId: 'msg-1',
    },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  mockApi.chat.createThread.mockResolvedValue(THREAD);
  mockApi.chat.addMessage.mockResolvedValue({
    ...THREAD,
    messages: [
      ...THREAD.messages,
      {
        content: 'Seconda risposta',
        role: 'assistant',
        timestamp: new Date().toISOString(),
        backendMessageId: 'msg-2',
      },
    ],
  });
});

describe('useSessionInlineChat', () => {
  it('starts with empty messages and not streaming', () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    expect(result.current.messages).toHaveLength(0);
    expect(result.current.isStreaming).toBe(false);
  });

  it('adds user message immediately on send', async () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Ciao');
    });
    expect(result.current.messages[0]).toMatchObject({ role: 'user', content: 'Ciao' });
  });

  it('calls createThread on first send with gameId', async () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Prima domanda');
    });
    expect(mockApi.chat.createThread).toHaveBeenCalledWith(
      expect.objectContaining({ gameId: 'game-abc', initialMessage: 'Prima domanda' })
    );
  });

  it('appends assistant reply after first send', async () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Ciao');
    });
    const assistantMsg = result.current.messages.find(m => m.role === 'assistant');
    expect(assistantMsg).toMatchObject({ role: 'assistant', content: 'Come posso aiutarti?' });
  });

  it('calls addMessage (not createThread) on subsequent sends', async () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Prima');
    });
    await act(async () => {
      result.current.send('Seconda');
    });
    expect(mockApi.chat.createThread).toHaveBeenCalledTimes(1);
    expect(mockApi.chat.addMessage).toHaveBeenCalledWith(
      'thread-1',
      expect.objectContaining({ content: 'Seconda', role: 'user' })
    );
  });

  it('is not streaming after send completes', async () => {
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Ciao');
    });
    expect(result.current.isStreaming).toBe(false);
  });

  it('imposta error quando createThread fallisce', async () => {
    mockApi.chat.createThread.mockRejectedValueOnce(new Error('network error'));
    const { result } = renderHook(() => useSessionInlineChat('game-abc'));
    await act(async () => {
      result.current.send('Ciao');
    });
    expect(result.current.error).toBeTruthy();
    expect(result.current.isStreaming).toBe(false);
  });
});
