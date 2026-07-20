import { describe, it, expect } from 'vitest';

import { createAdminMonitorClient } from '../adminMonitorClient';

// Minimal HttpClient stub — only `get` is exercised by getAlertHistory.
const makeHttp = (payload: unknown) =>
  ({
    get: async () => payload,
    post: async () => undefined,
    put: async () => undefined,
    patch: async () => undefined,
    delete: async () => undefined,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  }) as any;

describe('adminMonitorClient.getAlertHistory', () => {
  it('normalizes lowercase API severities to canonical Title-Case without throwing (#3231)', async () => {
    // The backend contract (alerts.schemas.ts) emits lowercase 'critical'/'warning'/'error'/'info';
    // the client previously used a strict PascalCase z.enum().parse() that threw on every real alert.
    const client = createAdminMonitorClient(
      makeHttp(
        ['critical', 'error', 'warning', 'info', 'nonsense'].map((severity, i) => ({
          id: String(i),
          alertType: 'x',
          severity,
          message: 'm',
          metadata: null,
          triggeredAt: 't',
          resolvedAt: null,
          isActive: true,
          channelSent: null,
        }))
      )
    );

    const out = await client.getAlertHistory();

    // error → Critical (the AlertHistoryTab badge has no Error case); unknown → Info.
    expect(out.map(a => a.severity)).toEqual(['Critical', 'Critical', 'Warning', 'Info', 'Info']);
  });
});
