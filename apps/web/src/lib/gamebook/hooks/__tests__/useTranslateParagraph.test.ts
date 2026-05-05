/**
 * useTranslateParagraph — DEMO-ONLY hook tests (Path 5a / Nanolith demo).
 *
 * Tests cover:
 *  A. composeTranslationPrompt (pure function — exhaustive)
 *  B. useTranslateParagraph (hook — mocked useAgentChatStream)
 *
 * No real API calls. No copyrighted Nanolith data.
 * Generic UUIDs and abstract paragraph references used throughout.
 */

import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// ---------------------------------------------------------------------------
// Mock useAgentChatStream before importing the hook under test
// ---------------------------------------------------------------------------

import type { AgentChatStreamState, AgentChatStreamCallbacks } from '@/hooks/useAgentChatStream';

const INITIAL_STREAM_STATE: AgentChatStreamState = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  error: null,
  chatThreadId: null,
  totalTokens: 0,
  debugSteps: [],
  modelDowngrade: null,
  strategyTier: null,
  executionId: null,
  connectionStatus: 'idle',
  retryCount: 0,
};

// Mutable state so individual tests can override
let mockState: AgentChatStreamState = { ...INITIAL_STREAM_STATE };
const mockSendMessage = vi.fn();
const mockStopStreaming = vi.fn();
const mockReset = vi.fn();
let capturedCallbacks: AgentChatStreamCallbacks | undefined;

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: (callbacks?: AgentChatStreamCallbacks) => {
    capturedCallbacks = callbacks;
    return {
      state: mockState,
      sendMessage: mockSendMessage,
      stopStreaming: mockStopStreaming,
      reset: mockReset,
    };
  },
}));

// Now import hook and pure function after mock is registered
import { composeTranslationPrompt, useTranslateParagraph } from '../useTranslateParagraph';

// ---------------------------------------------------------------------------
// A. composeTranslationPrompt — pure function tests
// ---------------------------------------------------------------------------

describe('composeTranslationPrompt', () => {
  it('includes numeric paragraphRef as-is in the prompt', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 42 });
    expect(prompt).toContain('paragrafo 42');
  });

  it('preserves string paragraphRef verbatim', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: '14a' });
    expect(prompt).toContain('paragrafo 14a');
  });

  it('appends chapter clause when chapterContext is provided', () => {
    const prompt = composeTranslationPrompt({
      paragraphRef: 7,
      chapterContext: 'Il Mercato',
    });
    expect(prompt).toContain('del capitolo "Il Mercato"');
  });

  it('omits chapter clause when chapterContext is undefined', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 7 });
    expect(prompt).not.toContain('del capitolo');
  });

  it('omits chapter clause when chapterContext is empty string', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 7, chapterContext: '' });
    expect(prompt).not.toContain('del capitolo');
  });

  it('appends docType hint when preferDocType is provided', () => {
    const prompt = composeTranslationPrompt({
      paragraphRef: 3,
      preferDocType: 'storybook',
    });
    expect(prompt).toContain('cerca preferibilmente nel storybook');
  });

  it('omits docType hint when preferDocType is undefined', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 3 });
    expect(prompt).not.toContain('cerca preferibilmente');
  });

  it('combines chapter and docType in same prompt correctly', () => {
    const prompt = composeTranslationPrompt({
      paragraphRef: 99,
      chapterContext: 'La Resa dei Conti',
      preferDocType: 'storybook',
    });
    expect(prompt).toContain('paragrafo 99');
    expect(prompt).toContain('del capitolo "La Resa dei Conti"');
    expect(prompt).toContain('cerca preferibilmente nel storybook');
  });

  it('includes anti-hallucination guard instruction', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 1 });
    expect(prompt).toContain('Paragrafo non trovato');
  });

  it('includes source page citation instruction', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 1 });
    expect(prompt).toContain('citazione della pagina sorgente');
  });

  it('starts with the task sentence', () => {
    const prompt = composeTranslationPrompt({ paragraphRef: 5 });
    expect(prompt.trimStart()).toMatch(/^Traduci in italiano/);
  });
});

// ---------------------------------------------------------------------------
// B. useTranslateParagraph — hook tests
// ---------------------------------------------------------------------------

describe('useTranslateParagraph', () => {
  const GAME_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
  const AGENT_ID = 'ffffffff-0000-1111-2222-333333333333';

  beforeEach(() => {
    mockState = { ...INITIAL_STREAM_STATE };
    capturedCallbacks = undefined;
    vi.clearAllMocks();
    // reset() mock should also reset the stream state
    mockReset.mockImplementation(() => {
      mockState = { ...INITIAL_STREAM_STATE };
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns empty translation, no citations, not streaming in initial state', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    expect(result.current.translation).toBe('');
    expect(result.current.citations).toHaveLength(0);
    expect(result.current.isStreaming).toBe(false);
    expect(result.current.isError).toBe(false);
    expect(result.current.error).toBeNull();
    expect(result.current.lastInput).toBeNull();
  });

  it('translate() is a no-op when gameId is empty', () => {
    const { result } = renderHook(() => useTranslateParagraph({ gameId: '', agentId: AGENT_ID }));
    act(() => {
      result.current.translate({ paragraphRef: 1 });
    });
    expect(mockSendMessage).not.toHaveBeenCalled();
  });

  it('translate() is a no-op when agentId is empty', () => {
    const { result } = renderHook(() => useTranslateParagraph({ gameId: GAME_ID, agentId: '' }));
    act(() => {
      result.current.translate({ paragraphRef: 1 });
    });
    expect(mockSendMessage).not.toHaveBeenCalled();
  });

  it('translate() is a no-op when enabled=false', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID, enabled: false })
    );
    act(() => {
      result.current.translate({ paragraphRef: 1 });
    });
    expect(mockSendMessage).not.toHaveBeenCalled();
  });

  it('translate() calls sendMessage with composed prompt and agentId', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    act(() => {
      result.current.translate({ paragraphRef: 42 });
    });
    expect(mockSendMessage).toHaveBeenCalledTimes(1);
    const [calledAgentId, calledPrompt] = mockSendMessage.mock.calls[0];
    expect(calledAgentId).toBe(AGENT_ID);
    expect(calledPrompt).toContain('paragrafo 42');
    // No chatThreadId passed (fresh stateless request)
    expect(mockSendMessage.mock.calls[0].length).toBe(2);
  });

  it('translation reflects state.currentAnswer during streaming', () => {
    mockState = { ...INITIAL_STREAM_STATE, currentAnswer: 'Parziale...', isStreaming: true };
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    expect(result.current.translation).toBe('Parziale...');
    expect(result.current.isStreaming).toBe(true);
  });

  it('isError is true when connectionStatus is "error"', () => {
    mockState = {
      ...INITIAL_STREAM_STATE,
      connectionStatus: 'error',
      error: 'Stream failed',
    };
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    expect(result.current.isError).toBe(true);
    expect(result.current.error).toBe('Stream failed');
  });

  it('lastInput is updated to the most recent translate() call', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    const input = { paragraphRef: 7, chapterContext: 'Capitolo Test' };
    act(() => {
      result.current.translate(input);
    });
    expect(result.current.lastInput).toEqual(input);
  });

  it('reset() calls underlying resetStream and clears lastInput', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    act(() => {
      result.current.translate({ paragraphRef: 1 });
    });
    act(() => {
      result.current.reset();
    });
    expect(mockReset).toHaveBeenCalledTimes(1);
    expect(result.current.lastInput).toBeNull();
  });

  it('citations are populated by onComplete when inline citations are present', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );

    // Trigger translate so callbacks are captured
    act(() => {
      result.current.translate({ paragraphRef: 5 });
    });

    // Simulate the onComplete callback with an answer containing an inline citation
    act(() => {
      capturedCallbacks?.onComplete?.('Testo tradotto qui. (Storybook, p. 42)', {
        totalTokens: 100,
        chatThreadId: null,
        followUpQuestions: [],
      });
    });

    expect(result.current.citations).toHaveLength(1);
    expect(result.current.citations[0].docType).toBe('storybook');
    expect(result.current.citations[0].pageNumber).toBe(42);
  });

  it('citations remain empty when onComplete answer has no inline citations', () => {
    const { result } = renderHook(() =>
      useTranslateParagraph({ gameId: GAME_ID, agentId: AGENT_ID })
    );
    act(() => {
      result.current.translate({ paragraphRef: 5 });
    });
    act(() => {
      capturedCallbacks?.onComplete?.('Paragrafo non trovato', {
        totalTokens: 10,
        chatThreadId: null,
        followUpQuestions: [],
      });
    });
    expect(result.current.citations).toHaveLength(0);
  });
});
