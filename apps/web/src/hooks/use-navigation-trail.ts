/**
 * useNavigationTrail - Hook for tracking entity navigation breadcrumbs
 *
 * Manages a trail of entity navigation steps persisted in sessionStorage.
 * When users click navigation footer links, the trail extends. Direct URL
 * navigation resets it.
 *
 * @see Issue #4704
 * @see Epic #4688 - MeepleCard Navigation System
 */

'use client';

import { useCallback, useEffect, useMemo, useSyncExternalStore } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface BreadcrumbStep {
  /** Entity type — drives icon + color */
  entity: MeepleEntityType;
  /** Display label */
  label: string;
  /** Navigation href */
  href: string;
}

// ---------------------------------------------------------------------------
// sessionStorage helpers
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'meeple-nav-trail';
const HIGHLIGHT_KEY = 'meeple-nav-highlight';

function readTrail(): BreadcrumbStep[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as BreadcrumbStep[]) : [];
  } catch {
    return [];
  }
}

function writeTrail(trail: BreadcrumbStep[]): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(trail));
  } catch {
    // sessionStorage full or unavailable
  }
  // Notify all subscribers
  window.dispatchEvent(new Event('meeple-trail-change'));
}

function readHighlight(): MeepleEntityType | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = sessionStorage.getItem(HIGHLIGHT_KEY);
    return raw as MeepleEntityType | null;
  } catch {
    return null;
  }
}

function writeHighlight(entity: MeepleEntityType | null): void {
  if (typeof window === 'undefined') return;
  try {
    if (entity) {
      sessionStorage.setItem(HIGHLIGHT_KEY, entity);
    } else {
      sessionStorage.removeItem(HIGHLIGHT_KEY);
    }
  } catch {
    // ignore
  }
}

// ---------------------------------------------------------------------------
// External store for useSyncExternalStore
// ---------------------------------------------------------------------------

function subscribe(callback: () => void): () => void {
  window.addEventListener('meeple-trail-change', callback);
  return () => window.removeEventListener('meeple-trail-change', callback);
}

function getSnapshot(): string {
  if (typeof window === 'undefined') return '[]';
  return sessionStorage.getItem(STORAGE_KEY) || '[]';
}

function getServerSnapshot(): string {
  return '[]';
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export interface UseNavigationTrailReturn {
  /** Current breadcrumb trail */
  trail: BreadcrumbStep[];
  /** Push a new step onto the trail */
  push: (step: BreadcrumbStep) => void;
  /** Navigate back to a specific step index, truncating trail */
  navigateTo: (index: number) => void;
  /** Clear the entire trail */
  clear: () => void;
  /** Entity to highlight on current page (from nav footer click) */
  highlightEntity: MeepleEntityType | null;
  /** Clear the highlight after animation */
  clearHighlight: () => void;
}

export function useNavigationTrail(): UseNavigationTrailReturn {
  // Subscribe to trail changes via useSyncExternalStore
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const trail = useMemo<BreadcrumbStep[]>(() => {
    try {
      return JSON.parse(snapshot) as BreadcrumbStep[];
    } catch {
      return [];
    }
  }, [snapshot]);

  const push = useCallback((step: BreadcrumbStep) => {
    const current = readTrail();
    // Avoid duplicates: if last step has same href, don't add
    if (current.length > 0 && current[current.length - 1].href === step.href) {
      return;
    }
    // Check if we're navigating to a step already in the trail
    const existingIndex = current.findIndex(s => s.href === step.href);
    if (existingIndex >= 0) {
      // Truncate to that point
      writeTrail(current.slice(0, existingIndex + 1));
      return;
    }
    writeTrail([...current, step]);
  }, []);

  const navigateTo = useCallback((index: number) => {
    const current = readTrail();
    if (index >= 0 && index < current.length) {
      writeTrail(current.slice(0, index + 1));
    }
  }, []);

  const clear = useCallback(() => {
    writeTrail([]);
  }, []);

  // Highlight management
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const highlightEntity = useMemo(() => readHighlight(), [snapshot]);

  const clearHighlight = useCallback(() => {
    writeHighlight(null);
  }, []);

  // Auto-clear highlight after 1.5s
  useEffect(() => {
    if (highlightEntity) {
      const timer = setTimeout(() => {
        writeHighlight(null);
      }, 1500);
      return () => clearTimeout(timer);
    }
  }, [highlightEntity]);

  return { trail, push, navigateTo, clear, highlightEntity, clearHighlight };
}

// ---------------------------------------------------------------------------
// Utility: set highlight for destination card
// ---------------------------------------------------------------------------

export function setNavigationHighlight(entity: MeepleEntityType): void {
  writeHighlight(entity);
}
