import { describe, it, expect } from 'vitest';

import { personInitials } from '@/lib/format/person-initials';

describe('personInitials', () => {
  it('takes the first + last word initials for multi-word names', () => {
    expect(personInitials('Mario Rossi')).toBe('MR');
    expect(personInitials('Mario Luigi Rossi')).toBe('MR');
    expect(personInitials('Guest Gina')).toBe('GG');
  });

  it('takes up to the first two code points for a single word', () => {
    expect(personInitials('Alice')).toBe('AL');
    expect(personInitials('A')).toBe('A');
  });

  it('is code-point safe for emoji (never splits a surrogate pair)', () => {
    // "🎲" is a surrogate pair; charAt(0) would return a broken half. Spread keeps it whole.
    expect(personInitials('🎲')).toBe('🎲');
    expect(personInitials('🎲 Player')).toBe('🎲P');
  });

  it('falls back to "?" for empty / whitespace-only names', () => {
    expect(personInitials('')).toBe('?');
    expect(personInitials('   ')).toBe('?');
  });

  it('trims surrounding whitespace', () => {
    expect(personInitials('  Bob Smith  ')).toBe('BS');
  });
});
