/**
 * Unit tests for session-live-state.ts (Wave D.2, Issue #746)
 *
 * Covers:
 *   - deriveSessionLiveUiState: 5 cells × edge cases (race conditions)
 *   - deriveSessionLiveDialogState: 3 states + invalid/absent params
 *   - parseStateOverride re-export (gated/ungated × valid/invalid)
 */
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import {
  deriveSessionLiveUiState,
  deriveSessionLiveDialogState,
  parseStateOverride,
  type SessionLiveUiState,
  type SessionLiveDialogState,
} from '../session-live-state';

// ---------------------------------------------------------------------------
// deriveSessionLiveUiState — 5 cells
// ---------------------------------------------------------------------------

describe('deriveSessionLiveUiState', () => {
  describe('Cell 1 — sessionId === null → not-found (highest priority)', () => {
    it('returns not-found when sessionId is null regardless of other state', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: null,
          isLoading: false,
          isError: false,
          hasData: false,
        })
      ).toBe('not-found');
    });

    it('returns not-found when sessionId is null even when isLoading is true (null beats loading)', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: null,
          isLoading: true,
          isError: false,
          hasData: false,
        })
      ).toBe('not-found');
    });

    it('returns not-found when sessionId is null even when isError is true', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: null,
          isLoading: false,
          isError: true,
          hasData: false,
        })
      ).toBe('not-found');
    });
  });

  describe('Cell 2 — isLoading === true → loading', () => {
    it('returns loading when sessionId present and isLoading is true', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: true,
          isError: false,
          hasData: false,
        })
      ).toBe('loading');
    });

    it('loading beats error (isLoading+isError race condition → loading wins)', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: true,
          isError: true,
          hasData: false,
        })
      ).toBe('loading');
    });

    it('loading beats not-having-data (isLoading+!hasData race → loading wins)', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: true,
          isError: false,
          hasData: false,
        })
      ).toBe('loading');
    });
  });

  describe('Cell 3 — isError === true → error', () => {
    it('returns error when sessionId present, not loading, and isError is true', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: false,
          isError: true,
          hasData: false,
        })
      ).toBe('error');
    });

    it('error is returned even if hasData is somehow true (error overrides data)', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: false,
          isError: true,
          hasData: true,
        })
      ).toBe('error');
    });
  });

  describe('Cell 4 — !hasData → not-found (404 from backend)', () => {
    it('returns not-found when sessionId present, not loading, not error, but no data', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: false,
          isError: false,
          hasData: false,
        })
      ).toBe('not-found');
    });
  });

  describe('Cell 5 — default (healthy, data present)', () => {
    it('returns default when sessionId present, not loading, not error, and hasData', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'abc-123',
          isLoading: false,
          isError: false,
          hasData: true,
        })
      ).toBe('default');
    });

    it('returns default for a typical deterministic UUID sessionId', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: '00000000-0000-4000-8000-000000000d20',
          isLoading: false,
          isError: false,
          hasData: true,
        })
      ).toBe('default');
    });
  });

  describe('precedence order exhaustive check', () => {
    it('null sessionId always wins (all other booleans true)', () => {
      expect(
        deriveSessionLiveUiState({ sessionId: null, isLoading: true, isError: true, hasData: true })
      ).toBe('not-found');
    });

    it('all false with data → default', () => {
      expect(
        deriveSessionLiveUiState({
          sessionId: 'sess-1',
          isLoading: false,
          isError: false,
          hasData: true,
        })
      ).toBe('default');
    });
  });
});

// ---------------------------------------------------------------------------
// deriveSessionLiveDialogState — 3 states
// ---------------------------------------------------------------------------

describe('deriveSessionLiveDialogState', () => {
  it('returns pause when ?dialog=pause', () => {
    const params = new URLSearchParams('dialog=pause');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('pause');
  });

  it('returns endgame when ?dialog=endgame', () => {
    const params = new URLSearchParams('dialog=endgame');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('endgame');
  });

  it('returns none when dialog param is absent', () => {
    const params = new URLSearchParams('');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('none');
  });

  it('returns none when dialog param is an unknown value', () => {
    const params = new URLSearchParams('dialog=unknown');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('none');
  });

  it('returns none when dialog param is empty string', () => {
    const params = new URLSearchParams('dialog=');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('none');
  });

  it('is case-sensitive — Pause (capital P) returns none', () => {
    const params = new URLSearchParams('dialog=Pause');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('none');
  });

  it('other search params do not affect dialog state', () => {
    const params = new URLSearchParams('tab=tools&dialog=pause&mtab=score');
    expect(deriveSessionLiveDialogState(params)).toBe<SessionLiveDialogState>('pause');
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride — gated by STATE_OVERRIDE_ENABLED
// ---------------------------------------------------------------------------

describe('parseStateOverride (re-exported)', () => {
  // In test environment, NODE_ENV !== 'production', so STATE_OVERRIDE_ENABLED is true.
  it('returns loading for ?state=loading in test/dev environment', () => {
    const params = new URLSearchParams('state=loading');
    const result = parseStateOverride(params);
    expect(result).toBe<SessionLiveUiState>('loading');
  });

  it('returns not-found for ?state=not-found in test/dev environment', () => {
    const params = new URLSearchParams('state=not-found');
    const result = parseStateOverride(params);
    expect(result).toBe<SessionLiveUiState>('not-found');
  });

  it('returns null for ?state=error (intentionally excluded — not reproducible via URL)', () => {
    const params = new URLSearchParams('state=error');
    const result = parseStateOverride(params);
    expect(result).toBeNull();
  });

  it('returns null for ?state=default (not a valid override)', () => {
    const params = new URLSearchParams('state=default');
    const result = parseStateOverride(params);
    expect(result).toBeNull();
  });

  it('returns null when state param is absent', () => {
    const params = new URLSearchParams('');
    expect(parseStateOverride(params)).toBeNull();
  });

  it('returns null for unknown state value', () => {
    const params = new URLSearchParams('state=bogus');
    expect(parseStateOverride(params)).toBeNull();
  });
});
