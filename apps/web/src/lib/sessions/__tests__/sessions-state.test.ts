import { describe, expect, it } from 'vitest';

import {
  type DeriveSessionsUiStateInput,
  type SessionsUiState,
  deriveSessionsUiState,
  parseStateOverride,
} from '../sessions-state';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function input(overrides: Partial<DeriveSessionsUiStateInput> = {}): DeriveSessionsUiStateInput {
  return {
    isLoading: false,
    isError: false,
    totalCount: 5,
    filteredCount: 5,
    ...overrides,
  };
}

function expectState(derived: SessionsUiState, expected: SessionsUiState): void {
  expect(derived).toBe(expected);
}

// ---------------------------------------------------------------------------
// 5-state FSM: precedence matrix
// ---------------------------------------------------------------------------

describe('deriveSessionsUiState', () => {
  // --- loading (highest precedence) ----------------------------------------

  it("returns 'loading' when isLoading=true, even with data present", () => {
    expectState(deriveSessionsUiState(input({ isLoading: true, totalCount: 5 })), 'loading');
  });

  it("returns 'loading' when isLoading=true and no data", () => {
    expectState(
      deriveSessionsUiState(input({ isLoading: true, totalCount: 0, filteredCount: 0 })),
      'loading'
    );
  });

  it("returns 'loading' beats error when both flags are true", () => {
    // Per contract: isLoading check first → 'loading' wins over 'error'
    expectState(deriveSessionsUiState(input({ isLoading: true, isError: true })), 'loading');
  });

  // --- error (second precedence) -------------------------------------------

  it("returns 'error' when isError=true and not loading", () => {
    expectState(
      deriveSessionsUiState(
        input({ isLoading: false, isError: true, totalCount: 0, filteredCount: 0 })
      ),
      'error'
    );
  });

  it("returns 'error' when isError=true even if totalCount>0 (stale-data error)", () => {
    expectState(
      deriveSessionsUiState(input({ isLoading: false, isError: true, totalCount: 3 })),
      'error'
    );
  });

  // --- empty (third precedence) --------------------------------------------

  it("returns 'empty' when fetch resolved with zero sessions and no filters applied", () => {
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 0,
          filteredCount: 0,
        })
      ),
      'empty'
    );
  });

  it("returns 'empty' when totalCount=0 regardless of filteredCount", () => {
    // totalCount=0 means backend returned nothing; filter state is irrelevant
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 0,
          filteredCount: 0,
        })
      ),
      'empty'
    );
  });

  // --- filtered-empty (fourth precedence) ----------------------------------

  it("returns 'filtered-empty' when data exists but active filter eliminates all results", () => {
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 6,
          filteredCount: 0,
        })
      ),
      'filtered-empty'
    );
  });

  it("returns 'filtered-empty' when totalCount>0 but filteredCount=0 (edge: 1 session, strong filter)", () => {
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 1,
          filteredCount: 0,
        })
      ),
      'filtered-empty'
    );
  });

  // --- default (base case) -------------------------------------------------

  it("returns 'default' for healthy populated state with no filters", () => {
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 6,
          filteredCount: 6,
        })
      ),
      'default'
    );
  });

  it("returns 'default' when status filter applied and results still present", () => {
    expectState(
      deriveSessionsUiState(
        input({
          isLoading: false,
          isError: false,
          totalCount: 6,
          filteredCount: 2, // e.g., 2 inprogress sessions visible
        })
      ),
      'default'
    );
  });

  it("returns 'default' when filteredCount=1 (edge: single visible result after filter)", () => {
    expectState(deriveSessionsUiState(input({ totalCount: 3, filteredCount: 1 })), 'default');
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride re-export — confirm sessions-state.ts re-exports it
// ---------------------------------------------------------------------------

describe('parseStateOverride re-export from sessions-state', () => {
  it('is a function exported from sessions-state (not just from sessions-visual-test-fixture)', () => {
    expect(typeof parseStateOverride).toBe('function');
  });

  it("returns 'loading' when ?state=loading (re-export delegates to fixture impl)", () => {
    expect(parseStateOverride(new URLSearchParams({ state: 'loading' }))).toBe('loading');
  });

  it("returns 'empty' when ?state=empty", () => {
    expect(parseStateOverride(new URLSearchParams({ state: 'empty' }))).toBe('empty');
  });

  it('returns null for unknown state values', () => {
    expect(parseStateOverride(new URLSearchParams({ state: 'unknown' }))).toBeNull();
  });

  it('returns null when no state param', () => {
    expect(parseStateOverride(new URLSearchParams())).toBeNull();
  });
});
