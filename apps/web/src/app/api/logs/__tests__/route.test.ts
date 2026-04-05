import { NextRequest } from 'next/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// Mock process.env
vi.stubEnv('LOKI_URL', 'http://loki:3100');

describe('GET /api/logs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns 401 when meepleai_user_role cookie is missing', async () => {
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs');
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns 401 when role is not admin', async () => {
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=user' },
    });
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it('returns lokiUnavailable when LOKI_URL is not configured', async () => {
    vi.stubEnv('LOKI_URL', '');
    vi.resetModules();
    const { GET } = await import('../route');
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    const body = await res.json() as { lokiUnavailable: boolean; entries: unknown[] };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
    expect(body.entries).toHaveLength(0);
    vi.stubEnv('LOKI_URL', 'http://loki:3100');
  });

  it('returns lokiUnavailable when Loki fetch fails', async () => {
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockRejectedValueOnce(new Error('connect ECONNREFUSED'));
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    const body = await res.json() as { lokiUnavailable: boolean };
    expect(res.status).toBe(200);
    expect(body.lokiUnavailable).toBe(true);
  });

  it('returns normalized entries sorted descending by timestamp', async () => {
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
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=admin' },
    });
    const res = await GET(req);
    expect(res.status).toBe(200);
    const body = await res.json() as { entries: Array<{ container: string; level: string }> };
    expect(body.entries).toHaveLength(2);
    expect(body.entries[0].container).toBe('meepleai-api');
    // Sort descending: WARNING (più recente) è entries[0], ERROR (più vecchio) è entries[1]
    expect(body.entries[0].level).toBe('warning');
    expect(body.entries[1].level).toBe('error');
  });

  it('returns 200 when role is superadmin', async () => {
    vi.resetModules();
    const { GET } = await import('../route');
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'success', data: { resultType: 'streams', result: [] } }),
    });
    const req = new NextRequest('http://localhost/api/logs', {
      headers: { cookie: 'meepleai_user_role=superadmin' },
    });
    const res = await GET(req);
    expect(res.status).toBe(200);
  });
});
