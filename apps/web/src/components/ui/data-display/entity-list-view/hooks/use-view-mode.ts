/**
 * useViewMode - View mode state management with localStorage persistence
 *
 * Manages view mode selection (Grid/List/Carousel) with automatic persistence
 * to localStorage. Supports both controlled and uncontrolled patterns.
 *
 * @module components/ui/data-display/entity-list-view/hooks/use-view-mode
 *
 * Features:
 * - localStorage persistence with namespaced keys
 * - Validation against available modes
 * - Controlled/uncontrolled pattern support
 * - Fallback to first available mode on invalid value
 * - Cross-tab synchronization (via useLocalStorage)
 *
 * @example
 * ```tsx
 * // Uncontrolled mode (internal state + persistence)
 * const { mode, setMode } = useViewMode('games-browse', 'grid', ['grid', 'list', 'carousel']);
 *
 * // Controlled mode (external state)
 * const [externalMode, setExternalMode] = useState('grid');
 * const { mode, setMode } = useViewMode('games-browse', 'grid', ['grid', 'list'], externalMode);
 * ```
 */

import { useCallback, useEffect } from 'react';

import { useLocalStorage } from '@/hooks/useLocalStorage';
import { logger } from '@/lib/logger';

import type { ViewMode } from '../entity-list-view.types';

/**
 * Return type for useViewMode hook
 */
export interface UseViewModeReturn {
  /** Current active view mode */
  mode: ViewMode;
  /** Update view mode (validates against availableModes) */
  setMode: (mode: ViewMode) => void;
  /** Check if a specific mode is available */
  isAvailable: (mode: ViewMode) => boolean;
}

/**
 * Hook for managing view mode with localStorage persistence
 *
 * @param persistenceKey - Unique key for localStorage (will be prefixed with "view-mode:")
 * @param defaultMode - Initial mode if no persisted value exists
 * @param availableModes - Array of allowed view modes (default: all 3)
 * @param controlledMode - Optional controlled mode (overrides internal state)
 * @returns Object with current mode, setter, and availability checker
 */
export function useViewMode(
  persistenceKey: string,
  defaultMode: ViewMode = 'grid',
  availableModes: ViewMode[] = ['grid', 'list', 'carousel'],
  controlledMode?: ViewMode
): UseViewModeReturn {
  // Validate defaultMode is in availableModes
  const validDefaultMode = availableModes.includes(defaultMode)
    ? defaultMode
    : availableModes[0] || 'grid';

  // Internal state with localStorage persistence
  const [internalMode, setInternalMode] = useLocalStorage<ViewMode>(
    `view-mode:${persistenceKey}`,
    validDefaultMode
  );

  // Use controlled mode if provided, otherwise use internal state
  const currentMode = controlledMode ?? internalMode;

  /**
   * Validate and set new view mode
   * Only accepts modes present in availableModes array
   */
  const setMode = useCallback(
    (newMode: ViewMode) => {
      // Validate mode is available
      if (!availableModes.includes(newMode)) {
        logger.warn(
          `View mode "${newMode}" is not available. Available modes: ${availableModes.join(', ')}`
        );
        return;
      }

      // Update internal state (which persists to localStorage)
      setInternalMode(newMode);
    },
    [availableModes, setInternalMode]
  );

  /**
   * Check if a specific mode is available
   */
  const isAvailable = useCallback(
    (mode: ViewMode) => availableModes.includes(mode),
    [availableModes]
  );

  /**
   * Validate current mode on mount and availableModes change
   * Fallback to first available mode if current mode becomes unavailable
   */
  useEffect(() => {
    if (!availableModes.includes(currentMode)) {
      const fallbackMode = availableModes[0] || 'grid';
      logger.warn(
        `Current mode "${currentMode}" is not available. Falling back to "${fallbackMode}"`
      );
      setInternalMode(fallbackMode);
    }
  }, [availableModes, currentMode, setInternalMode]);

  return {
    mode: currentMode,
    setMode,
    isAvailable,
  };
}
