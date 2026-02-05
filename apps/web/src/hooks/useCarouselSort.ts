/**
 * useCarouselSort - Carousel sorting state with persistence
 *
 * Issue #3587: GC-002 — Sorting Controls
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 *
 * Features:
 * - LocalStorage persistence
 * - URL query param sync (?sort=rating)
 * - SSR-safe with initial state from URL/localStorage
 */

'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import type { CarouselSortValue } from '@/components/ui/data-display/game-carousel';

const STORAGE_KEY_PREFIX = 'carousel-sort-';
const VALID_SORT_VALUES: CarouselSortValue[] = ['rating', 'popularity', 'name', 'date'];

/**
 * Check if a value is a valid CarouselSortValue
 */
function isValidSortValue(value: unknown): value is CarouselSortValue {
  return typeof value === 'string' && VALID_SORT_VALUES.includes(value as CarouselSortValue);
}

/**
 * Options for useCarouselSort hook
 */
export interface UseCarouselSortOptions {
  /** Unique key for this carousel (for localStorage) */
  carouselKey: string;
  /** Default sort value if no preference stored */
  defaultSort?: CarouselSortValue;
  /** Whether to sync with URL query params */
  syncWithUrl?: boolean;
  /** URL query param name (default: 'sort') */
  urlParamName?: string;
}

/**
 * Hook for managing carousel sort state with persistence
 *
 * @example
 * ```tsx
 * const { sort, setSort } = useCarouselSort({
 *   carouselKey: 'featured-games',
 *   defaultSort: 'rating',
 *   syncWithUrl: true,
 * });
 *
 * <GameCarousel
 *   sortable
 *   sort={sort}
 *   onSortChange={setSort}
 * />
 * ```
 */
export function useCarouselSort({
  carouselKey,
  defaultSort = 'rating',
  syncWithUrl = false,
  urlParamName = 'sort',
}: UseCarouselSortOptions) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Get initial value from URL or localStorage
  const getInitialValue = useCallback((): CarouselSortValue => {
    // Check URL first if syncing
    if (syncWithUrl && searchParams) {
      const urlValue = searchParams.get(urlParamName);
      if (urlValue && isValidSortValue(urlValue)) {
        return urlValue;
      }
    }

    // Check localStorage (SSR-safe)
    if (typeof window !== 'undefined') {
      try {
        const stored = localStorage.getItem(`${STORAGE_KEY_PREFIX}${carouselKey}`);
        if (stored && isValidSortValue(stored)) {
          return stored;
        }
      } catch {
        // localStorage might be unavailable
      }
    }

    return defaultSort;
  }, [carouselKey, defaultSort, searchParams, syncWithUrl, urlParamName]);

  const [sort, setSortInternal] = useState<CarouselSortValue>(getInitialValue);

  // Sync URL -> state when URL changes
  useEffect(() => {
    if (!syncWithUrl || !searchParams) return;

    const urlValue = searchParams.get(urlParamName);
    if (urlValue && isValidSortValue(urlValue) && urlValue !== sort) {
      setSortInternal(urlValue);
    }
  }, [searchParams, urlParamName, syncWithUrl, sort]);

  // Set sort and persist
  const setSort = useCallback(
    (newSort: CarouselSortValue) => {
      setSortInternal(newSort);

      // Persist to localStorage
      if (typeof window !== 'undefined') {
        try {
          localStorage.setItem(`${STORAGE_KEY_PREFIX}${carouselKey}`, newSort);
        } catch {
          // localStorage might be unavailable
        }
      }

      // Update URL if syncing
      if (syncWithUrl && pathname) {
        const params = new URLSearchParams(searchParams?.toString() ?? '');
        if (newSort === defaultSort) {
          // Remove param if it's the default value
          params.delete(urlParamName);
        } else {
          params.set(urlParamName, newSort);
        }
        const queryString = params.toString();
        const newUrl = queryString ? `${pathname}?${queryString}` : pathname;
        router.replace(newUrl, { scroll: false });
      }
    },
    [carouselKey, defaultSort, pathname, router, searchParams, syncWithUrl, urlParamName]
  );

  // Clear stored preference
  const clearPreference = useCallback(() => {
    if (typeof window !== 'undefined') {
      try {
        localStorage.removeItem(`${STORAGE_KEY_PREFIX}${carouselKey}`);
      } catch {
        // localStorage might be unavailable
      }
    }
    setSortInternal(defaultSort);
  }, [carouselKey, defaultSort]);

  return {
    /** Current sort value */
    sort,
    /** Set sort value (persists to localStorage and optionally URL) */
    setSort,
    /** Clear stored preference and reset to default */
    clearPreference,
    /** Whether current sort is the default */
    isDefault: sort === defaultSort,
  };
}
