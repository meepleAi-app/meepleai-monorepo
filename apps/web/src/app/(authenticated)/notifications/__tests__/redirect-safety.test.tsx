import { describe, it, expect } from 'vitest';
import { isSafeRelativeLink } from '@/lib/url-safety';

describe('Notifications detail link safety (#2182)', () => {
  it('rejects external link in detail.link', () => {
    expect(isSafeRelativeLink('https://evil.com')).toBe(false);
  });

  it('accepts safe deep link', () => {
    expect(isSafeRelativeLink('/library/private/abc-123/toolkit')).toBe(true);
  });
});
