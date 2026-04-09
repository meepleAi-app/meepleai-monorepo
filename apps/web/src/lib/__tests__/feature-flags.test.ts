import { describe, it, expect, afterEach } from 'vitest';

import { isUxRedesignEnabled } from '../feature-flags';

describe('feature-flags', () => {
  const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
  });

  describe('isUxRedesignEnabled', () => {
    it('returns false when env var is undefined', () => {
      delete process.env.NEXT_PUBLIC_UX_REDESIGN;
      expect(isUxRedesignEnabled()).toBe(false);
    });

    it('returns false when env var is "false"', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
      expect(isUxRedesignEnabled()).toBe(false);
    });

    it('returns true when env var is "true"', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
      expect(isUxRedesignEnabled()).toBe(true);
    });

    it('returns false for any other value', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = '1';
      expect(isUxRedesignEnabled()).toBe(false);
    });
  });
});
