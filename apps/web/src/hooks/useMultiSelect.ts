/**
 * useMultiSelect Hook
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 *
 * Manages multi-selection state for batch operations.
 */

'use client';

import { useCallback, useMemo } from 'react';

import { useLayout } from '@/components/layout/LayoutProvider';

/**
 * Item that can be selected (must have an id)
 */
export interface Selectable {
  id: string;
}

/**
 * Hook return type
 */
export interface UseMultiSelectReturn<T extends Selectable> {
  /** Whether multi-select mode is active */
  isActive: boolean;
  /** Set of selected item IDs */
  selectedIds: Set<string>;
  /** Number of selected items */
  selectedCount: number;
  /** Toggle selection of an item */
  toggle: (id: string) => void;
  /** Select all items */
  selectAll: (items: T[]) => void;
  /** Clear all selections */
  clearSelection: () => void;
  /** Enter multi-select mode */
  enterMultiSelect: () => void;
  /** Exit multi-select mode */
  exitMultiSelect: () => void;
  /** Check if an item is selected */
  isSelected: (id: string) => boolean;
}

/**
 * useMultiSelect Hook
 *
 * Provides multi-selection state and actions for batch operations.
 * Integrates with LayoutProvider for global state management.
 *
 * @returns Multi-select state and actions
 *
 * @example
 * ```tsx
 * function LibraryList({ games }) {
 *   const {
 *     isActive,
 *     selectedCount,
 *     toggle,
 *     isSelected,
 *     enterMultiSelect,
 *   } = useMultiSelect<Game>();
 *
 *   return (
 *     <ul>
 *       {games.map(game => (
 *         <li
 *           key={game.id}
 *           onClick={() => toggle(game.id)}
 *           data-selected={isSelected(game.id)}
 *         >
 *           {game.name}
 *         </li>
 *       ))}
 *     </ul>
 *   );
 * }
 * ```
 */
export function useMultiSelect<T extends Selectable>(): UseMultiSelectReturn<T> {
  const {
    multiSelect,
    toggleMultiSelect,
    addToSelection,
    removeFromSelection,
    clearSelection: clearLayoutSelection,
    selectAll: selectAllLayout,
  } = useLayout();

  // Convert array to Set for efficient lookups
  const selectedIds = useMemo(() => {
    return new Set(multiSelect.selectedIds);
  }, [multiSelect.selectedIds]);

  // Selected count
  const selectedCount = selectedIds.size;

  // Check if item is selected
  const isSelected = useCallback(
    (id: string) => selectedIds.has(id),
    [selectedIds]
  );

  // Toggle selection of an item
  const toggle = useCallback(
    (id: string) => {
      if (selectedIds.has(id)) {
        removeFromSelection(id);
      } else {
        addToSelection(id);
      }

      // Auto-enter multi-select mode on first selection
      if (!multiSelect.isActive) {
        toggleMultiSelect(true);
      }
    },
    [selectedIds, addToSelection, removeFromSelection, multiSelect.isActive, toggleMultiSelect]
  );

  // Select all items
  const selectAll = useCallback(
    (items: T[]) => {
      const ids = items.map(item => item.id);
      selectAllLayout(ids);

      // Enter multi-select mode if not already
      if (!multiSelect.isActive) {
        toggleMultiSelect(true);
      }

      // Announce for screen readers
      announceToScreenReader(`Selezionati ${ids.length} elementi`);
    },
    [selectAllLayout, multiSelect.isActive, toggleMultiSelect]
  );

  // Clear all selections
  const clearSelection = useCallback(() => {
    clearLayoutSelection();

    // Announce for screen readers
    announceToScreenReader('Selezione annullata');
  }, [clearLayoutSelection]);

  // Enter multi-select mode
  const enterMultiSelect = useCallback(() => {
    toggleMultiSelect(true);

    // Announce for screen readers
    announceToScreenReader('Modalità selezione multipla attivata');
  }, [toggleMultiSelect]);

  // Exit multi-select mode
  const exitMultiSelect = useCallback(() => {
    toggleMultiSelect(false);

    // Announce for screen readers
    announceToScreenReader('Modalità selezione multipla disattivata');
  }, [toggleMultiSelect]);

  return {
    isActive: multiSelect.isActive,
    selectedIds,
    selectedCount,
    toggle,
    selectAll,
    clearSelection,
    enterMultiSelect,
    exitMultiSelect,
    isSelected,
  };
}

/**
 * Announce message to screen readers using a live region.
 */
function announceToScreenReader(message: string) {
  // Create or get existing announcer element
  let announcer = document.getElementById('multi-select-announcer');
  if (!announcer) {
    announcer = document.createElement('div');
    announcer.id = 'multi-select-announcer';
    announcer.setAttribute('role', 'status');
    announcer.setAttribute('aria-live', 'polite');
    announcer.setAttribute('aria-atomic', 'true');
    announcer.className = 'sr-only';
    document.body.appendChild(announcer);
  }

  // Clear and set message (needs to be different to trigger announcement)
  announcer.textContent = '';
  requestAnimationFrame(() => {
    if (announcer) {
      announcer.textContent = message;
    }
  });
}
