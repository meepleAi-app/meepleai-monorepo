/**
 * useSidebarState Hook
 * Manages sidebar collapsed/expanded state with localStorage persistence.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';

const STORAGE_KEY = 'meepleai-sidebar-collapsed';

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
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'true') {
        setIsCollapsed(true);
      }
    } catch {
      // localStorage unavailable (SSR, privacy mode, etc.)
    }
  }, []);

  const setCollapsed = useCallback((collapsed: boolean) => {
    setIsCollapsed(collapsed);
    try {
      localStorage.setItem(STORAGE_KEY, String(collapsed));
    } catch {
      // localStorage unavailable
    }
  }, []);

  const toggle = useCallback(() => {
    setCollapsed(!isCollapsed);
  }, [isCollapsed, setCollapsed]);

  return { isCollapsed, toggle, setCollapsed };
}
