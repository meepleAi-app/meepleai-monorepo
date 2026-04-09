/**
 * Tests for server-side view mode cookie reader.
 * Mocks `next/headers` cookies() API.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { readViewModeCookieServer } from '../server';
import { VIEW_MODE_COOKIE } from '../constants';

// Mock next/headers
const mockGet = vi.fn();
vi.mock('next/headers', () => ({
  cookies: () => ({
    get: mockGet,
  }),
}));

describe('readViewModeCookieServer', () => {
  beforeEach(() => {
    mockGet.mockReset();
  });

  it('returns null when cookie is absent', async () => {
    mockGet.mockReturnValue(undefined);
    const result = await readViewModeCookieServer();
    expect(mockGet).toHaveBeenCalledWith(VIEW_MODE_COOKIE);
    expect(result).toBeNull();
  });

  it('returns "admin" when cookie has admin value', async () => {
    mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'admin' });
    const result = await readViewModeCookieServer();
    expect(result).toBe('admin');
  });

  it('returns "user" when cookie has user value', async () => {
    mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'user' });
    const result = await readViewModeCookieServer();
    expect(result).toBe('user');
  });

  it('returns null when cookie has invalid value', async () => {
    mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'malicious' });
    const result = await readViewModeCookieServer();
    expect(result).toBeNull();
  });
});
