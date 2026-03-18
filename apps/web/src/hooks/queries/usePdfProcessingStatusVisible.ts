/**
 * usePdfProcessingStatusVisible — Visibility-aware PDF status polling
 *
 * Wraps usePdfProcessingStatus with an IntersectionObserver so polling
 * only runs when the associated DOM element is visible in the viewport.
 */

import { useEffect, useRef, useState, type RefObject } from 'react';

import { usePdfProcessingStatus } from './usePdfProcessingStatus';

/**
 * @param gameId - UUID of the game (null/undefined disables polling)
 * @param elementRef - ref to the DOM element to observe for visibility
 */
export function usePdfProcessingStatusVisible(
  gameId: string | null | undefined,
  elementRef: RefObject<HTMLElement | null>
) {
  const [isVisible, setIsVisible] = useState(false);
  const observerRef = useRef<IntersectionObserver | null>(null);

  useEffect(() => {
    const el = elementRef.current;
    if (!el) return;

    observerRef.current = new IntersectionObserver(
      ([entry]) => setIsVisible(entry.isIntersecting),
      { threshold: 0.1 }
    );

    observerRef.current.observe(el);

    return () => {
      observerRef.current?.disconnect();
    };
  }, [elementRef]);

  // Only poll when both gameId is valid AND element is visible.
  // When not visible, passes null → inner hook sets enabled:false.
  // Note: queryKey becomes ['pdf-status', ''] for disabled queries — this is
  // fine because the query never fires (enabled:false), so deduplication
  // between visible/hidden cards of the same gameId is not an issue.
  return usePdfProcessingStatus(isVisible ? gameId : null);
}
