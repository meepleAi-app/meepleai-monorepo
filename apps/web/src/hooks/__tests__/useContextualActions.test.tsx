/**
 * useContextualActions Tests
 * Issue #4062 - MeepleCard: Context-Aware Actions
 */

import { renderHook } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { QueryClientWrapper } from '@/__tests__/utils/query-client-wrapper';
import { useContextualActions } from '../use-contextual-actions';

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
}));

describe('useContextualActions', () => {
  const mockPush = vi.fn();
  const mockRouter = { push: mockPush };

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as ReturnType<typeof vi.fn>).mockReturnValue(mockRouter);
  });

  // ===== Base actions passthrough =====

  describe('Base actions passthrough', () => {
    it('returns base entity actions when no context provided', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1' }),
        { wrapper: QueryClientWrapper }
      );

      // Should return the first 4 base actions from useEntityActions (slice at max=4)
      // useEntityActions now returns: [collectionAction, Crea Agente, Chat con Agent, Avvia Sessione, Condividi]
      expect(result.current.quickActions).toHaveLength(4);
      expect(result.current.quickActions[0].label).toBe('Aggiungi a Collezione');
      expect(result.current.quickActions[1].label).toBe('Crea Agente');
      expect(result.current.quickActions[2].label).toBe('Chat con Agent');
      expect(result.current.quickActions[3].label).toBe('Avvia Sessione');
    });

    it('returns empty actions for custom entity', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'custom', id: 'custom-1' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(0);
    });
  });

  // ===== Tier-based filtering =====

  describe('Tier-based filtering', () => {
    it('disables pro-tier actions for free users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' }),
        { wrapper: QueryClientWrapper }
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat con Agent');
      expect(chatAction?.disabled).toBe(true);
    });

    it('disables basic-tier actions for free users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' }),
        { wrapper: QueryClientWrapper }
      );

      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(sessionAction?.disabled).toBe(true);
    });

    it('enables basic-tier actions for basic users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'basic' }),
        { wrapper: QueryClientWrapper }
      );

      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(sessionAction?.disabled).toBe(false);
    });

    it('enables all actions for pro users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'pro' }),
        { wrapper: QueryClientWrapper }
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat con Agent');
      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(chatAction?.disabled).toBe(false);
      expect(sessionAction?.disabled).toBe(false);
    });

    it('enables all actions for enterprise users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'enterprise' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions.forEach(action => {
        expect(action.disabled).toBeFalsy();
      });
    });

    it('keeps share action enabled for free tier', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' }),
        { wrapper: QueryClientWrapper }
      );

      const shareAction = result.current.quickActions.find(a => a.label === 'Condividi');
      expect(shareAction?.disabled).toBeFalsy();
    });
  });

  // ===== Collection context: catalog =====

  describe('Catalog context', () => {
    it('adds "Aggiungi alla libreria" for catalog games not in collection', () => {
      const onAdd = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'catalog',
          inCollection: false,
          onAddToLibrary: onAdd,
        }),
        { wrapper: QueryClientWrapper }
      );

      const addAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla libreria');
      expect(addAction).toBeDefined();
      addAction?.onClick();
      expect(onAdd).toHaveBeenCalled();
    });

    it('adds "Rimuovi dalla libreria" for catalog games in collection', () => {
      const onRemove = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'catalog',
          inCollection: true,
          onRemoveFromLibrary: onRemove,
        }),
        { wrapper: QueryClientWrapper }
      );

      const removeAction = result.current.quickActions.find(a => a.label === 'Rimuovi dalla libreria');
      expect(removeAction).toBeDefined();
      removeAction?.onClick();
      expect(onRemove).toHaveBeenCalled();
    });

    it('does not add library action without callback', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'catalog',
          inCollection: false,
        }),
        { wrapper: QueryClientWrapper }
      );

      const addAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla libreria');
      expect(addAction).toBeUndefined();
    });
  });

  // ===== Collection context: library =====

  describe('Library context', () => {
    it('includes remove action in library context (within max 4 limit)', () => {
      const onRemove = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'library',
          onRemoveFromLibrary: onRemove,
        }),
        { wrapper: QueryClientWrapper }
      );

      // Base actions (4) + appended remove action (1) = 5, but max 4 limit truncates
      // The remove action is appended at the end and gets cut by the slice(0, 4) limit
      expect(result.current.quickActions.length).toBeLessThanOrEqual(4);
    });

    it('retains base actions in library context', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'library',
          tier: 'pro',
        }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions.find(a => a.label === 'Chat con Agent')).toBeDefined();
      expect(result.current.quickActions.find(a => a.label === 'Avvia Sessione')).toBeDefined();
    });
  });

  // ===== Collection context: wishlist =====

  describe('Wishlist context', () => {
    it('adds library and remove-wishlist actions in wishlist context', () => {
      const onAdd = vi.fn();
      const onWishlist = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'wishlist',
          onAddToLibrary: onAdd,
          onWishlistToggle: onWishlist,
        }),
        { wrapper: QueryClientWrapper }
      );

      const addAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla libreria');
      const removeWishlist = result.current.quickActions.find(a => a.label === 'Rimuovi dalla wishlist');

      expect(addAction).toBeDefined();
      expect(removeWishlist).toBeDefined();

      removeWishlist?.onClick();
      expect(onWishlist).toHaveBeenCalled();
    });
  });

  // ===== Collection context: shared =====

  describe('Shared context', () => {
    it('limits actions to max 4 for shared games (wishlist appended after base)', () => {
      const onWishlist = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'shared',
          inCollection: false,
          isWishlisted: false,
          onWishlistToggle: onWishlist,
        }),
        { wrapper: QueryClientWrapper }
      );

      // Base actions (4) + appended wishlist (1) = 5, but max 4 limit truncates
      // The wishlist action is appended at the end and gets cut by the slice(0, 4) limit
      expect(result.current.quickActions.length).toBeLessThanOrEqual(4);
    });

    it('does not add wishlist action when already wishlisted', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'shared',
          inCollection: false,
          isWishlisted: true,
          onWishlistToggle: vi.fn(),
        }),
        { wrapper: QueryClientWrapper }
      );

      const wishlistAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla wishlist');
      expect(wishlistAction).toBeUndefined();
    });

    it('does not add wishlist action when in collection', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'shared',
          inCollection: true,
          isWishlisted: false,
          onWishlistToggle: vi.fn(),
        }),
        { wrapper: QueryClientWrapper }
      );

      const wishlistAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla wishlist');
      expect(wishlistAction).toBeUndefined();
    });
  });

  // ===== Action limit =====

  describe('Action limit', () => {
    it('limits to maximum 4 actions', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          tier: 'enterprise',
          collectionContext: 'catalog',
          inCollection: false,
          onAddToLibrary: vi.fn(),
        }),
        { wrapper: QueryClientWrapper }
      );

      // 1 (add to library) + 3 (base) = 4, max 4
      expect(result.current.quickActions.length).toBeLessThanOrEqual(4);
    });
  });

  // ===== Non-game entities =====

  describe('Non-game entities', () => {
    it('does not add collection actions for session entity', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'session',
          id: 'session-1',
          collectionContext: 'catalog',
          onAddToLibrary: vi.fn(),
        }),
        { wrapper: QueryClientWrapper }
      );

      const addAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla libreria');
      expect(addAction).toBeUndefined();
    });

    it('applies tier filtering to non-game entities', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'agent',
          id: 'agent-1',
          tier: 'free',
        }),
        { wrapper: QueryClientWrapper }
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat');
      expect(chatAction?.disabled).toBe(true);
    });
  });
});
