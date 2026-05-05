import { describe, expect, it } from 'vitest';

import {
  type DerivePlayersUiStateInput,
  type PlayersUiState,
  derivePlayersUiState,
} from '../players-state';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function input(overrides: Partial<DerivePlayersUiStateInput> = {}): DerivePlayersUiStateInput {
  return {
    isLoading: false,
    isError: false,
    hasData: true,
    hasFilters: false,
    filteredCount: 5,
    ...overrides,
  };
}

function expect_state(derived: PlayersUiState, expected: PlayersUiState): void {
  expect(derived).toBe(expected);
}

// ---------------------------------------------------------------------------
// 5-state FSM: precedence matrix
// ---------------------------------------------------------------------------

describe('derivePlayersUiState', () => {
  // --- loading (highest precedence) ----------------------------------------

  it("returns 'loading' when isLoading=true, even with data present", () => {
    expect_state(derivePlayersUiState(input({ isLoading: true, hasData: true })), 'loading');
  });

  it("returns 'loading' when isLoading=true and no data", () => {
    expect_state(
      derivePlayersUiState(input({ isLoading: true, hasData: false, filteredCount: 0 })),
      'loading'
    );
  });

  it("returns 'loading' beats error when both flags are true", () => {
    // Per contract: isLoading check first → 'loading' wins over 'error'
    expect_state(derivePlayersUiState(input({ isLoading: true, isError: true })), 'loading');
  });

  // --- error (second precedence) -------------------------------------------

  it("returns 'error' when isError=true and not loading", () => {
    expect_state(
      derivePlayersUiState(
        input({ isLoading: false, isError: true, hasData: false, filteredCount: 0 })
      ),
      'error'
    );
  });

  it("returns 'error' when isError=true even if hasData=true (stale-data error)", () => {
    expect_state(
      derivePlayersUiState(input({ isLoading: false, isError: true, hasData: true })),
      'error'
    );
  });

  // --- empty (third precedence) --------------------------------------------

  it("returns 'empty' when fetch resolved with zero entries and no filters", () => {
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: false,
          hasFilters: false,
          filteredCount: 0,
        })
      ),
      'empty'
    );
  });

  it("returns 'empty' when hasData=false even with hasFilters=true (backend returned nothing)", () => {
    // hasData=false means the raw dataset is empty; filter is irrelevant
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: false,
          hasFilters: true,
          filteredCount: 0,
        })
      ),
      'empty'
    );
  });

  // --- filtered-empty (fourth precedence) ----------------------------------

  it("returns 'filtered-empty' when data exists but active filter eliminates all results", () => {
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: true,
          hasFilters: true,
          filteredCount: 0,
        })
      ),
      'filtered-empty'
    );
  });

  it("returns 'filtered-empty' only when BOTH hasFilters=true AND filteredCount=0", () => {
    // filteredCount=0 alone without hasFilters does NOT trigger filtered-empty
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: true,
          hasFilters: false,
          filteredCount: 0,
        })
      ),
      'default' // no filter active → shouldn't be filtered-empty; hasData=true → not empty either
    );
  });

  // --- default (base case) -------------------------------------------------

  it("returns 'default' for healthy populated state with no filters", () => {
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: true,
          hasFilters: false,
          filteredCount: 5,
        })
      ),
      'default'
    );
  });

  it("returns 'default' when filters applied and results still present", () => {
    expect_state(
      derivePlayersUiState(
        input({
          isLoading: false,
          isError: false,
          hasData: true,
          hasFilters: true,
          filteredCount: 3,
        })
      ),
      'default'
    );
  });

  it("returns 'default' when filteredCount=1 (edge: single result after filter)", () => {
    expect_state(
      derivePlayersUiState(input({ hasData: true, hasFilters: true, filteredCount: 1 })),
      'default'
    );
  });
});
