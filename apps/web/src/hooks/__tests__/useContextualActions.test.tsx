/**
 * useContextualActions Tests
 * Issue #4062 - MeepleCard: Context-Aware Actions
 */

import { renderHook } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

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
        useContextualActions({ entity: 'game', id: 'game-1' })
      );

      // Should return the same 3 base actions as useEntityActions
      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Chat con Agent');
      expect(result.current.quickActions[1].label).toBe('Avvia Sessione');
      expect(result.current.quickActions[2].label).toBe('Condividi');
    });

    it('returns empty actions for custom entity', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'custom', id: 'custom-1' })
      );

      expect(result.current.quickActions).toHaveLength(0);
    });
  });

  // ===== Tier-based filtering =====

  describe('Tier-based filtering', () => {
    it('disables pro-tier actions for free users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' })
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat con Agent');
      expect(chatAction?.disabled).toBe(true);
    });

    it('disables basic-tier actions for free users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' })
      );

      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(sessionAction?.disabled).toBe(true);
    });

    it('enables basic-tier actions for basic users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'basic' })
      );

      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(sessionAction?.disabled).toBe(false);
    });

    it('enables all actions for pro users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'pro' })
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat con Agent');
      const sessionAction = result.current.quickActions.find(a => a.label === 'Avvia Sessione');
      expect(chatAction?.disabled).toBe(false);
      expect(sessionAction?.disabled).toBe(false);
    });

    it('enables all actions for enterprise users', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'enterprise' })
      );

      result.current.quickActions.forEach(action => {
        expect(action.disabled).toBeFalsy();
      });
    });

    it('keeps share action enabled for free tier', () => {
      const { result } = renderHook(() =>
        useContextualActions({ entity: 'game', id: 'game-1', tier: 'free' })
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
        })
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
        })
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
        })
      );

      const addAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla libreria');
      expect(addAction).toBeUndefined();
    });
  });

  // ===== Collection context: library =====

  describe('Library context', () => {
    it('adds remove action in library context', () => {
      const onRemove = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'library',
          onRemoveFromLibrary: onRemove,
        })
      );

      const removeAction = result.current.quickActions.find(a => a.label === 'Rimuovi dalla libreria');
      expect(removeAction).toBeDefined();
    });

    it('retains base actions in library context', () => {
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'library',
          tier: 'pro',
        })
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
        })
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
    it('adds wishlist action for shared games not in collection', () => {
      const onWishlist = vi.fn();
      const { result } = renderHook(() =>
        useContextualActions({
          entity: 'game',
          id: 'game-1',
          collectionContext: 'shared',
          inCollection: false,
          isWishlisted: false,
          onWishlistToggle: onWishlist,
        })
      );

      const wishlistAction = result.current.quickActions.find(a => a.label === 'Aggiungi alla wishlist');
      expect(wishlistAction).toBeDefined();
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
        })
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
        })
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
        })
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
        })
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
        })
      );

      const chatAction = result.current.quickActions.find(a => a.label === 'Chat');
      expect(chatAction?.disabled).toBe(true);
    });
  });
});
