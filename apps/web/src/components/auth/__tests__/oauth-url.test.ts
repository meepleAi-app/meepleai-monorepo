/**
 * Tests for buildOAuthUrl helper
 *
 * Ported from the legacy OAuthButtons.test.tsx `buildOAuthUrl` suite
 * after the component itself was removed in favour of v2 OAuthButton.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { buildOAuthUrl } from '../oauth-url';

describe('buildOAuthUrl', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it('builds correct URL for Google provider with default API base', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    expect(buildOAuthUrl('google')).toBe('http://localhost:8080/api/v1/auth/oauth/google/login');
  });

  it('builds correct URL for Discord provider with default API base', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    expect(buildOAuthUrl('discord')).toBe('http://localhost:8080/api/v1/auth/oauth/discord/login');
  });

  it('builds correct URL for GitHub provider with default API base', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    expect(buildOAuthUrl('github')).toBe('http://localhost:8080/api/v1/auth/oauth/github/login');
  });

  it('uses custom API base from environment variable', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.production.com';

    expect(buildOAuthUrl('google')).toBe(
      'https://api.production.com/api/v1/auth/oauth/google/login'
    );
  });

  it('handles API base with trailing slash (does not normalize)', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com/';

    expect(buildOAuthUrl('discord')).toBe(
      'https://api.example.com//api/v1/auth/oauth/discord/login'
    );
  });

  it('handles API base without protocol', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'api.example.com';

    expect(buildOAuthUrl('github')).toBe('api.example.com/api/v1/auth/oauth/github/login');
  });

  it('handles empty string API base (falls back to default)', () => {
    process.env.NEXT_PUBLIC_API_BASE = '';

    expect(buildOAuthUrl('google')).toBe('http://localhost:8080/api/v1/auth/oauth/google/login');
  });
});
