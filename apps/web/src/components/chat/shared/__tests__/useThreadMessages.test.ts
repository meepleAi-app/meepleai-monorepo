/**
 * Unit tests for the `useThreadMessages` reducer + hook scaffold.
 *
 * Phase 1, Task 2: verifies the pure reducer and the initial hook surface.
 * Streaming/SSE behavior is intentionally NOT exercised here — those land
 * in Tasks 4-6 once `sendMessage`/`continueStream` are wired.
 *
 * The reducer is the load-bearing piece: covering every action variant
 * plus bail-out fast paths (no-op REMOVE_BY_ID, identity SET_STREAM_STATUS,
 * etc.) gives us a stable foundation before ChatThreadView migrates.
 */

import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { ChatMessageItem } from '../types';
import {
  initialThreadMessagesState,
  threadMessagesReducer,
  useThreadMessages,
  type ThreadMessagesAction,
  type ThreadMessagesState,
} from '../useThreadMessages';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function userMsg(id: string, content: string): ChatMessageItem {
  return { id, role: 'user', content };
}

function assistantMsg(
  id: string,
  content: string,
  extras: Partial<ChatMessageItem> = {}
): ChatMessageItem {
  return { id, role: 'assistant', content, ...extras };
}

function seed(messages: ChatMessageItem[] = []): ThreadMessagesState {
  return { ...initialThreadMessagesState, messages };
}

// ---------------------------------------------------------------------------
// Reducer — pure function tests
// ---------------------------------------------------------------------------

describe('threadMessagesReducer', () => {
  it('starts from the documented initial state', () => {
    expect(initialThreadMessagesState).toEqual({
      messages: [],
      streamStatus: 'idle',
      currentAnswer: '',
      lastMessageWasVoice: false,
    });
  });

  describe('APPEND', () => {
    it('appends a message to an empty list', () => {
      const msg = userMsg('u1', 'hello');
      const next = threadMessagesReducer(seed(), { type: 'APPEND', message: msg });
      expect(next.messages).toEqual([msg]);
    });

    it('preserves order when appending multiple messages', () => {
      const m1 = userMsg('u1', 'hello');
      const m2 = assistantMsg('a1', 'hi');
      const m3 = userMsg('u2', 'how are you?');
      let state = seed();
      state = threadMessagesReducer(state, { type: 'APPEND', message: m1 });
      state = threadMessagesReducer(state, { type: 'APPEND', message: m2 });
      state = threadMessagesReducer(state, { type: 'APPEND', message: m3 });
      expect(state.messages.map(m => m.id)).toEqual(['u1', 'a1', 'u2']);
    });

    it('returns a new messages array (no in-place mutation)', () => {
      const before = seed([userMsg('u1', 'hello')]);
      const after = threadMessagesReducer(before, {
        type: 'APPEND',
        message: assistantMsg('a1', 'hi'),
      });
      expect(after.messages).not.toBe(before.messages);
      expect(before.messages).toEqual([userMsg('u1', 'hello')]); // untouched
    });
  });

  describe('PATCH_BY_ID', () => {
    it('merges the patch into the matching message', () => {
      const state = seed([assistantMsg('a1', 'partial')]);
      const next = threadMessagesReducer(state, {
        type: 'PATCH_BY_ID',
        id: 'a1',
        patch: { content: 'partial token' },
      });
      expect(next.messages[0]).toEqual({
        id: 'a1',
        role: 'assistant',
        content: 'partial token',
      });
    });

    it('leaves unrelated messages untouched (by reference)', () => {
      const u1 = userMsg('u1', 'hello');
      const a1 = assistantMsg('a1', 'partial');
      const state = seed([u1, a1]);
      const next = threadMessagesReducer(state, {
        type: 'PATCH_BY_ID',
        id: 'a1',
        patch: { content: 'partial token' },
      });
      expect(next.messages[0]).toBe(u1);
      expect(next.messages[1]).not.toBe(a1);
    });

    it('is a no-op when the id is not found', () => {
      const state = seed([userMsg('u1', 'hello')]);
      const next = threadMessagesReducer(state, {
        type: 'PATCH_BY_ID',
        id: 'does-not-exist',
        patch: { content: 'ignored' },
      });
      expect(next).toBe(state);
    });

    it('can patch follow-up questions without touching content', () => {
      const state = seed([assistantMsg('a1', 'done')]);
      const next = threadMessagesReducer(state, {
        type: 'PATCH_BY_ID',
        id: 'a1',
        patch: { followUpQuestions: ['what next?'] },
      });
      expect(next.messages[0]).toEqual({
        id: 'a1',
        role: 'assistant',
        content: 'done',
        followUpQuestions: ['what next?'],
      });
    });
  });

  describe('REMOVE_BY_ID', () => {
    it('removes the matching message', () => {
      const state = seed([userMsg('u1', 'a'), assistantMsg('a1', 'b'), userMsg('u2', 'c')]);
      const next = threadMessagesReducer(state, { type: 'REMOVE_BY_ID', id: 'a1' });
      expect(next.messages.map(m => m.id)).toEqual(['u1', 'u2']);
    });

    it('is a no-op when the id is not present (state identity preserved)', () => {
      const state = seed([userMsg('u1', 'a')]);
      const next = threadMessagesReducer(state, { type: 'REMOVE_BY_ID', id: 'missing' });
      expect(next).toBe(state);
    });
  });

  describe('REPLACE_ALL', () => {
    it('replaces the entire list', () => {
      const state = seed([userMsg('u1', 'old')]);
      const replacement = [assistantMsg('welcome', 'hi there')];
      const next = threadMessagesReducer(state, {
        type: 'REPLACE_ALL',
        messages: replacement,
      });
      expect(next.messages).toBe(replacement);
    });

    it('accepts an empty replacement (thread clear)', () => {
      const state = seed([userMsg('u1', 'a')]);
      const next = threadMessagesReducer(state, { type: 'REPLACE_ALL', messages: [] });
      expect(next.messages).toEqual([]);
    });
  });

  describe('SET_STREAM_STATUS', () => {
    it('transitions idle → streaming → idle', () => {
      let state = initialThreadMessagesState;
      state = threadMessagesReducer(state, { type: 'SET_STREAM_STATUS', status: 'streaming' });
      expect(state.streamStatus).toBe('streaming');
      state = threadMessagesReducer(state, { type: 'SET_STREAM_STATUS', status: 'idle' });
      expect(state.streamStatus).toBe('idle');
    });

    it('is a no-op when the status is unchanged', () => {
      const state = { ...initialThreadMessagesState, streamStatus: 'streaming' as const };
      const next = threadMessagesReducer(state, { type: 'SET_STREAM_STATUS', status: 'streaming' });
      expect(next).toBe(state);
    });
  });

  describe('SET_CURRENT_ANSWER', () => {
    it('updates the buffered answer', () => {
      const next = threadMessagesReducer(initialThreadMessagesState, {
        type: 'SET_CURRENT_ANSWER',
        answer: 'hello wo',
      });
      expect(next.currentAnswer).toBe('hello wo');
    });

    it('is a no-op when the answer is unchanged', () => {
      const state = { ...initialThreadMessagesState, currentAnswer: 'same' };
      const next = threadMessagesReducer(state, { type: 'SET_CURRENT_ANSWER', answer: 'same' });
      expect(next).toBe(state);
    });
  });

  describe('SET_VOICE_FLAG', () => {
    it('flips the voice flag on and off', () => {
      let state = initialThreadMessagesState;
      state = threadMessagesReducer(state, { type: 'SET_VOICE_FLAG', flag: true });
      expect(state.lastMessageWasVoice).toBe(true);
      state = threadMessagesReducer(state, { type: 'SET_VOICE_FLAG', flag: false });
      expect(state.lastMessageWasVoice).toBe(false);
    });

    it('is a no-op when the flag is unchanged', () => {
      const state = { ...initialThreadMessagesState, lastMessageWasVoice: true };
      const next = threadMessagesReducer(state, { type: 'SET_VOICE_FLAG', flag: true });
      expect(next).toBe(state);
    });
  });

  it('does not mutate the input state for any action', () => {
    const snapshot: ThreadMessagesState = seed([userMsg('u1', 'hello')]);
    const frozen = Object.freeze({
      ...snapshot,
      messages: Object.freeze(snapshot.messages.slice()),
    });
    const actions: ThreadMessagesAction[] = [
      { type: 'APPEND', message: assistantMsg('a1', 'hi') },
      { type: 'PATCH_BY_ID', id: 'u1', patch: { content: 'edited' } },
      { type: 'REMOVE_BY_ID', id: 'u1' },
      { type: 'REPLACE_ALL', messages: [userMsg('u2', 'new')] },
      { type: 'SET_STREAM_STATUS', status: 'streaming' },
      { type: 'SET_CURRENT_ANSWER', answer: 'buf' },
      { type: 'SET_VOICE_FLAG', flag: true },
    ];
    for (const action of actions) {
      expect(() => threadMessagesReducer(frozen, action)).not.toThrow();
    }
  });
});

// ---------------------------------------------------------------------------
// Hook — scaffold surface tests (streaming behavior deferred to Tasks 4-6)
// ---------------------------------------------------------------------------

describe('useThreadMessages (scaffold)', () => {
  it('exposes the documented initial surface', () => {
    const { result } = renderHook(() => useThreadMessages());
    expect(result.current.messages).toEqual([]);
    expect(result.current.streamStatus).toBe('idle');
    expect(result.current.currentAnswer).toBe('');
    expect(result.current.lastMessageWasVoice).toBe(false);
  });

  it('returns stable callback identities across re-renders', () => {
    const { result, rerender } = renderHook(() => useThreadMessages());
    const firstSend = result.current.sendMessage;
    const firstContinue = result.current.continueStream;
    const firstAbort = result.current.abortCurrent;
    const firstBeginAbort = result.current.beginAbort;
    const firstReplace = result.current.replaceMessages;
    const firstAppend = result.current.appendMessage;
    const firstPatch = result.current.patchMessageById;
    const firstRemove = result.current.removeMessageById;
    rerender();
    expect(result.current.sendMessage).toBe(firstSend);
    expect(result.current.continueStream).toBe(firstContinue);
    expect(result.current.abortCurrent).toBe(firstAbort);
    expect(result.current.beginAbort).toBe(firstBeginAbort);
    expect(result.current.replaceMessages).toBe(firstReplace);
    expect(result.current.appendMessage).toBe(firstAppend);
    expect(result.current.patchMessageById).toBe(firstPatch);
    expect(result.current.removeMessageById).toBe(firstRemove);
  });

  it('appendMessage dispatches APPEND (freshly-read state, not closure)', () => {
    const { result } = renderHook(() => useThreadMessages());
    act(() => {
      result.current.appendMessage(userMsg('u1', 'hello'));
    });
    act(() => {
      result.current.appendMessage(assistantMsg('a1', 'hi'));
    });
    expect(result.current.messages.map(m => m.id)).toEqual(['u1', 'a1']);
  });

  it('patchMessageById merges partial updates into the matching message', () => {
    const { result } = renderHook(() => useThreadMessages());
    act(() => {
      result.current.appendMessage(assistantMsg('a1', 'partial'));
    });
    act(() => {
      result.current.patchMessageById('a1', { content: 'final' });
    });
    expect(result.current.messages[0]).toEqual({
      id: 'a1',
      role: 'assistant',
      content: 'final',
    });
  });

  it('removeMessageById drops the matching message', () => {
    const { result } = renderHook(() => useThreadMessages());
    act(() => {
      result.current.replaceMessages([userMsg('u1', 'a'), assistantMsg('a1', 'b')]);
    });
    act(() => {
      result.current.removeMessageById('a1');
    });
    expect(result.current.messages.map(m => m.id)).toEqual(['u1']);
  });

  it('replaceMessages swaps the list synchronously', () => {
    const { result } = renderHook(() => useThreadMessages());
    const next = [assistantMsg('welcome', 'hi there')];
    act(() => {
      result.current.replaceMessages(next);
    });
    expect(result.current.messages).toEqual(next);
  });

  it('replaceMessages([]) clears the thread', () => {
    const { result } = renderHook(() => useThreadMessages());
    act(() => {
      result.current.replaceMessages([userMsg('u1', 'hello')]);
    });
    expect(result.current.messages).toHaveLength(1);
    act(() => {
      result.current.replaceMessages([]);
    });
    expect(result.current.messages).toEqual([]);
  });

  it('abortCurrent is safe to call when no stream is in flight', () => {
    const { result } = renderHook(() => useThreadMessages());
    expect(() => {
      act(() => {
        result.current.abortCurrent();
      });
    }).not.toThrow();
  });

  it('beginAbort returns a fresh AbortController and aborts the previous one', () => {
    const { result } = renderHook(() => useThreadMessages());

    let first!: AbortController;
    act(() => {
      first = result.current.beginAbort();
    });
    expect(first.signal.aborted).toBe(false);

    let second!: AbortController;
    act(() => {
      second = result.current.beginAbort();
    });
    // Starting a new abort cycle MUST cancel the previous in-flight controller
    // so the single-stream invariant holds even if callers forget to abort.
    expect(first.signal.aborted).toBe(true);
    expect(second.signal.aborted).toBe(false);
    expect(second).not.toBe(first);
  });

  it('abortCurrent aborts the controller returned by beginAbort', () => {
    const { result } = renderHook(() => useThreadMessages());
    let controller!: AbortController;
    act(() => {
      controller = result.current.beginAbort();
    });
    expect(controller.signal.aborted).toBe(false);
    act(() => {
      result.current.abortCurrent();
    });
    expect(controller.signal.aborted).toBe(true);
  });

  it('sendMessage and continueStream are no-op stubs that resolve', async () => {
    // Scaffold contract: wiring lands in Tasks 4-6. The stubs must still
    // satisfy the TS signature and not reject so call-sites can be migrated
    // incrementally without crashing.
    const { result } = renderHook(() => useThreadMessages());
    await expect(
      result.current.sendMessage('anything', { threadId: 't1' })
    ).resolves.toBeUndefined();
    await expect(result.current.continueStream('tok', { gameId: 'g1' })).resolves.toBeUndefined();
    // State remains untouched because the stubs don't dispatch.
    expect(result.current.messages).toEqual([]);
    expect(result.current.streamStatus).toBe('idle');
  });
});
