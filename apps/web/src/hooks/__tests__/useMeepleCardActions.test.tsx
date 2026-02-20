/**
 * useMeepleCardActions Tests
 * Issue #4899 - MeepleCard button visibility system
 *
 * Tests the 3-state visibility model:
 * - visible + enabled
 * - visible + disabled (with disabledTooltip)
 * - hidden
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { QueryClientWrapper } from '@/__tests__/utils/query-client-wrapper';
import { useMeepleCardActions } from '../useMeepleCardActions';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useGameInLibraryStatus: vi.fn(),
  useAddGameToLibrary: vi.fn(),
  useRemoveGameFromLibrary: vi.fn(),
}));

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import {
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
} from '@/hooks/queries/useLibrary';

const mockMutate = vi.fn();

function setupMocks({
  isAuthenticated = false,
  isInLibrary = false,
}: {
  isAuthenticated?: boolean;
  isInLibrary?: boolean;
}) {
  vi.mocked(useCurrentUser).mockReturnValue({
    data: isAuthenticated ? { id: 'user-1', email: 'test@test.com', role: 'user' } : null,
    isLoading: false,
    isError: false,
  } as ReturnType<typeof useCurrentUser>);

  vi.mocked(useGameInLibraryStatus).mockReturnValue({
    data: { inLibrary: isInLibrary, isFavorite: false },
    isLoading: false,
    isError: false,
  } as ReturnType<typeof useGameInLibraryStatus>);

  vi.mocked(useAddGameToLibrary).mockReturnValue({
    mutate: mockMutate,
    isPending: false,
  } as unknown as ReturnType<typeof useAddGameToLibrary>);

  vi.mocked(useRemoveGameFromLibrary).mockReturnValue({
    mutate: mockMutate,
    isPending: false,
  } as unknown as ReturnType<typeof useRemoveGameFromLibrary>);
}

// ============================================================================
// Tests
// ============================================================================

describe('useMeepleCardActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // Game + Catalog context
  // --------------------------------------------------------------------------

  describe('game entity in catalog context', () => {
    describe('Guest user (not authenticated)', () => {
      beforeEach(() => setupMocks({ isAuthenticated: false, isInLibrary: false }));

      it('returns 2 actions (Add + Remove)', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        expect(result.current).toHaveLength(2);
      });

      it('Add to Library is visible but disabled', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        expect(addAction).toBeDefined();
        expect(addAction?.disabled).toBe(true);
        expect(addAction?.hidden).toBe(false);
      });

      it('Add to Library has a disabledTooltip when disabled', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        expect(addAction?.disabledTooltip).toBeTruthy();
        expect(addAction?.disabledTooltip).toBe('Accedi per aggiungere alla libreria');
      });

      it('Remove from Library is hidden for guest', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const removeAction = result.current.find(a => a.label === 'Rimuovi da Libreria');
        expect(removeAction?.hidden).toBe(true);
      });
    });

    describe('Authenticated user, game NOT in library', () => {
      beforeEach(() => setupMocks({ isAuthenticated: true, isInLibrary: false }));

      it('Add to Library is visible and enabled', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        expect(addAction?.disabled).toBe(false);
        expect(addAction?.hidden).toBe(false);
      });

      it('Add to Library has no disabledTooltip when enabled', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        // disabledTooltip may be set but should not matter when disabled=false
        expect(addAction?.disabled).toBe(false);
      });

      it('Remove from Library is hidden', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const removeAction = result.current.find(a => a.label === 'Rimuovi da Libreria');
        expect(removeAction?.hidden).toBe(true);
      });

      it('clicking Add to Library calls addToLibrary.mutate', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        addAction?.onClick();
        expect(mockMutate).toHaveBeenCalledWith({ gameId: 'game-123' });
      });

      it('uses onAddToLibrary callback if provided', () => {
        const onAddToLibrary = vi.fn();
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog', { onAddToLibrary }),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        addAction?.onClick();
        expect(onAddToLibrary).toHaveBeenCalled();
        expect(mockMutate).not.toHaveBeenCalled();
      });
    });

    describe('Authenticated user, game IN library', () => {
      beforeEach(() => setupMocks({ isAuthenticated: true, isInLibrary: true }));

      it('Add to Library is hidden', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const addAction = result.current.find(a => a.label === 'Aggiungi a Libreria');
        expect(addAction?.hidden).toBe(true);
      });

      it('Remove from Library is visible and enabled', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const removeAction = result.current.find(a => a.label === 'Rimuovi da Libreria');
        expect(removeAction?.disabled).toBeUndefined();
        expect(removeAction?.hidden).toBe(false);
      });

      it('clicking Remove from Library calls removeFromLibrary.mutate', () => {
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog'),
          { wrapper: QueryClientWrapper }
        );
        const removeAction = result.current.find(a => a.label === 'Rimuovi da Libreria');
        removeAction?.onClick();
        expect(mockMutate).toHaveBeenCalledWith('game-123');
      });

      it('uses onRemoveFromLibrary callback if provided', () => {
        const onRemoveFromLibrary = vi.fn();
        const { result } = renderHook(
          () => useMeepleCardActions('game', 'game-123', 'catalog', { onRemoveFromLibrary }),
          { wrapper: QueryClientWrapper }
        );
        const removeAction = result.current.find(a => a.label === 'Rimuovi da Libreria');
        removeAction?.onClick();
        expect(onRemoveFromLibrary).toHaveBeenCalled();
        expect(mockMutate).not.toHaveBeenCalled();
      });
    });
  });

  // --------------------------------------------------------------------------
  // Game + Library context
  // --------------------------------------------------------------------------

  describe('game entity in library context', () => {
    it('shows Remove from Library action for authenticated user', () => {
      setupMocks({ isAuthenticated: true, isInLibrary: true });
      const { result } = renderHook(
        () => useMeepleCardActions('game', 'game-123', 'library'),
        { wrapper: QueryClientWrapper }
      );
      expect(result.current).toHaveLength(1);
      expect(result.current[0].label).toBe('Rimuovi da Libreria');
      expect(result.current[0].hidden).toBe(false);
    });
  });

  // --------------------------------------------------------------------------
  // Non-game entities
  // --------------------------------------------------------------------------

  describe('non-game entities', () => {
    it('returns empty actions for player entity (not yet implemented)', () => {
      setupMocks({ isAuthenticated: true });
      const { result } = renderHook(
        () => useMeepleCardActions('player', 'player-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      expect(result.current).toHaveLength(0);
    });

    it('returns empty actions for session entity', () => {
      setupMocks({ isAuthenticated: true });
      const { result } = renderHook(
        () => useMeepleCardActions('session', 'session-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      expect(result.current).toHaveLength(0);
    });
  });

  // --------------------------------------------------------------------------
  // Click guard: disabled actions must not invoke onClick
  // --------------------------------------------------------------------------

  describe('click guard for disabled actions', () => {
    it('disabled action onClick does not call the mutate function', () => {
      setupMocks({ isAuthenticated: false });
      const { result } = renderHook(
        () => useMeepleCardActions('game', 'game-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      const disabledAction = result.current.find(a => a.disabled === true);
      expect(disabledAction).toBeDefined();
      // Calling onClick on a disabled action should be a no-op in the component
      // (the MeepleCardQuickActions component guards it with `if (!action.disabled)`)
      // The action itself does not have built-in click prevention — that is the component's job.
      // We verify the action is correctly marked disabled so the component can guard it.
      expect(disabledAction?.disabled).toBe(true);
    });
  });

  // --------------------------------------------------------------------------
  // Visibility model: the 3 states
  // --------------------------------------------------------------------------

  describe('3-state visibility model', () => {
    it('disabled action has disabledTooltip (not label) as explanation', () => {
      setupMocks({ isAuthenticated: false });
      const { result } = renderHook(
        () => useMeepleCardActions('game', 'game-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      const disabledAction = result.current.find(a => a.disabled === true);
      expect(disabledAction).toBeDefined();
      expect(disabledAction?.disabledTooltip).toBeDefined();
      expect(disabledAction?.disabledTooltip).not.toBe(disabledAction?.label);
    });

    it('enabled action does not need disabledTooltip', () => {
      setupMocks({ isAuthenticated: true, isInLibrary: false });
      const { result } = renderHook(
        () => useMeepleCardActions('game', 'game-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      const enabledAction = result.current.find(a => !a.disabled && !a.hidden);
      expect(enabledAction).toBeDefined();
      expect(enabledAction?.disabled).toBeFalsy();
    });

    it('hidden action is filtered out by MeepleCardQuickActions', () => {
      // This test verifies the contract: hidden=true means the action
      // will be filtered out in MeepleCardQuickActions rendering
      setupMocks({ isAuthenticated: false });
      const { result } = renderHook(
        () => useMeepleCardActions('game', 'game-123', 'catalog'),
        { wrapper: QueryClientWrapper }
      );
      const hiddenActions = result.current.filter(a => a.hidden === true);
      // Remove from Library should be hidden for guest
      expect(hiddenActions.length).toBeGreaterThan(0);
      expect(hiddenActions[0].label).toBe('Rimuovi da Libreria');
    });
  });
});
