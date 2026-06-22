import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// C4: the route now derives the role from /auth/me via getServerUser instead
// of reading the (HMAC-protected post-deploy) meepleai_user_role cookie. The
// tests mock that helper directly to keep the route under test isolated from
// the auth round-trip.
const getServerUserMock = vi.fn();
vi.mock('@/lib/auth/server', async () => {
  const actual = await vi.importActual<typeof import('@/lib/auth/server')>('@/lib/auth/server');
  return {
    ...actual,
    getServerUser: getServerUserMock,
  };
});

// Mock process.env
vi.stubEnv('LOKI_URL', 'http://loki:3100');

type FakeUser = { id: string; email: string; role: string };

function setUser(role: string | null) {
  if (role === null) {
    getServerUserMock.mockResolvedValue(null);
  } else {
    const user: FakeUser = { id: 'u1', email: 'u1@test.local', role };
    getServerUserMock.mockResolvedValue(user);
  }
}

describe('GET /api/logs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.stubEnv('LOKI_URL', 'http://loki:3100');
  });

  it('returns 401 when no authenticated user', async () => {
    setUser(null);
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns 401 when role is not admin', async () => {
    setUser('user');
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns lokiUnavailable when LOKI_URL is not configured', async () => {
    setUser('admin');
    vi.stubEnv('LOKI_URL', '');
    vi.resetModules();
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    const body = (await res.json()) as { lokiUnavailable: boolean; entries: unknown[] };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
    expect(body.entries).toHaveLength(0);
  });

  it('returns lokiUnavailable when Loki fetch fails', async () => {
    setUser('admin');
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockRejectedValueOnce(new Error('connect ECONNREFUSED'));
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    const body = (await res.json()) as { lokiUnavailable: boolean };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
  });

  it('returns normalized entries sorted descending by timestamp', async () => {
    setUser('admin');
    vi.resetModules();
    const { GET } = await import('../route');
    const lokiResponse = {
      status: 'success',
      data: {
        resultType: 'streams',
        result: [
          {
            stream: { container_name: 'meepleai-api' },
            values: [
              // timestamp più vecchio → ERROR
              ['1712300000000000000', 'ERROR: database connection failed'],
              // timestamp più recente → WARNING (sarà entries[0] dopo sort desc)
              ['1712300001000000000', 'WARNING: high memory usage'],
            ],
          },
        ],
      },
    };
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => lokiResponse,
    });
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(200);
    const body = (await res.json()) as { entries: Array<{ container: string; level: string }> };
    expect(body.entries).toHaveLength(2);
    expect(body.entries[0].container).toBe('meepleai-api');
    // Sort descending: WARNING (più recente) è entries[0], ERROR (più vecchio) è entries[1]
    expect(body.entries[0].level).toBe('warning');
    expect(body.entries[1].level).toBe('error');
  });

  it('returns 200 when role is superadmin', async () => {
    setUser('superadmin');
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'success', data: { resultType: 'streams', result: [] } }),
    });
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(200);
  });
});
