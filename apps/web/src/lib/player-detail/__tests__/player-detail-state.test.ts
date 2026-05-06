import { describe, expect, it } from 'vitest';

import {
  type DerivePlayerDetailUiStateInput,
  type PlayerDetailUiState,
  derivePlayerDetailUiState,
} from '../player-detail-state';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function input(
  overrides: Partial<DerivePlayerDetailUiStateInput> = {}
): DerivePlayerDetailUiStateInput {
  return {
    playerId: 'sara-rossi',
    isLoading: false,
    isError: false,
    hasData: true,
    ...overrides,
  };
}

function expect_state(derived: PlayerDetailUiState, expected: PlayerDetailUiState): void {
  expect(derived).toBe(expected);
}

// ---------------------------------------------------------------------------
// 4-state FSM: precedence matrix
// Precedence: playerId null → loading → error → !hasData → default
// ---------------------------------------------------------------------------

describe('derivePlayerDetailUiState', () => {
  // --- not-found (highest precedence: null playerId) -----------------------

  it("returns 'not-found' when playerId is null", () => {
    expect_state(derivePlayerDetailUiState(input({ playerId: null })), 'not-found');
  });

  it("returns 'not-found' when playerId is null even if isLoading=true", () => {
    // null playerId beats loading — no point loading if we have no id
    expect_state(
      derivePlayerDetailUiState(input({ playerId: null, isLoading: true })),
      'not-found'
    );
  });

  it("returns 'not-found' when playerId is null even if isError=true", () => {
    expect_state(derivePlayerDetailUiState(input({ playerId: null, isError: true })), 'not-found');
  });

  it("returns 'not-found' when hasData=false and not loading and not error", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({ playerId: 'sara-rossi', isLoading: false, isError: false, hasData: false })
      ),
      'not-found'
    );
  });

  // --- loading (second precedence) -----------------------------------------

  it("returns 'loading' when isLoading=true and playerId is present", () => {
    expect_state(
      derivePlayerDetailUiState(input({ playerId: 'sara-rossi', isLoading: true })),
      'loading'
    );
  });

  it("returns 'loading' when isLoading=true even with hasData=false", () => {
    expect_state(
      derivePlayerDetailUiState(input({ playerId: 'sara-rossi', isLoading: true, hasData: false })),
      'loading'
    );
  });

  it("returns 'loading' beats error when both flags are true", () => {
    // Per contract: loading check comes before error
    expect_state(
      derivePlayerDetailUiState(input({ playerId: 'sara-rossi', isLoading: true, isError: true })),
      'loading'
    );
  });

  // --- error (third precedence) --------------------------------------------

  it("returns 'error' when isError=true and not loading and playerId present", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({
          playerId: 'sara-rossi',
          isLoading: false,
          isError: true,
          hasData: false,
        })
      ),
      'error'
    );
  });

  it("returns 'error' when isError=true even if hasData=true (stale-data error)", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({ playerId: 'sara-rossi', isLoading: false, isError: true, hasData: true })
      ),
      'error'
    );
  });

  // --- default (base case) -------------------------------------------------

  it("returns 'default' for healthy populated state", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({
          playerId: 'sara-rossi',
          isLoading: false,
          isError: false,
          hasData: true,
        })
      ),
      'default'
    );
  });

  it("returns 'default' for non-null string playerId with valid data", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({ playerId: 'marco-bianchi', isLoading: false, isError: false, hasData: true })
      ),
      'default'
    );
  });

  it("returns 'default' for URL-encoded playerId slug", () => {
    expect_state(
      derivePlayerDetailUiState(
        input({ playerId: 'mario-rossi-junior', isLoading: false, isError: false, hasData: true })
      ),
      'default'
    );
  });
});
