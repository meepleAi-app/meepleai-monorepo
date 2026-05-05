/**
 * Tests for `/agents/[id]` FSM state derivation (Wave C.2, Issue #581).
 *
 * Covers the Phase 0.5 contract sez. 3 FSM cell matrix:
 *   Cell 1: agentId=null → 'not-found' (short-circuits FIRST, regardless of other flags)
 *   Cell 2: valid agentId + isLoading   → 'loading'
 *   Cell 3: valid agentId + isError     → 'error'
 *   Cell 4: valid agentId + !hasData    → 'not-found' (success(null))
 *   Cell 5: valid agentId + hasData     → 'default'   (Cells 5-10 all return 'default')
 *
 * Critical: agentId === null must short-circuit BEFORE isLoading/isError/hasData checks.
 * This contract prevents the `/api/v1/agents/undefined` cascade failure (Wave C.1 lesson).
 *
 * Cell 10 (agent.gameId === null) maps to 'default' from this FSM perspective —
 * the standalone Knowledge tab empty state is handled at orchestrator level, not here.
 */

import { describe, it, expect } from 'vitest';

import {
  type AgentDetailUiState,
  type DeriveAgentDetailUiStateInput,
  deriveAgentDetailUiState,
} from '../agent-detail-state';

// Helper for building input objects with defaults
const input = (
  overrides: Partial<DeriveAgentDetailUiStateInput>
): DeriveAgentDetailUiStateInput => ({
  agentId: 'valid-agent-id',
  isLoading: false,
  isError: false,
  hasData: false,
  ...overrides,
});

describe('deriveAgentDetailUiState', () => {
  // ============================================================
  // Cell 1: agentId === null — MUST short-circuit first
  // Property test: all 8 combinations of isLoading/isError/hasData
  // ============================================================
  describe('Cell 1 — agentId null short-circuits regardless of other flags', () => {
    const combinations: Array<{ isLoading: boolean; isError: boolean; hasData: boolean }> = [
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
      it(`returns 'not-found' when agentId=null, isLoading=${isLoading}, isError=${isError}, hasData=${hasData}`, () => {
        const result = deriveAgentDetailUiState({
          agentId: null,
          isLoading,
          isError,
          hasData,
        });
        expect(result).toBe<AgentDetailUiState>('not-found');
      });
    });
  });

  // ============================================================
  // Cell 2: valid agentId + isLoading → 'loading'
  // ============================================================
  describe('Cell 2 — isLoading → loading', () => {
    it("returns 'loading' when agentId valid and isLoading=true", () => {
      expect(
        deriveAgentDetailUiState(input({ agentId: 'valid-id', isLoading: true }))
      ).toBe<AgentDetailUiState>('loading');
    });

    it("returns 'loading' even when isError=true (loading takes precedence over error)", () => {
      expect(
        deriveAgentDetailUiState(input({ agentId: 'valid-id', isLoading: true, isError: true }))
      ).toBe<AgentDetailUiState>('loading');
    });
  });

  // ============================================================
  // Cell 3: valid agentId + isError → 'error'
  // ============================================================
  describe('Cell 3 — isError → error', () => {
    it("returns 'error' when agentId valid, isLoading=false, isError=true", () => {
      expect(
        deriveAgentDetailUiState(input({ agentId: 'valid-id', isLoading: false, isError: true }))
      ).toBe<AgentDetailUiState>('error');
    });
  });

  // ============================================================
  // Cell 4: valid agentId + success(null) → 'not-found'
  // ============================================================
  describe('Cell 4 — success(null) → not-found', () => {
    it("returns 'not-found' when agentId valid, not loading, no error, hasData=false", () => {
      expect(
        deriveAgentDetailUiState(
          input({ agentId: 'valid-id', isLoading: false, isError: false, hasData: false })
        )
      ).toBe<AgentDetailUiState>('not-found');
    });

    it('Cell 4 is distinct from Cell 1 (same shell, different input)', () => {
      const cell1 = deriveAgentDetailUiState(
        input({ agentId: null, isLoading: false, isError: false, hasData: false })
      );
      const cell4 = deriveAgentDetailUiState(
        input({ agentId: 'valid-id', isLoading: false, isError: false, hasData: false })
      );
      // Both produce not-found but via different code paths
      expect(cell1).toBe<AgentDetailUiState>('not-found');
      expect(cell4).toBe<AgentDetailUiState>('not-found');
    });
  });

  // ============================================================
  // Cell 5: valid agentId + hasData → 'default'
  // (Also covers Cells 6-10 — all return 'default' from FSM perspective)
  // ============================================================
  describe('Cell 5 (and Cells 6-10) — success(data) → default', () => {
    it("returns 'default' when agentId valid, not loading, no error, hasData=true", () => {
      expect(
        deriveAgentDetailUiState(
          input({ agentId: 'valid-id', isLoading: false, isError: false, hasData: true })
        )
      ).toBe<AgentDetailUiState>('default');
    });

    it("Cell 10 (agent.gameId=null) also returns 'default' — standalone handled at orchestrator level", () => {
      // FSM does NOT distinguish Cell 10 from Cells 5-9.
      // Knowledge tab standalone state is derived from agent.gameId at orchestrator.
      expect(
        deriveAgentDetailUiState(
          input({ agentId: 'valid-id', isLoading: false, isError: false, hasData: true })
        )
      ).toBe<AgentDetailUiState>('default');
    });
  });

  // ============================================================
  // Type and precedence contract
  // ============================================================
  describe('Type and precedence contract', () => {
    it('accepts UUID-shaped agentId strings', () => {
      const uuid = '00000000-0000-4000-8000-000000000581';
      expect(
        deriveAgentDetailUiState(
          input({ agentId: uuid, isLoading: false, isError: false, hasData: true })
        )
      ).toBe<AgentDetailUiState>('default');
    });

    it('precedence: null > loading > error > not-found > default', () => {
      // Verify strict priority ordering
      expect(
        deriveAgentDetailUiState(
          input({ agentId: null, isLoading: true, isError: true, hasData: true })
        )
      ).toBe('not-found'); // null wins over all

      expect(
        deriveAgentDetailUiState(
          input({ agentId: 'x', isLoading: true, isError: true, hasData: true })
        )
      ).toBe('loading'); // loading wins over error

      expect(
        deriveAgentDetailUiState(
          input({ agentId: 'x', isLoading: false, isError: true, hasData: true })
        )
      ).toBe('error'); // error wins over hasData
    });
  });
});
