import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import React, { useRef } from 'react';

import { useScrollAnchor } from '../use-scroll-anchor';

interface FakeObserver {
  callback: IntersectionObserverCallback;
  observe: ReturnType<typeof vi.fn>;
  disconnect: ReturnType<typeof vi.fn>;
}

let fakeObservers: FakeObserver[] = [];

function installIntersectionObserverMock(): void {
  fakeObservers = [];
  vi.stubGlobal(
    'IntersectionObserver',
    vi.fn(function (this: unknown, cb: IntersectionObserverCallback) {
      const observer: FakeObserver = {
        callback: cb,
        observe: vi.fn(),
        disconnect: vi.fn(),
      };
      fakeObservers.push(observer);
      // When called with `new`, the constructor return value must be an object.
      return observer as unknown as IntersectionObserver;
    })
  );
}

function fireIntersection(isIntersecting: boolean): void {
  for (const obs of fakeObservers) {
    act(() => {
      obs.callback(
        [{ isIntersecting } as IntersectionObserverEntry],
        obs as unknown as IntersectionObserver
      );
    });
  }
}

describe('useScrollAnchor', () => {
  beforeEach(() => {
    installIntersectionObserverMock();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('starts with isAtBottom=true (default optimistic)', () => {
    const { result } = renderHook(() => {
      const containerRef = useRef<HTMLDivElement>(null);
      const bottomRef = useRef<HTMLDivElement>(null);
      return useScrollAnchor({ containerRef, bottomRef, trigger: 0 });
    });
    expect(result.current.isAtBottom).toBe(true);
  });

  it('flips isAtBottom to false when sentinel leaves viewport', () => {
    const { result } = renderHook(() => {
      const containerRef = useRef<HTMLDivElement>(null);
      const bottomRef = useRef<HTMLDivElement>(null);

      // Provide a non-null sentinel so the effect does not exit early.
      if (bottomRef.current === null) {
        (bottomRef as React.MutableRefObject<HTMLDivElement>).current =
          document.createElement('div');
      }

      return useScrollAnchor({ containerRef, bottomRef, trigger: 0 });
    });

    fireIntersection(false);
    expect(result.current.isAtBottom).toBe(false);

    fireIntersection(true);
    expect(result.current.isAtBottom).toBe(true);
  });

  it('scrollToBottom calls scrollIntoView on bottomRef', () => {
    const scrollIntoView = vi.fn();

    function TestHook() {
      const containerRef = useRef<HTMLDivElement>(null);
      const bottomRef = useRef<HTMLDivElement>(null);

      if (bottomRef.current === null) {
        (bottomRef as React.MutableRefObject<HTMLDivElement>).current = {
          scrollIntoView,
        } as unknown as HTMLDivElement;
      }

      return useScrollAnchor({ containerRef, bottomRef, trigger: 0 });
    }

    const { result } = renderHook(TestHook);

    act(() => {
      result.current.scrollToBottom();
    });

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth' });
  });

  it('falls back to isAtBottom=true when IntersectionObserver is undefined', () => {
    vi.unstubAllGlobals();
    // @ts-expect-error — deleting global for fallback path
    delete (globalThis as { IntersectionObserver?: unknown }).IntersectionObserver;

    const { result } = renderHook(() => {
      const containerRef = useRef<HTMLDivElement>(null);
      const bottomRef = useRef<HTMLDivElement>(null);
      return useScrollAnchor({ containerRef, bottomRef, trigger: 0 });
    });

    expect(result.current.isAtBottom).toBe(true);
  });

  it('re-registers observer when trigger changes (e.g. messages.length grows)', () => {
    const sentinel = document.createElement('div');

    function TestHook({ trigger }: { trigger: number }) {
      const containerRef = useRef<HTMLDivElement>(null);
      const bottomRef = useRef<HTMLDivElement>(null);

      if (bottomRef.current === null) {
        (bottomRef as React.MutableRefObject<HTMLDivElement>).current = sentinel;
      }

      return useScrollAnchor({ containerRef, bottomRef, trigger });
    }

    const { rerender } = renderHook(({ trigger }) => TestHook({ trigger }), {
      initialProps: { trigger: 0 },
    });

    // First observer registered on initial render
    expect(fakeObservers.length).toBe(1);
    const firstObserver = fakeObservers[0];

    // trigger change → effect re-runs → old observer disconnected + new one created
    rerender({ trigger: 1 });

    expect(firstObserver!.disconnect).toHaveBeenCalled();
    expect(fakeObservers.length).toBe(2);
  });
});
