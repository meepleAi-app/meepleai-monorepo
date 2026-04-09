/**
 * Tests for client-side view mode cookie helpers.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { readViewModeCookie, writeViewModeCookie, clearViewModeCookie } from '../cookie';
import { VIEW_MODE_COOKIE } from '../constants';

describe('view-mode cookie helpers', () => {
  beforeEach(() => {
    // Reset document.cookie before each test.
    // NOTE: `configurable: true` is REQUIRED — without it, the second
    // beforeEach silently fails because the property becomes non-configurable
    // after the first Object.defineProperty call, causing flaky tests.
    Object.defineProperty(document, 'cookie', {
      writable: true,
      configurable: true,
      value: '',
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('readViewModeCookie', () => {
    it('returns null when cookie is absent', () => {
      document.cookie = '';
      expect(readViewModeCookie()).toBeNull();
    });

    it('returns "admin" when cookie value is admin', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=admin`;
      expect(readViewModeCookie()).toBe('admin');
    });

    it('returns "user" when cookie value is user', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=user`;
      expect(readViewModeCookie()).toBe('user');
    });

    it('returns null when cookie value is invalid', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=malicious`;
      expect(readViewModeCookie()).toBeNull();
    });

    it('correctly extracts cookie when multiple cookies present', () => {
      document.cookie = `other_cookie=foo; ${VIEW_MODE_COOKIE}=admin; third=bar`;
      expect(readViewModeCookie()).toBe('admin');
    });
  });

  describe('writeViewModeCookie', () => {
    it('writes admin value to document.cookie', () => {
      writeViewModeCookie('admin');
      expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=admin`);
    });

    it('writes user value to document.cookie', () => {
      writeViewModeCookie('user');
      expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=user`);
    });

    it('includes path=/ in the cookie string', () => {
      const spy = vi.spyOn(document, 'cookie', 'set');
      writeViewModeCookie('admin');
      expect(spy).toHaveBeenCalledWith(expect.stringContaining('path=/'));
    });

    it('includes SameSite=Lax in the cookie string', () => {
      const spy = vi.spyOn(document, 'cookie', 'set');
      writeViewModeCookie('admin');
      expect(spy).toHaveBeenCalledWith(expect.stringContaining('SameSite=Lax'));
    });
  });

  describe('clearViewModeCookie', () => {
    it('clears the cookie by setting expired date', () => {
      const spy = vi.spyOn(document, 'cookie', 'set');
      clearViewModeCookie();
      expect(spy).toHaveBeenCalledWith(expect.stringContaining('Max-Age=0'));
    });
  });
});
