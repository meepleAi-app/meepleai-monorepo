'use client';

/**
 * useScrollAnchor — Issue #2375 G3.
 *
 * IntersectionObserver-based smart scroll anchor for chat messages.
 *
 * Returns:
 *   isAtBottom — true when bottomRef intersects the viewport (last message visible)
 *   scrollToBottom — programmatic scroll-to-bottom (smooth)
 *
 * Fallback: when `IntersectionObserver` is `undefined`, isAtBottom stays `true`
 * (naive auto-scroll on every new message — degraded UX for <1% of browsers).
 *
 * Caller is responsible for using `trigger` (e.g., messages.length) to decide
 * whether to auto-scroll (`isAtBottom=true`) or show a "N nuovi messaggi" toast
 * (`isAtBottom=false`).
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2375-g3-chatagent-always-visible-design.md §4.1
 */

import { useCallback, useEffect, useState, type RefObject } from 'react';

export interface UseScrollAnchorOptions {
  readonly containerRef: RefObject<HTMLElement | null>;
  readonly bottomRef: RefObject<HTMLElement | null>;
  /** Trigger to keep observer reactive (e.g. messages.length). */
  readonly trigger: unknown;
}

export interface UseScrollAnchorReturn {
  readonly isAtBottom: boolean;
  readonly scrollToBottom: () => void;
}

export function useScrollAnchor({
  containerRef,
  bottomRef,
  trigger,
}: UseScrollAnchorOptions): UseScrollAnchorReturn {
  const [isAtBottom, setIsAtBottom] = useState<boolean>(true);

  useEffect(() => {
    if (typeof IntersectionObserver === 'undefined') {
      setIsAtBottom(true);
      return undefined;
    }

    const sentinel = bottomRef.current;
    if (sentinel == null) return undefined;

    const observer = new IntersectionObserver(
      entries => {
        const entry = entries[0];
        if (entry != null) {
          setIsAtBottom(entry.isIntersecting);
        }
      },
      { root: containerRef.current ?? null, threshold: 0.1 }
    );

    observer.observe(sentinel);

    return () => {
      observer.disconnect();
    };
  }, [containerRef, bottomRef, trigger]);

  const scrollToBottom = useCallback(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [bottomRef]);

  return { isAtBottom, scrollToBottom };
}
