/**
 * Wave C.1 (Issue #581) — deriveGameDetailUiState FSM unit tests.
 */

import { describe, expect, it } from 'vitest';

import { deriveGameDetailUiState } from '../game-detail-state';

describe('deriveGameDetailUiState (Wave C.1)', () => {
  it('returns "error" when isError is true (highest precedence)', () => {
    expect(deriveGameDetailUiState({ isError: true, isLoading: true, hasData: true })).toBe(
      'error'
    );
    expect(deriveGameDetailUiState({ isError: true, isLoading: false, hasData: false })).toBe(
      'error'
    );
  });

  it('returns "loading" when isLoading is true and not error', () => {
    expect(deriveGameDetailUiState({ isError: false, isLoading: true, hasData: false })).toBe(
      'loading'
    );
    expect(deriveGameDetailUiState({ isError: false, isLoading: true, hasData: true })).toBe(
      'loading'
    );
  });

  it('returns "not-found" when not loading/error and hasData is false', () => {
    expect(deriveGameDetailUiState({ isError: false, isLoading: false, hasData: false })).toBe(
      'not-found'
    );
  });

  it('returns "default" when not loading/error and hasData is true', () => {
    expect(deriveGameDetailUiState({ isError: false, isLoading: false, hasData: true })).toBe(
      'default'
    );
  });
});
