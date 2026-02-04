'use client';

import { useState, useEffect, useCallback } from 'react';

const STORAGE_KEY = 'rag-dashboard-accordion-state';

export interface AccordionStateOptions {
  /** Default sections to open (typically first section of each group) */
  defaultOpen?: string[];
  /** Storage key override for different contexts */
  storageKey?: string;
}

/**
 * Hook for managing accordion state with localStorage persistence.
 * Remembers which sections the user has opened/closed across sessions.
 *
 * @example
 * ```tsx
 * const { openSections, toggleSection, isOpen, resetToDefaults } = useAccordionState({
 *   defaultOpen: ['overview', 'query-sim'],
 * });
 * ```
 */
export function useAccordionState(options: AccordionStateOptions = {}) {
  const { defaultOpen = [], storageKey = STORAGE_KEY } = options;

  // Initialize with defaults, will be replaced by localStorage on mount
  const [openSections, setOpenSections] = useState<string[]>(defaultOpen);
  const [isInitialized, setIsInitialized] = useState(false);

  // Load state from localStorage on mount
  useEffect(() => {
    try {
      const stored = localStorage.getItem(storageKey);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          setOpenSections(parsed);
        }
      }
    } catch {
      // If localStorage fails, keep defaults
      console.warn('[useAccordionState] Failed to load from localStorage');
    }
    setIsInitialized(true);
  }, [storageKey]);

  // Persist state to localStorage on change (after initialization)
  useEffect(() => {
    if (isInitialized) {
      try {
        localStorage.setItem(storageKey, JSON.stringify(openSections));
      } catch {
        console.warn('[useAccordionState] Failed to save to localStorage');
      }
    }
  }, [openSections, storageKey, isInitialized]);

  /**
   * Toggle a section open/closed
   */
  const toggleSection = useCallback((sectionId: string) => {
    setOpenSections((prev) => {
      if (prev.includes(sectionId)) {
        return prev.filter((id) => id !== sectionId);
      }
      return [...prev, sectionId];
    });
  }, []);

  /**
   * Set multiple sections to open at once
   */
  const setOpen = useCallback((sectionIds: string[]) => {
    setOpenSections(sectionIds);
  }, []);

  /**
   * Check if a specific section is open
   */
  const isOpen = useCallback(
    (sectionId: string) => openSections.includes(sectionId),
    [openSections]
  );

  /**
   * Reset to default open sections
   */
  const resetToDefaults = useCallback(() => {
    setOpenSections(defaultOpen);
    try {
      localStorage.removeItem(storageKey);
    } catch {
      // Ignore localStorage errors
    }
  }, [defaultOpen, storageKey]);

  /**
   * Open a section (if not already open)
   */
  const openSection = useCallback((sectionId: string) => {
    setOpenSections((prev) => {
      if (prev.includes(sectionId)) return prev;
      return [...prev, sectionId];
    });
  }, []);

  /**
   * Close a section (if open)
   */
  const closeSection = useCallback((sectionId: string) => {
    setOpenSections((prev) => prev.filter((id) => id !== sectionId));
  }, []);

  return {
    /** Currently open section IDs */
    openSections,
    /** Set open sections directly */
    setOpen,
    /** Toggle a section open/closed */
    toggleSection,
    /** Check if a section is open */
    isOpen,
    /** Reset to default open sections */
    resetToDefaults,
    /** Open a specific section */
    openSection,
    /** Close a specific section */
    closeSection,
    /** Whether state has been initialized from localStorage */
    isInitialized,
  };
}

export default useAccordionState;
