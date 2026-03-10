/**
 * useSidebarState Hook
 * Manages sidebar collapsed/expanded state with localStorage + cookie persistence.
 *
 * Migrated to safeStorage (Issue #5238) for validated reads.
 * Cookie sync added for RSC support — Server Components can read the cookie
 * to render the correct layout without a client-side flash.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { z } from 'zod';

import { SIDEBAR_COOKIE_MAX_AGE, SIDEBAR_COOKIE_NAME } from '@/lib/cookies/sidebar-cookie';
import { safeStorage } from '@/lib/storage/safeStorage';

const STORAGE_KEY = 'meepleai-sidebar-collapsed';
const BoolStringSchema = z.enum(['true', 'false']);

export interface UseSidebarStateReturn {
  /** Whether the sidebar is collapsed */
  isCollapsed: boolean;
  /** Toggle between collapsed and expanded */
  toggle: () => void;
  /** Set collapsed state directly */
  setCollapsed: (collapsed: boolean) => void;
}

function syncCookie(value: boolean): void {
  try {
    document.cookie = `${SIDEBAR_COOKIE_NAME}=${value}; path=/; max-age=${SIDEBAR_COOKIE_MAX_AGE}; SameSite=Lax`;
  } catch {
    // Cookie write may fail in non-browser environments
  }
}

export function useSidebarState(initialValue = false): UseSidebarStateReturn {
  const [isCollapsed, setIsCollapsed] = useState(initialValue);

  // Read stored value on mount and sync cookie.
  // If localStorage has a valid value, use it as source of truth.
  // Otherwise keep initialValue (from RSC cookie) and sync it to cookie.
  useEffect(() => {
    const stored = safeStorage.getRaw(STORAGE_KEY, '');
    const result = BoolStringSchema.safeParse(stored);
    if (result.success) {
      const fromStorage = result.data === 'true';
      setIsCollapsed(fromStorage);
      syncCookie(fromStorage);
    } else {
      // No valid localStorage value — keep initialValue and sync to cookie
      syncCookie(initialValue);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const setCollapsed = useCallback((collapsed: boolean) => {
    setIsCollapsed(collapsed);
    safeStorage.setRaw(STORAGE_KEY, String(collapsed));
    syncCookie(collapsed);
  }, []);

  const toggle = useCallback(() => {
    setCollapsed(!isCollapsed);
  }, [isCollapsed, setCollapsed]);

  return { isCollapsed, toggle, setCollapsed };
}
