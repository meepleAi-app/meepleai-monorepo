import { describe, it, expect } from 'vitest';
import { isLibroGame } from '../is-libro-game';

describe('isLibroGame', () => {
  it('returns true for Nanolith', () => {
    expect(isLibroGame({ gameTitle: 'Nanolith' })).toBe(true);
  });

  it('returns false for non-allowlisted title (Catan)', () => {
    expect(isLibroGame({ gameTitle: 'Catan' })).toBe(false);
  });

  it('returns false for empty string', () => {
    expect(isLibroGame({ gameTitle: '' })).toBe(false);
  });

  it('returns false for null', () => {
    expect(isLibroGame({ gameTitle: null })).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(isLibroGame({ gameTitle: undefined })).toBe(false);
    expect(isLibroGame({})).toBe(false);
  });
});
