import { describe, it, expect } from 'vitest';
import { isSafeRelativeLink, assertSafeRelativeOrFallback } from '@/lib/url-safety';

describe('isSafeRelativeLink', () => {
  describe('SAFE inputs (return true)', () => {
    it.each([
      ['/library'],
      ['/sessions/abc-123/scores'],
      ['/games?tab=discover'],
      ['/profile#settings'],
      ['/'],
    ])('accepts safe relative path: %s', input => {
      expect(isSafeRelativeLink(input)).toBe(true);
    });
  });

  describe('UNSAFE inputs (return false) — 8 attack vectors', () => {
    it.each([
      ['https://evil.com', 'absolute external'],
      ['http://evil.com/path', 'absolute external http'],
      ['//evil.com', 'protocol-relative'],
      ['\\\\evil.com', 'Windows path'],
      ['javascript:alert(1)', 'scheme injection'],
      ['data:text/html,<script>', 'data URI'],
      ['%2F%2Fevil.com', 'encoded protocol-relative'],
      ['  //evil.com', 'whitespace bypass'],
    ])('rejects %s (%s)', (input, _label) => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });

  describe('UNSAFE inputs — control character bypass (CVE-class)', () => {
    it.each([
      ['/\t//evil.com', 'tab-before-protocol-relative bypass'],
      ['/\n//evil.com', 'newline-before-protocol-relative bypass'],
      ['/\r//evil.com', 'CR-before-protocol-relative bypass'],
      ['/library\x00//evil.com', 'NUL byte injection'],
    ])('rejects %s (%s)', (input, _label) => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });

  describe('UNSAFE inputs — double-encoding bypass', () => {
    it.each([
      ['/%252F%252Fevil.com', 'double-encoded protocol-relative'],
      ['/%252F%255Cevil.com', 'double-encoded slash-backslash'],
    ])('rejects %s (%s)', (input, _label) => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });

  describe('EDGE inputs (return false defensively)', () => {
    it.each([[''], ['null'], ['undefined']])('rejects edge input: %s', input => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });
});

describe('assertSafeRelativeOrFallback', () => {
  it('returns input when safe', () => {
    expect(assertSafeRelativeOrFallback('/library', '/dashboard')).toBe('/library');
  });

  it('preserves root / path', () => {
    expect(assertSafeRelativeOrFallback('/', '/dashboard')).toBe('/');
  });

  it('returns fallback when unsafe', () => {
    expect(assertSafeRelativeOrFallback('https://evil.com', '/dashboard')).toBe('/dashboard');
  });

  it('returns fallback when null/undefined', () => {
    expect(assertSafeRelativeOrFallback(null, '/dashboard')).toBe('/dashboard');
    expect(assertSafeRelativeOrFallback(undefined, '/dashboard')).toBe('/dashboard');
  });

  it('returns fallback when empty string', () => {
    expect(assertSafeRelativeOrFallback('', '/dashboard')).toBe('/dashboard');
  });
});
