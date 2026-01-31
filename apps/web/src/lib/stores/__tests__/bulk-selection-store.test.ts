/**
 * BulkSelectionStore Tests (Issue #2613)
 *
 * Test Coverage:
 * - Selection mode toggle
 * - Individual item selection/deselection
 * - Select all / deselect all
 * - Range selection (Shift+Click)
 * - Utility getters
 *
 * Target: 100% coverage
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { act } from '@testing-library/react';
import { useBulkSelectionStore } from '../bulk-selection-store';

// ============================================================================
// Helper
// ============================================================================

const resetStore = () => {
  act(() => {
    useBulkSelectionStore.setState({
      selectedIds: new Set<string>(),
      selectionMode: false,
      lastSelectedId: null,
    });
  });
};

// ============================================================================
// Initial State Tests
// ============================================================================

describe('BulkSelectionStore - Initial State', () => {
  beforeEach(resetStore);

  it('starts with empty selection', () => {
    const { selectedIds, selectionMode, lastSelectedId } = useBulkSelectionStore.getState();
    expect(selectedIds.size).toBe(0);
    expect(selectionMode).toBe(false);
    expect(lastSelectedId).toBe(null);
  });

  it('hasSelection returns false initially', () => {
    const { hasSelection } = useBulkSelectionStore.getState();
    expect(hasSelection()).toBe(false);
  });

  it('getSelectedCount returns 0 initially', () => {
    const { getSelectedCount } = useBulkSelectionStore.getState();
    expect(getSelectedCount()).toBe(0);
  });
});

// ============================================================================
// Selection Mode Toggle Tests
// ============================================================================

describe('BulkSelectionStore - Selection Mode', () => {
  beforeEach(resetStore);

  it('toggles selection mode on', () => {
    const { toggleSelectionMode } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelectionMode();
    });

    expect(useBulkSelectionStore.getState().selectionMode).toBe(true);
  });

  it('toggles selection mode off and clears selection', () => {
    // Set up: enable selection mode and add items
    act(() => {
      useBulkSelectionStore.setState({
        selectionMode: true,
        selectedIds: new Set(['game-1', 'game-2']),
        lastSelectedId: 'game-2',
      });
    });

    const { toggleSelectionMode } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelectionMode();
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectionMode).toBe(false);
    expect(state.selectedIds.size).toBe(0);
    expect(state.lastSelectedId).toBe(null);
  });
});

// ============================================================================
// Individual Selection Tests
// ============================================================================

describe('BulkSelectionStore - Individual Selection', () => {
  beforeEach(() => {
    resetStore();
    // Enable selection mode
    act(() => {
      useBulkSelectionStore.setState({ selectionMode: true });
    });
  });

  it('adds item to selection', () => {
    const { toggleSelection } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelection('game-1');
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.has('game-1')).toBe(true);
    expect(state.lastSelectedId).toBe('game-1');
  });

  it('removes item from selection', () => {
    // Set up: add item first
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1']),
        selectionMode: true,
      });
    });

    const { toggleSelection } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelection('game-1');
    });

    expect(useBulkSelectionStore.getState().selectedIds.has('game-1')).toBe(false);
  });

  it('does not toggle when selection mode is off', () => {
    act(() => {
      useBulkSelectionStore.setState({ selectionMode: false });
    });

    const { toggleSelection } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelection('game-1');
    });

    expect(useBulkSelectionStore.getState().selectedIds.size).toBe(0);
  });

  it('updates lastSelectedId on toggle', () => {
    const { toggleSelection } = useBulkSelectionStore.getState();

    act(() => {
      toggleSelection('game-1');
      toggleSelection('game-2');
    });

    expect(useBulkSelectionStore.getState().lastSelectedId).toBe('game-2');
  });
});

// ============================================================================
// Select All / Deselect All Tests
// ============================================================================

describe('BulkSelectionStore - Bulk Operations', () => {
  beforeEach(() => {
    resetStore();
    act(() => {
      useBulkSelectionStore.setState({ selectionMode: true });
    });
  });

  it('selects all provided IDs', () => {
    const { selectAll } = useBulkSelectionStore.getState();
    const ids = ['game-1', 'game-2', 'game-3'];

    act(() => {
      selectAll(ids);
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.size).toBe(3);
    ids.forEach(id => {
      expect(state.selectedIds.has(id)).toBe(true);
    });
  });

  it('does not select all when selection mode is off', () => {
    act(() => {
      useBulkSelectionStore.setState({ selectionMode: false });
    });

    const { selectAll } = useBulkSelectionStore.getState();

    act(() => {
      selectAll(['game-1', 'game-2']);
    });

    expect(useBulkSelectionStore.getState().selectedIds.size).toBe(0);
  });

  it('deselects all items', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1', 'game-2']),
        selectionMode: true,
      });
    });

    const { deselectAll } = useBulkSelectionStore.getState();

    act(() => {
      deselectAll();
    });

    expect(useBulkSelectionStore.getState().selectedIds.size).toBe(0);
  });

  it('clears selection and exits selection mode', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1', 'game-2']),
        selectionMode: true,
        lastSelectedId: 'game-2',
      });
    });

    const { clearSelection } = useBulkSelectionStore.getState();

    act(() => {
      clearSelection();
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.size).toBe(0);
    expect(state.selectionMode).toBe(false);
    expect(state.lastSelectedId).toBe(null);
  });
});

// ============================================================================
// Range Selection Tests
// ============================================================================

describe('BulkSelectionStore - Range Selection', () => {
  const allIds = ['game-1', 'game-2', 'game-3', 'game-4', 'game-5'];

  beforeEach(() => {
    resetStore();
    act(() => {
      useBulkSelectionStore.setState({
        selectionMode: true,
        lastSelectedId: 'game-2',
      });
    });
  });

  it('selects range from last selected to target', () => {
    const { selectRange } = useBulkSelectionStore.getState();

    act(() => {
      selectRange(allIds, 'game-4');
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.has('game-2')).toBe(true);
    expect(state.selectedIds.has('game-3')).toBe(true);
    expect(state.selectedIds.has('game-4')).toBe(true);
    expect(state.selectedIds.has('game-1')).toBe(false);
    expect(state.selectedIds.has('game-5')).toBe(false);
  });

  it('selects range backwards', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectionMode: true,
        lastSelectedId: 'game-4',
      });
    });

    const { selectRange } = useBulkSelectionStore.getState();

    act(() => {
      selectRange(allIds, 'game-2');
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.has('game-2')).toBe(true);
    expect(state.selectedIds.has('game-3')).toBe(true);
    expect(state.selectedIds.has('game-4')).toBe(true);
  });

  it('selects just target when no previous selection', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectionMode: true,
        lastSelectedId: null,
      });
    });

    const { selectRange } = useBulkSelectionStore.getState();

    act(() => {
      selectRange(allIds, 'game-3');
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.size).toBe(1);
    expect(state.selectedIds.has('game-3')).toBe(true);
    expect(state.lastSelectedId).toBe('game-3');
  });

  it('handles invalid lastSelectedId gracefully', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectionMode: true,
        lastSelectedId: 'invalid-id',
      });
    });

    const { selectRange } = useBulkSelectionStore.getState();

    act(() => {
      selectRange(allIds, 'game-3');
    });

    const state = useBulkSelectionStore.getState();
    expect(state.selectedIds.has('game-3')).toBe(true);
    expect(state.lastSelectedId).toBe('game-3');
  });

  it('does not select range when selection mode is off', () => {
    act(() => {
      useBulkSelectionStore.setState({ selectionMode: false });
    });

    const { selectRange } = useBulkSelectionStore.getState();

    act(() => {
      selectRange(allIds, 'game-4');
    });

    expect(useBulkSelectionStore.getState().selectedIds.size).toBe(0);
  });
});

// ============================================================================
// Utility Getter Tests
// ============================================================================

describe('BulkSelectionStore - Utility Getters', () => {
  beforeEach(resetStore);

  it('isSelected returns true for selected items', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1', 'game-2']),
        selectionMode: true,
      });
    });

    const { isSelected } = useBulkSelectionStore.getState();
    expect(isSelected('game-1')).toBe(true);
    expect(isSelected('game-2')).toBe(true);
    expect(isSelected('game-3')).toBe(false);
  });

  it('getSelectedCount returns correct count', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1', 'game-2', 'game-3']),
        selectionMode: true,
      });
    });

    const { getSelectedCount } = useBulkSelectionStore.getState();
    expect(getSelectedCount()).toBe(3);
  });

  it('getSelectedIds returns array of selected IDs', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1', 'game-2']),
        selectionMode: true,
      });
    });

    const { getSelectedIds } = useBulkSelectionStore.getState();
    const ids = getSelectedIds();
    expect(ids).toHaveLength(2);
    expect(ids).toContain('game-1');
    expect(ids).toContain('game-2');
  });

  it('hasSelection returns true when items are selected', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(['game-1']),
        selectionMode: true,
      });
    });

    const { hasSelection } = useBulkSelectionStore.getState();
    expect(hasSelection()).toBe(true);
  });

  it('hasSelection returns false when no items are selected', () => {
    act(() => {
      useBulkSelectionStore.setState({
        selectedIds: new Set(),
        selectionMode: true,
      });
    });

    const { hasSelection } = useBulkSelectionStore.getState();
    expect(hasSelection()).toBe(false);
  });
});
