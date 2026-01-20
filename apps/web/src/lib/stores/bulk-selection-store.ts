/**
 * Bulk Selection Store (Issue #2613)
 *
 * Zustand store for managing bulk selection state in library.
 * Features:
 * - Selection mode toggle
 * - Individual game selection
 * - Select all / deselect all
 * - Shift+Click range selection support
 * - Selection count tracking
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

interface BulkSelectionStore {
  // State
  selectedIds: Set<string>;
  selectionMode: boolean;
  lastSelectedId: string | null;

  // Actions
  toggleSelectionMode: () => void;
  toggleSelection: (id: string) => void;
  selectAll: (ids: string[]) => void;
  deselectAll: () => void;
  clearSelection: () => void;
  selectRange: (ids: string[], targetId: string) => void;
  setLastSelectedId: (id: string | null) => void;

  // Utility getters
  isSelected: (id: string) => boolean;
  getSelectedCount: () => number;
  getSelectedIds: () => string[];
  hasSelection: () => boolean;
}

const initialState = {
  selectedIds: new Set<string>(),
  selectionMode: false,
  lastSelectedId: null,
};

export const useBulkSelectionStore = create<BulkSelectionStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      toggleSelectionMode: () => {
        const currentMode = get().selectionMode;
        set({
          selectionMode: !currentMode,
          // Clear selection when exiting selection mode
          selectedIds: currentMode ? new Set<string>() : get().selectedIds,
          lastSelectedId: currentMode ? null : get().lastSelectedId,
        });
      },

      toggleSelection: (id: string) => {
        const { selectedIds, selectionMode } = get();
        if (!selectionMode) return;

        const newSet = new Set(selectedIds);
        if (newSet.has(id)) {
          newSet.delete(id);
        } else {
          newSet.add(id);
        }
        set({ selectedIds: newSet, lastSelectedId: id });
      },

      selectAll: (ids: string[]) => {
        if (!get().selectionMode) return;
        set({ selectedIds: new Set(ids) });
      },

      deselectAll: () => {
        set({ selectedIds: new Set<string>() });
      },

      clearSelection: () => {
        set({
          selectedIds: new Set<string>(),
          selectionMode: false,
          lastSelectedId: null,
        });
      },

      /**
       * Select range of items between lastSelectedId and targetId
       * Used for Shift+Click range selection
       */
      selectRange: (ids: string[], targetId: string) => {
        const { selectedIds, lastSelectedId, selectionMode } = get();
        if (!selectionMode) return;

        // If no previous selection, just select the target
        if (!lastSelectedId) {
          const newSet = new Set(selectedIds);
          newSet.add(targetId);
          set({ selectedIds: newSet, lastSelectedId: targetId });
          return;
        }

        // Find indices
        const lastIndex = ids.indexOf(lastSelectedId);
        const targetIndex = ids.indexOf(targetId);

        if (lastIndex === -1 || targetIndex === -1) {
          // Fallback to simple selection
          const newSet = new Set(selectedIds);
          newSet.add(targetId);
          set({ selectedIds: newSet, lastSelectedId: targetId });
          return;
        }

        // Select range
        const start = Math.min(lastIndex, targetIndex);
        const end = Math.max(lastIndex, targetIndex);
        const newSet = new Set(selectedIds);

        for (let i = start; i <= end; i++) {
          // eslint-disable-next-line security/detect-object-injection -- i is a controlled loop index
          newSet.add(ids[i]);
        }

        set({ selectedIds: newSet, lastSelectedId: targetId });
      },

      setLastSelectedId: (id: string | null) => {
        set({ lastSelectedId: id });
      },

      // Utility getters
      isSelected: (id: string) => get().selectedIds.has(id),
      getSelectedCount: () => get().selectedIds.size,
      getSelectedIds: () => Array.from(get().selectedIds),
      hasSelection: () => get().selectedIds.size > 0,
    }),
    { name: 'BulkSelectionStore' }
  )
);
