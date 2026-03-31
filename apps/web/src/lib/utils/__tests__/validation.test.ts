import { describe, it, expect } from 'vitest';

import { isValidEmail } from '../validation';

describe('isValidEmail', () => {
  it.each(['user@example.com', 'user+tag@sub.domain.com', 'a@b.co'])(
    'returns true for valid email: %s',
    email => {
      expect(isValidEmail(email)).toBe(true);
    }
  );

  it.each([
    '',
    '   ',
    'notanemail',
    'missing@',
    '@nodomain',
    'spaces in@email.com',
    'double@@domain.com',
  ])('returns false for invalid email: %s', email => {
    expect(isValidEmail(email)).toBe(false);
  });
});
