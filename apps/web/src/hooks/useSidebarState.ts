/**
 * useSidebarState Hook
 * Manages sidebar collapsed/expanded state with localStorage persistence.
 *
 * Migrated to safeStorage (Issue #5238) for validated reads.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import { z } from 'zod';

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

export function useSidebarState(): UseSidebarStateReturn {
  const [isCollapsed, setIsCollapsed] = useState(false);

  // Read stored value on mount
  useEffect(() => {
    const stored = safeStorage.getRaw(STORAGE_KEY, 'false');
    const result = BoolStringSchema.safeParse(stored);
    if (result.success && result.data === 'true') {
      setIsCollapsed(true);
    }
  }, []);

  const setCollapsed = useCallback((collapsed: boolean) => {
    setIsCollapsed(collapsed);
    safeStorage.setRaw(STORAGE_KEY, String(collapsed));
  }, []);

  const toggle = useCallback(() => {
    setCollapsed(!isCollapsed);
  }, [isCollapsed, setCollapsed]);

  return { isCollapsed, toggle, setCollapsed };
}
