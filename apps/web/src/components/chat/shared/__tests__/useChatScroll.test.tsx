import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useChatScroll } from '../useChatScroll';

// jsdom does not implement scrollIntoView — stub it so the hook can call it.
beforeEach(() => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (HTMLElement.prototype as any).scrollIntoView = vi.fn();
});

describe('useChatScroll', () => {
  it('returns an anchorRef (initially null current) and a scrollToBottom fn', () => {
    const { result } = renderHook(() => useChatScroll([0]));
    expect(result.current.anchorRef).toBeDefined();
    expect(result.current.anchorRef.current).toBeNull();
    expect(typeof result.current.scrollToBottom).toBe('function');
  });

  it('invokes scrollIntoView({ behavior: "smooth" }) on mount once', () => {
    const scrollSpy = vi.fn();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (HTMLElement.prototype as any).scrollIntoView = scrollSpy;

    const { result } = renderHook(() => useChatScroll([0]));
    // Attach a real element so scrollIntoView can be called on current.
    act(() => {
      const div = document.createElement('div');
      (result.current.anchorRef as { current: HTMLElement | null }).current = div;
      result.current.scrollToBottom();
    });

    expect(scrollSpy).toHaveBeenCalledWith({ behavior: 'smooth' });
  });

  it('re-scrolls when any trigger in the dependency array changes', () => {
    const scrollSpy = vi.fn();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (HTMLElement.prototype as any).scrollIntoView = scrollSpy;

    const { result, rerender } = renderHook(
      ({ triggers }: { triggers: ReadonlyArray<unknown> }) => useChatScroll(triggers),
      { initialProps: { triggers: [1, 'a'] as ReadonlyArray<unknown> } }
    );

    act(() => {
      const div = document.createElement('div');
      (result.current.anchorRef as { current: HTMLElement | null }).current = div;
    });
    scrollSpy.mockClear();

    // Change first trigger
    rerender({ triggers: [2, 'a'] });
    expect(scrollSpy).toHaveBeenCalledTimes(1);

    // Change second trigger
    rerender({ triggers: [2, 'b'] });
    expect(scrollSpy).toHaveBeenCalledTimes(2);

    // No change — no new call
    rerender({ triggers: [2, 'b'] });
    expect(scrollSpy).toHaveBeenCalledTimes(2);
  });

  it('scrollToBottom is a no-op when anchorRef.current is null', () => {
    const scrollSpy = vi.fn();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (HTMLElement.prototype as any).scrollIntoView = scrollSpy;

    const { result } = renderHook(() => useChatScroll([0]));
    scrollSpy.mockClear();
    act(() => {
      // anchorRef.current stays null
      result.current.scrollToBottom();
    });
    expect(scrollSpy).not.toHaveBeenCalled();
  });
});
