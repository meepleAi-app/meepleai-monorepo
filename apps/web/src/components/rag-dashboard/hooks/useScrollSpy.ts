'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

interface UseScrollSpyOptions {
  /** Offset from top in pixels (default: 100px for header) */
  offset?: number;
  /** Custom IntersectionObserver rootMargin */
  rootMargin?: string;
  /** Whether to update URL hash on section change (default: true) */
  updateHash?: boolean;
}

/**
 * Hook that tracks which section is currently visible in the viewport
 * using IntersectionObserver. Updates URL hash without triggering scroll.
 *
 * @param sectionIds - Array of section element IDs to observe
 * @param options - Configuration options
 * @returns The ID of the currently active section
 *
 * @example
 * ```tsx
 * const activeSection = useScrollSpy(['overview', 'features', 'pricing']);
 * ```
 */
export function useScrollSpy(
  sectionIds: string[],
  options: UseScrollSpyOptions = {}
): string {
  const { offset = 100, rootMargin, updateHash = true } = options;
  const [activeId, setActiveId] = useState<string>('');
  const observerRef = useRef<IntersectionObserver | null>(null);

  // Memoize the intersection callback
  const handleIntersection = useCallback(
    (entries: IntersectionObserverEntry[]) => {
      // Find the first intersecting entry (top-most visible section)
      const intersectingEntries = entries.filter((entry) => entry.isIntersecting);

      if (intersectingEntries.length > 0) {
        // Sort by boundingClientRect.top to get the topmost section
        intersectingEntries.sort(
          (a, b) => a.boundingClientRect.top - b.boundingClientRect.top
        );
        const newActiveId = intersectingEntries[0].target.id;

        setActiveId((prevId) => {
          if (prevId !== newActiveId) {
            // Update URL hash without scroll
            if (updateHash && typeof window !== 'undefined') {
              history.replaceState(null, '', `#${newActiveId}`);
            }
            return newActiveId;
          }
          return prevId;
        });
      }
    },
    [updateHash]
  );

  // Set up IntersectionObserver
  useEffect(() => {
    if (typeof window === 'undefined' || sectionIds.length === 0) {
      return;
    }

    // Disconnect previous observer if exists
    if (observerRef.current) {
      observerRef.current.disconnect();
    }

    const observerOptions: IntersectionObserverInit = {
      rootMargin: rootMargin || `-${offset}px 0px -80% 0px`,
      threshold: 0,
    };

    observerRef.current = new IntersectionObserver(handleIntersection, observerOptions);

    // Observe all sections
    sectionIds.forEach((id) => {
      const element = document.getElementById(id);
      if (element) {
        observerRef.current?.observe(element);
      }
    });

    // Cleanup
    return () => {
      observerRef.current?.disconnect();
      observerRef.current = null;
    };
  }, [sectionIds, offset, rootMargin, handleIntersection]);

  // Handle initial hash on mount
  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const hash = window.location.hash.slice(1);
    if (hash && sectionIds.includes(hash)) {
      setActiveId(hash);
      // Scroll to the hashed section after a brief delay for DOM readiness
      setTimeout(() => {
        scrollToSection(hash, offset);
      }, 100);
    } else if (sectionIds.length > 0 && !activeId) {
      setActiveId(sectionIds[0]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return activeId;
}

/**
 * Smoothly scrolls to a section by ID with offset
 *
 * @param sectionId - The ID of the section to scroll to
 * @param offset - Offset from top in pixels (default: 100)
 *
 * @example
 * ```tsx
 * <button onClick={() => scrollToSection('features')}>Go to Features</button>
 * ```
 */
export function scrollToSection(sectionId: string, offset = 100): void {
  if (typeof window === 'undefined') {
    return;
  }

  const element = document.getElementById(sectionId);
  if (element) {
    const top = element.getBoundingClientRect().top + window.scrollY - offset;
    window.scrollTo({ top, behavior: 'smooth' });
  }
}

export type { UseScrollSpyOptions };
