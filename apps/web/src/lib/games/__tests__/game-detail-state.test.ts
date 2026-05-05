/**
 * Tests for `/games/[id]` FSM state derivation (Wave C.1, Issue #581).
 *
 * Covers the Phase 0.5 contract sez. 3 FSM cell matrix:
 *   Cell 1: gameId=null  → 'not-found'  (short-circuits FIRST, regardless of other flags)
 *   Cell 2: valid gameId + isLoading    → 'loading'
 *   Cell 3: valid gameId + isError      → 'error'
 *   Cell 4: valid gameId + !hasData     → 'not-found' (success(null))
 *   Cell 5: valid gameId + hasData      → 'default'
 *
 * Critical: gameId === null must short-circuit BEFORE isLoading/isError/hasData checks.
 * PR #697 omitted gameId from the input interface — fixed here.
 */

import { describe, it, expect } from 'vitest';

import {
  type GameDetailUiState,
  type DeriveGameDetailUiStateInput,
  deriveGameDetailUiState,
} from '../game-detail-state';

// Helper for building input objects
const input = (overrides: Partial<DeriveGameDetailUiStateInput>): DeriveGameDetailUiStateInput => ({
  gameId: 'valid-id',
  isLoading: false,
  isError: false,
  hasData: false,
  ...overrides,
});

describe('deriveGameDetailUiState', () => {
  // ============================================================
  // Cell 1: gameId === null — MUST short-circuit first
  // Property test: all 8 combinations of isLoading/isError/hasData
  // ============================================================
  describe('Cell 1 — gameId null short-circuits regardless of other flags', () => {
    const nullId = { gameId: null } as unknown as Pick<DeriveGameDetailUiStateInput, 'gameId'>;

    const combinations: Array<{
      isLoading: boolean;
      isError: boolean;
      hasData: boolean;
    }> = [
      { isLoading: false, isError: false, hasData: false },
      { isLoading: false, isError: false, hasData: true },
      { isLoading: false, isError: true, hasData: false },
      { isLoading: false, isError: true, hasData: true },
      { isLoading: true, isError: false, hasData: false },
      { isLoading: true, isError: false, hasData: true },
      { isLoading: true, isError: true, hasData: false },
      { isLoading: true, isError: true, hasData: true },
    ];

    combinations.forEach(({ isLoading, isError, hasData }) => {
      it(`returns 'not-found' when gameId=null, isLoading=${isLoading}, isError=${isError}, hasData=${hasData}`, () => {
        const result = deriveGameDetailUiState({
          ...nullId,
          isLoading,
          isError,
          hasData,
        });
        expect(result).toBe<GameDetailUiState>('not-found');
      });
    });
  });

  // ============================================================
  // Cell 2: valid gameId + isLoading → 'loading'
  // ============================================================
  describe('Cell 2 — isLoading → loading', () => {
    it("returns 'loading' when gameId valid and isLoading=true", () => {
      expect(
        deriveGameDetailUiState(input({ gameId: 'valid-id', isLoading: true }))
      ).toBe<GameDetailUiState>('loading');
    });

    it("returns 'loading' even when isError=true (loading takes precedence over error)", () => {
      expect(
        deriveGameDetailUiState(input({ gameId: 'valid-id', isLoading: true, isError: true }))
      ).toBe<GameDetailUiState>('loading');
    });
  });

  // ============================================================
  // Cell 3: valid gameId + isError → 'error'
  // ============================================================
  describe('Cell 3 — isError → error', () => {
    it("returns 'error' when gameId valid, isLoading=false, isError=true", () => {
      expect(
        deriveGameDetailUiState(input({ gameId: 'valid-id', isLoading: false, isError: true }))
      ).toBe<GameDetailUiState>('error');
    });
  });

  // ============================================================
  // Cell 4: valid gameId + success(null) → 'not-found'
  // ============================================================
  describe('Cell 4 — success(null) → not-found', () => {
    it("returns 'not-found' when gameId valid, not loading, no error, hasData=false", () => {
      expect(
        deriveGameDetailUiState(
          input({ gameId: 'valid-id', isLoading: false, isError: false, hasData: false })
        )
      ).toBe<GameDetailUiState>('not-found');
    });

    it('Cell 4 is distinct from Cell 1 (same shell, different input)', () => {
      const cell1 = deriveGameDetailUiState(
        input({ gameId: null, isLoading: false, isError: false, hasData: false })
      );
      const cell4 = deriveGameDetailUiState(
        input({ gameId: 'valid-id', isLoading: false, isError: false, hasData: false })
      );
      // Both produce not-found but via different code paths
      expect(cell1).toBe<GameDetailUiState>('not-found');
      expect(cell4).toBe<GameDetailUiState>('not-found');
    });
  });

  // ============================================================
  // Cell 5: valid gameId + hasData → 'default'
  // ============================================================
  describe('Cell 5 — success(data) → default', () => {
    it("returns 'default' when gameId valid, not loading, no error, hasData=true", () => {
      expect(
        deriveGameDetailUiState(
          input({ gameId: 'valid-id', isLoading: false, isError: false, hasData: true })
        )
      ).toBe<GameDetailUiState>('default');
    });
  });

  // ============================================================
  // Type contract: gameId must be string | null, never undefined
  // ============================================================
  describe('Type and precedence contract', () => {
    it('accepts UUID-shaped gameId strings', () => {
      const uuid = '00000000-0000-4000-8000-000000000581';
      expect(
        deriveGameDetailUiState(
          input({ gameId: uuid, isLoading: false, isError: false, hasData: true })
        )
      ).toBe<GameDetailUiState>('default');
    });

    it('precedence: null > loading > error > not-found > default', () => {
      // Verify strict priority ordering
      expect(
        deriveGameDetailUiState(
          input({ gameId: null, isLoading: true, isError: true, hasData: true })
        )
      ).toBe('not-found'); // null wins over all

      expect(
        deriveGameDetailUiState(
          input({ gameId: 'x', isLoading: true, isError: true, hasData: true })
        )
      ).toBe('loading'); // loading wins over error

      expect(
        deriveGameDetailUiState(
          input({ gameId: 'x', isLoading: false, isError: true, hasData: true })
        )
      ).toBe('error'); // error wins over hasData
    });
  });
});
