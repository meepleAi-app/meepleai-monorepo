/**
 * LayoutProvider - Global Layout State Management
 * Issue #3287 - Phase 1: Core Layout Structure
 *
 * Provides centralized layout state management including:
 * - Current page context (library, game_detail, session, chat, etc.)
 * - Responsive state (mobile/tablet/desktop detection)
 * - FAB visibility and configuration
 * - ActionBar configuration
 * - Multi-select mode state
 * - Hamburger menu state
 *
 * @example
 * ```tsx
 * // In app/providers.tsx
 * <LayoutProvider>
 *   <App />
 * </LayoutProvider>
 *
 * // In any component
 * const { context, setContext, fab, actionBar } = useLayout();
 * ```
 */

'use client';

import {
  createContext,
  useContext,
  useCallback,
  useMemo,
  useReducer,
  type ReactNode,
} from 'react';

import { useResponsive } from '@/hooks/useResponsive';
import type {
  LayoutState,
  LayoutContextValue,
  LayoutContext,
  FABConfig,
  ActionBarConfig,
  Action,
  BreadcrumbItem,
} from '@/types/layout';

/**
 * Action types for layout reducer
 */
type LayoutAction =
  | { type: 'SET_CONTEXT'; payload: LayoutContext }
  | { type: 'SET_FAB_CONFIG'; payload: Partial<FABConfig> }
  | { type: 'SET_ACTION_BAR_CONFIG'; payload: Partial<ActionBarConfig> }
  | { type: 'TOGGLE_MULTI_SELECT'; payload?: boolean }
  | { type: 'ADD_TO_SELECTION'; payload: string }
  | { type: 'REMOVE_FROM_SELECTION'; payload: string }
  | { type: 'CLEAR_SELECTION' }
  | { type: 'SELECT_ALL'; payload: string[] }
  | { type: 'SET_BREADCRUMBS'; payload: BreadcrumbItem[] }
  | { type: 'TOGGLE_MENU'; payload?: boolean }
  | { type: 'SET_KEYBOARD_VISIBLE'; payload: boolean }
  | { type: 'SET_MODAL_OPEN'; payload: boolean }
  | { type: 'SET_SCROLL_DIRECTION'; payload: 'up' | 'down' | 'none' }
  | { type: 'REGISTER_ACTIONS'; payload: Action[] }
  | { type: 'CLEAR_ACTIONS' };

/**
 * Initial layout state
 */
const initialState: Omit<LayoutState, 'responsive'> = {
  context: 'default',
  fab: {
    visible: true,
    bottomOffset: 80,
    rightOffset: 16,
  },
  actionBar: {
    actions: [],
    visible: true,
  },
  multiSelect: {
    isActive: false,
    selectedIds: [],
    totalCount: 0,
  },
  breadcrumbs: [],
  isMenuOpen: false,
  isKeyboardVisible: false,
  isModalOpen: false,
  scrollDirection: 'none',
};

/**
 * Layout reducer for state management
 */
function layoutReducer(
  state: Omit<LayoutState, 'responsive'>,
  action: LayoutAction
): Omit<LayoutState, 'responsive'> {
  switch (action.type) {
    case 'SET_CONTEXT':
      return {
        ...state,
        context: action.payload,
        // Reset multi-select when context changes
        multiSelect: {
          isActive: false,
          selectedIds: [],
          totalCount: 0,
        },
      };

    case 'SET_FAB_CONFIG':
      return {
        ...state,
        fab: { ...state.fab, ...action.payload },
      };

    case 'SET_ACTION_BAR_CONFIG':
      return {
        ...state,
        actionBar: { ...state.actionBar, ...action.payload },
      };

    case 'TOGGLE_MULTI_SELECT':
      return {
        ...state,
        multiSelect: {
          ...state.multiSelect,
          isActive: action.payload ?? !state.multiSelect.isActive,
          selectedIds: action.payload === false ? [] : state.multiSelect.selectedIds,
        },
      };

    case 'ADD_TO_SELECTION':
      return {
        ...state,
        multiSelect: {
          ...state.multiSelect,
          selectedIds: state.multiSelect.selectedIds.includes(action.payload)
            ? state.multiSelect.selectedIds
            : [...state.multiSelect.selectedIds, action.payload],
        },
      };

    case 'REMOVE_FROM_SELECTION':
      return {
        ...state,
        multiSelect: {
          ...state.multiSelect,
          selectedIds: state.multiSelect.selectedIds.filter(id => id !== action.payload),
        },
      };

    case 'CLEAR_SELECTION':
      return {
        ...state,
        multiSelect: {
          ...state.multiSelect,
          selectedIds: [],
          isActive: false,
        },
      };

    case 'SELECT_ALL':
      return {
        ...state,
        multiSelect: {
          ...state.multiSelect,
          selectedIds: action.payload,
          totalCount: action.payload.length,
        },
      };

    case 'SET_BREADCRUMBS':
      return {
        ...state,
        breadcrumbs: action.payload,
      };

    case 'TOGGLE_MENU':
      return {
        ...state,
        isMenuOpen: action.payload ?? !state.isMenuOpen,
      };

    case 'SET_KEYBOARD_VISIBLE':
      return {
        ...state,
        isKeyboardVisible: action.payload,
      };

    case 'SET_MODAL_OPEN':
      return {
        ...state,
        isModalOpen: action.payload,
      };

    case 'SET_SCROLL_DIRECTION':
      return {
        ...state,
        scrollDirection: action.payload,
      };

    case 'REGISTER_ACTIONS':
      return {
        ...state,
        actionBar: {
          ...state.actionBar,
          actions: action.payload,
        },
      };

    case 'CLEAR_ACTIONS':
      return {
        ...state,
        actionBar: {
          ...state.actionBar,
          actions: [],
        },
      };

    default:
      return state;
  }
}

/**
 * Extended context value type with dispatch
 */
type LayoutContextValueWithDispatch = LayoutContextValue & {
  dispatch: React.Dispatch<LayoutAction>;
};

/**
 * Layout React context (note: LayoutContext type is imported from @/types/layout)
 */
const LayoutReactContext = createContext<LayoutContextValueWithDispatch | null>(null);

/**
 * Provider props
 */
interface LayoutProviderProps {
  children: ReactNode;
  /** Optional initial context */
  initialContext?: LayoutContext;
}

/**
 * LayoutProvider Component
 *
 * Wraps the application to provide layout state management.
 * Must be placed within the component tree where layout state is needed.
 */
export function LayoutProvider({
  children,
  initialContext = 'default',
}: LayoutProviderProps) {
  // Get responsive state from hook
  const responsive = useResponsive();

  // Use reducer for complex state management
  const [state, dispatch] = useReducer(layoutReducer, {
    ...initialState,
    context: initialContext,
  });

  // Action creators
  const setContext = useCallback((context: LayoutContext) => {
    dispatch({ type: 'SET_CONTEXT', payload: context });
  }, []);

  const setFABConfig = useCallback((config: Partial<FABConfig>) => {
    dispatch({ type: 'SET_FAB_CONFIG', payload: config });
  }, []);

  const setActionBarConfig = useCallback((config: Partial<ActionBarConfig>) => {
    dispatch({ type: 'SET_ACTION_BAR_CONFIG', payload: config });
  }, []);

  const toggleMultiSelect = useCallback((enabled?: boolean) => {
    dispatch({ type: 'TOGGLE_MULTI_SELECT', payload: enabled });
  }, []);

  const addToSelection = useCallback((id: string) => {
    dispatch({ type: 'ADD_TO_SELECTION', payload: id });
  }, []);

  const removeFromSelection = useCallback((id: string) => {
    dispatch({ type: 'REMOVE_FROM_SELECTION', payload: id });
  }, []);

  const clearSelection = useCallback(() => {
    dispatch({ type: 'CLEAR_SELECTION' });
  }, []);

  const selectAll = useCallback((ids: string[]) => {
    dispatch({ type: 'SELECT_ALL', payload: ids });
  }, []);

  const setBreadcrumbs = useCallback((items: BreadcrumbItem[]) => {
    dispatch({ type: 'SET_BREADCRUMBS', payload: items });
  }, []);

  const toggleMenu = useCallback((open?: boolean) => {
    dispatch({ type: 'TOGGLE_MENU', payload: open });
  }, []);

  const setKeyboardVisible = useCallback((visible: boolean) => {
    dispatch({ type: 'SET_KEYBOARD_VISIBLE', payload: visible });
  }, []);

  const setModalOpen = useCallback((open: boolean) => {
    dispatch({ type: 'SET_MODAL_OPEN', payload: open });
  }, []);

  const setScrollDirection = useCallback((direction: 'up' | 'down' | 'none') => {
    dispatch({ type: 'SET_SCROLL_DIRECTION', payload: direction });
  }, []);

  const registerActions = useCallback((actions: Action[]) => {
    dispatch({ type: 'REGISTER_ACTIONS', payload: actions });
  }, []);

  const clearActions = useCallback(() => {
    dispatch({ type: 'CLEAR_ACTIONS' });
  }, []);

  // Memoize context value to prevent unnecessary re-renders
  const contextValue = useMemo<LayoutContextValue & { dispatch: React.Dispatch<LayoutAction> }>(
    () => ({
      ...state,
      responsive,
      dispatch,
      setContext,
      setFABConfig,
      setActionBarConfig,
      toggleMultiSelect,
      addToSelection,
      removeFromSelection,
      clearSelection,
      selectAll,
      setBreadcrumbs,
      toggleMenu,
      setKeyboardVisible,
      setModalOpen,
      setScrollDirection,
      registerActions,
      clearActions,
    }),
    [
      state,
      responsive,
      dispatch,
      setContext,
      setFABConfig,
      setActionBarConfig,
      toggleMultiSelect,
      addToSelection,
      removeFromSelection,
      clearSelection,
      selectAll,
      setBreadcrumbs,
      toggleMenu,
      setKeyboardVisible,
      setModalOpen,
      setScrollDirection,
      registerActions,
      clearActions,
    ]
  );

  return (
    <LayoutReactContext.Provider value={contextValue}>
      {children}
    </LayoutReactContext.Provider>
  );
}

/**
 * Hook to access layout context
 *
 * @throws Error if used outside LayoutProvider
 * @returns LayoutContextValue with state and actions
 *
 * @example
 * ```tsx
 * const { context, isMobile, setContext } = useLayout();
 * ```
 */
export function useLayout(): LayoutContextValueWithDispatch {
  const context = useContext(LayoutReactContext);

  if (!context) {
    throw new Error('useLayout must be used within a LayoutProvider');
  }

  return context;
}

/**
 * Hook to access only responsive state (lighter weight)
 *
 * @returns ResponsiveState
 */
export function useLayoutResponsive() {
  const { responsive } = useLayout();
  return responsive;
}

/**
 * Hook to access only FAB state
 *
 * @returns FAB state and setter
 */
export function useLayoutFAB() {
  const { fab, setFABConfig } = useLayout();
  return { fab, setFABConfig };
}

/**
 * Hook to access only ActionBar state
 *
 * @returns ActionBar state and setter
 */
export function useLayoutActionBar() {
  const { actionBar, setActionBarConfig, registerActions, clearActions } = useLayout();
  return { actionBar, setActionBarConfig, registerActions, clearActions };
}

/**
 * Hook to access only multi-select state
 *
 * @returns Multi-select state and actions
 */
export function useLayoutMultiSelect() {
  const {
    multiSelect,
    toggleMultiSelect,
    addToSelection,
    removeFromSelection,
    clearSelection,
    selectAll,
  } = useLayout();

  return {
    multiSelect,
    toggleMultiSelect,
    addToSelection,
    removeFromSelection,
    clearSelection,
    selectAll,
  };
}

/**
 * Hook to set layout context on mount
 *
 * @param context - Layout context to set
 *
 * @example
 * ```tsx
 * // In a page component
 * useLayoutContext('library');
 * ```
 */
export function useLayoutContext(context: LayoutContext) {
  const { setContext } = useLayout();

  // Set context on mount
  useMemo(() => {
    setContext(context);
  }, [context, setContext]);
}
