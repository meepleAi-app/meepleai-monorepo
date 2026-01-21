/**
 * Server-Side Authentication Utilities Test Suite (Issue #2766)
 *
 * Tests for auth/server.ts:
 * - getServerUser() session validation
 * - isAdmin() role check
 * - isEditor() role check (includes admin)
 * - hasRole() specific role check
 * - hasAnyRole() multiple roles check
 *
 * Uses mocked Next.js cookies() and fetch
 */

import { getServerUser, isAdmin, isEditor, hasRole, hasAnyRole } from '../server';
import type { AuthUser } from '@/types/auth';

// Mock next/headers cookies
const mockCookieGet = vi.fn();
vi.mock('next/headers', () => ({
  cookies: vi.fn(() =>
    Promise.resolve({
      get: mockCookieGet,
    })
  ),
}));

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

// Test user factory
const createMockUser = (overrides?: Partial<AuthUser>): AuthUser => ({
  id: 'user-123',
  email: 'test@example.com',
  name: 'Test User',
  role: 'user',
  avatarUrl: null,
  createdAt: '2024-01-01T00:00:00Z',
  ...overrides,
});

describe('getServerUser', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Suppress console.error for expected errors
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns null when no session cookie exists', async () => {
    mockCookieGet.mockReturnValue(undefined);

    const result = await getServerUser();

    expect(result).toBeNull();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('returns user when session is valid', async () => {
    const mockUser = createMockUser();
    mockCookieGet.mockReturnValue({ value: 'valid-session-token' });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ user: mockUser }),
    });

    const result = await getServerUser();

    expect(result).toEqual(mockUser);
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/auth/me'),
      expect.objectContaining({
        headers: { Cookie: 'meepleai_session=valid-session-token' },
        credentials: 'include',
        cache: 'no-store',
      })
    );
  });

  it('returns null when API returns non-ok response', async () => {
    mockCookieGet.mockReturnValue({ value: 'expired-session-token' });
    mockFetch.mockResolvedValue({
      ok: false,
      status: 401,
    });

    const result = await getServerUser();

    expect(result).toBeNull();
  });

  it('returns null when API returns no user', async () => {
    mockCookieGet.mockReturnValue({ value: 'valid-session-token' });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({}),
    });

    const result = await getServerUser();

    expect(result).toBeNull();
  });

  it('returns null when API returns null user', async () => {
    mockCookieGet.mockReturnValue({ value: 'valid-session-token' });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ user: null }),
    });

    const result = await getServerUser();

    expect(result).toBeNull();
  });

  it('returns null when fetch throws network error', async () => {
    mockCookieGet.mockReturnValue({ value: 'valid-session-token' });
    mockFetch.mockRejectedValue(new Error('Network error'));

    const result = await getServerUser();

    expect(result).toBeNull();
    expect(console.error).toHaveBeenCalled();
  });

  it('returns null when JSON parsing fails', async () => {
    mockCookieGet.mockReturnValue({ value: 'valid-session-token' });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.reject(new Error('Invalid JSON')),
    });

    const result = await getServerUser();

    expect(result).toBeNull();
    expect(console.error).toHaveBeenCalled();
  });

  it('includes session cookie in request headers', async () => {
    const sessionToken = 'test-session-abc123';
    mockCookieGet.mockReturnValue({ value: sessionToken });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ user: createMockUser() }),
    });

    await getServerUser();

    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: { Cookie: `meepleai_session=${sessionToken}` },
      })
    );
  });

  it('uses no-store cache policy', async () => {
    mockCookieGet.mockReturnValue({ value: 'session-token' });
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ user: createMockUser() }),
    });

    await getServerUser();

    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        cache: 'no-store',
      })
    );
  });
});

describe('isAdmin', () => {
  it('returns true for admin role', () => {
    const user = createMockUser({ role: 'admin' });
    expect(isAdmin(user)).toBe(true);
  });

  it('returns true for Admin role (case insensitive)', () => {
    const user = createMockUser({ role: 'Admin' });
    expect(isAdmin(user)).toBe(true);
  });

  it('returns true for ADMIN role (uppercase)', () => {
    const user = createMockUser({ role: 'ADMIN' });
    expect(isAdmin(user)).toBe(true);
  });

  it('returns false for non-admin roles', () => {
    expect(isAdmin(createMockUser({ role: 'user' }))).toBe(false);
    expect(isAdmin(createMockUser({ role: 'editor' }))).toBe(false);
    expect(isAdmin(createMockUser({ role: 'moderator' }))).toBe(false);
  });
});

describe('isEditor', () => {
  it('returns true for editor role', () => {
    const user = createMockUser({ role: 'editor' });
    expect(isEditor(user)).toBe(true);
  });

  it('returns true for admin role (admin is also editor)', () => {
    const user = createMockUser({ role: 'admin' });
    expect(isEditor(user)).toBe(true);
  });

  it('returns true for Editor role (case insensitive)', () => {
    const user = createMockUser({ role: 'Editor' });
    expect(isEditor(user)).toBe(true);
  });

  it('returns true for EDITOR role (uppercase)', () => {
    const user = createMockUser({ role: 'EDITOR' });
    expect(isEditor(user)).toBe(true);
  });

  it('returns false for non-editor and non-admin roles', () => {
    expect(isEditor(createMockUser({ role: 'user' }))).toBe(false);
    expect(isEditor(createMockUser({ role: 'viewer' }))).toBe(false);
    expect(isEditor(createMockUser({ role: 'guest' }))).toBe(false);
  });
});

describe('hasRole', () => {
  it('returns true when user has exact role', () => {
    const user = createMockUser({ role: 'moderator' });
    expect(hasRole(user, 'moderator')).toBe(true);
  });

  it('is case insensitive', () => {
    const user = createMockUser({ role: 'Moderator' });

    expect(hasRole(user, 'moderator')).toBe(true);
    expect(hasRole(user, 'MODERATOR')).toBe(true);
    expect(hasRole(user, 'Moderator')).toBe(true);
  });

  it('returns false when user does not have role', () => {
    const user = createMockUser({ role: 'user' });

    expect(hasRole(user, 'admin')).toBe(false);
    expect(hasRole(user, 'editor')).toBe(false);
    expect(hasRole(user, 'moderator')).toBe(false);
  });

  it('works with standard roles', () => {
    expect(hasRole(createMockUser({ role: 'admin' }), 'admin')).toBe(true);
    expect(hasRole(createMockUser({ role: 'editor' }), 'editor')).toBe(true);
    expect(hasRole(createMockUser({ role: 'user' }), 'user')).toBe(true);
  });
});

describe('hasAnyRole', () => {
  it('returns true when user has one of the specified roles', () => {
    const user = createMockUser({ role: 'editor' });
    expect(hasAnyRole(user, ['admin', 'editor', 'moderator'])).toBe(true);
  });

  it('returns true when user has first role in array', () => {
    const user = createMockUser({ role: 'admin' });
    expect(hasAnyRole(user, ['admin', 'editor'])).toBe(true);
  });

  it('returns true when user has last role in array', () => {
    const user = createMockUser({ role: 'moderator' });
    expect(hasAnyRole(user, ['admin', 'editor', 'moderator'])).toBe(true);
  });

  it('returns false when user has none of the specified roles', () => {
    const user = createMockUser({ role: 'guest' });
    expect(hasAnyRole(user, ['admin', 'editor', 'moderator'])).toBe(false);
  });

  it('is case insensitive', () => {
    const user = createMockUser({ role: 'Editor' });

    expect(hasAnyRole(user, ['admin', 'editor'])).toBe(true);
    expect(hasAnyRole(user, ['ADMIN', 'EDITOR'])).toBe(true);
    expect(hasAnyRole(user, ['Admin', 'Editor'])).toBe(true);
  });

  it('returns false for empty roles array', () => {
    const user = createMockUser({ role: 'admin' });
    expect(hasAnyRole(user, [])).toBe(false);
  });

  it('works with single role array', () => {
    const user = createMockUser({ role: 'admin' });

    expect(hasAnyRole(user, ['admin'])).toBe(true);
    expect(hasAnyRole(user, ['editor'])).toBe(false);
  });
});

describe('Role Authorization Integration', () => {
  it('admin passes all editor checks', () => {
    const admin = createMockUser({ role: 'admin' });

    expect(isAdmin(admin)).toBe(true);
    expect(isEditor(admin)).toBe(true);
    expect(hasRole(admin, 'admin')).toBe(true);
    expect(hasAnyRole(admin, ['admin', 'editor'])).toBe(true);
  });

  it('editor passes editor checks but not admin', () => {
    const editor = createMockUser({ role: 'editor' });

    expect(isAdmin(editor)).toBe(false);
    expect(isEditor(editor)).toBe(true);
    expect(hasRole(editor, 'editor')).toBe(true);
    expect(hasAnyRole(editor, ['admin', 'editor'])).toBe(true);
  });

  it('regular user fails admin and editor checks', () => {
    const user = createMockUser({ role: 'user' });

    expect(isAdmin(user)).toBe(false);
    expect(isEditor(user)).toBe(false);
    expect(hasRole(user, 'user')).toBe(true);
    expect(hasAnyRole(user, ['admin', 'editor'])).toBe(false);
    expect(hasAnyRole(user, ['user'])).toBe(true);
  });
});
