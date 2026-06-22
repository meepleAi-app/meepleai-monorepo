import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useChatDraft, CHAT_DRAFT_KEY_PREFIX } from '../use-chat-draft';

describe('useChatDraft', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  // I3 fix: suite-level safety net — restores all spies/mocks even if assertions throw.
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns empty draft when no value persisted', () => {
    const { result } = renderHook(() => useChatDraft({ sessionId: 'sess-1' }));
    expect(result.current.draft).toBe('');
  });

  it('reads existing draft from sessionStorage on mount', () => {
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`, 'hello world');
    const { result } = renderHook(() => useChatDraft({ sessionId: 'sess-1' }));
    expect(result.current.draft).toBe('hello world');
  });

  it('setDraft writes to sessionStorage', () => {
    const { result } = renderHook(() => useChatDraft({ sessionId: 'sess-1' }));
    act(() => result.current.setDraft('typing...'));
    expect(result.current.draft).toBe('typing...');
    expect(sessionStorage.getItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`)).toBe('typing...');
  });

  it('clearDraft removes from sessionStorage', () => {
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`, 'existing');
    const { result } = renderHook(() => useChatDraft({ sessionId: 'sess-1' }));
    act(() => result.current.clearDraft());
    expect(result.current.draft).toBe('');
    expect(sessionStorage.getItem(`${CHAT_DRAFT_KEY_PREFIX}sess-1`)).toBe(null);
  });

  it('sessionId=null → no-op (no sessionStorage access)', () => {
    const setSpy = vi.spyOn(Storage.prototype, 'setItem');
    const { result } = renderHook(() => useChatDraft({ sessionId: null }));
    act(() => result.current.setDraft('ignored'));
    expect(result.current.draft).toBe('');
    expect(setSpy).not.toHaveBeenCalled();
    setSpy.mockRestore();
  });

  it('distinct sessionId values use distinct keys', () => {
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-a`, 'draft a');
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-b`, 'draft b');

    const { result: a } = renderHook(() => useChatDraft({ sessionId: 'sess-a' }));
    const { result: b } = renderHook(() => useChatDraft({ sessionId: 'sess-b' }));

    expect(a.current.draft).toBe('draft a');
    expect(b.current.draft).toBe('draft b');
  });

  it('swallows quota-exceeded errors with warn', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const setItemSpy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Quota exceeded', 'QuotaExceededError');
    });

    const { result } = renderHook(() => useChatDraft({ sessionId: 'sess-1' }));
    expect(() => act(() => result.current.setDraft('won-t-fit'))).not.toThrow();
    expect(warnSpy).toHaveBeenCalled();

    setItemSpy.mockRestore();
    warnSpy.mockRestore();
  });

  // I2 fix: clearDraft must be a no-op when sessionId is null (C1 guard symmetry).
  it('clearDraft is no-op when sessionId is null', () => {
    const removeSpy = vi.spyOn(Storage.prototype, 'removeItem');
    const { result } = renderHook(() => useChatDraft({ sessionId: null }));
    act(() => result.current.clearDraft());
    expect(result.current.draft).toBe('');
    expect(removeSpy).not.toHaveBeenCalled();
    removeSpy.mockRestore();
  });

  // I3 / I1 fix: re-reads stored draft when sessionId transitions null → string.
  it('re-reads draft from sessionStorage when sessionId transitions from null', () => {
    sessionStorage.setItem(`${CHAT_DRAFT_KEY_PREFIX}sess-late`, 'late draft');

    const { result, rerender } = renderHook(
      ({ sessionId }: { sessionId: string | null }) => useChatDraft({ sessionId }),
      { initialProps: { sessionId: null as string | null } }
    );

    expect(result.current.draft).toBe('');

    rerender({ sessionId: 'sess-late' });

    expect(result.current.draft).toBe('late draft');
  });
});
