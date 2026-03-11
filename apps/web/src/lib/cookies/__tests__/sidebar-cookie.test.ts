import { describe, it, expect } from 'vitest';

import {
  SIDEBAR_COOKIE_NAME,
  SIDEBAR_COOKIE_MAX_AGE,
  parseSidebarCookie,
} from '../sidebar-cookie';

describe('sidebar-cookie', () => {
  it('returns false for undefined', () => {
    expect(parseSidebarCookie(undefined)).toBe(false);
  });

  it('returns true for "true"', () => {
    expect(parseSidebarCookie('true')).toBe(true);
  });

  it('returns false for "false"', () => {
    expect(parseSidebarCookie('false')).toBe(false);
  });

  it('returns false for invalid values', () => {
    expect(parseSidebarCookie('yes')).toBe(false);
    expect(parseSidebarCookie('')).toBe(false);
  });

  it('exports correct cookie name', () => {
    expect(SIDEBAR_COOKIE_NAME).toBe('meepleai-sidebar-collapsed');
  });

  it('exports max age of 1 year', () => {
    expect(SIDEBAR_COOKIE_MAX_AGE).toBe(60 * 60 * 24 * 365);
  });
});
