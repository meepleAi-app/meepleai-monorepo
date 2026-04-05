import { NextRequest, NextResponse } from 'next/server';

import type { LokiLogEntry, LokiQueryRangeResponse, LogsApiResponse } from '@/lib/loki/types';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

const ADMIN_ROLES = new Set(['admin', 'superadmin']);
// container_name=~"meepleai-.*" scopea ai soli container MeepleAI (Fluent-bit label standard)
const LOKI_QUERY = '{container_name=~"meepleai-.*"} |~ "(?i)(error|warn|fatal)"';
const LIMIT = 100;
const LOOK_BACK_HOURS = 24;

function isAdminCookie(cookieHeader: string | null): boolean {
  if (!cookieHeader) return false;
  const match = cookieHeader.match(/meepleai_user_role=([^;]+)/);
  if (!match) return false;
  return ADMIN_ROLES.has(match[1].toLowerCase());
}

function detectLevel(line: string): LokiLogEntry['level'] {
  const lower = line.toLowerCase();
  if (lower.includes('fatal') || lower.includes('"level":"fatal"')) return 'error';
  if (lower.includes('error') || lower.includes('"level":"error"')) return 'error';
  if (lower.includes('warn') || lower.includes('"level":"warn"')) return 'warning';
  if (lower.includes('info') || lower.includes('"level":"info"')) return 'info';
  return 'unknown';
}

function nsToIso(ns: string): string {
  const ms = Math.floor(Number(ns) / 1_000_000);
  return new Date(ms).toISOString();
}

export async function GET(request: NextRequest): Promise<NextResponse<LogsApiResponse>> {
  const cookieHeader = request.headers.get('cookie');
  if (!isAdminCookie(cookieHeader)) {
    return NextResponse.json({ entries: [] }, { status: 401 });
  }

  const lokiUrl = process.env.LOKI_URL;
  if (!lokiUrl) {
    return NextResponse.json({ entries: [], lokiUnavailable: true });
  }

  const now = Date.now();
  const start = now - LOOK_BACK_HOURS * 60 * 60 * 1000;
  const params = new URLSearchParams({
    query: LOKI_QUERY,
    start: String(start * 1_000_000),
    end: String(now * 1_000_000),
    limit: String(LIMIT),
    direction: 'backward',
  });

  let lokiData: LokiQueryRangeResponse;
  try {
    const response = await fetch(`${lokiUrl}/loki/api/v1/query_range?${params.toString()}`, {
      headers: { 'Content-Type': 'application/json' },
      signal: AbortSignal.timeout(10_000),
    });
    if (!response.ok) {
      return NextResponse.json({ entries: [], lokiUnavailable: true });
    }
    lokiData = (await response.json()) as LokiQueryRangeResponse;
  } catch {
    return NextResponse.json({ entries: [], lokiUnavailable: true });
  }

  const entries: LokiLogEntry[] = [];
  const result = lokiData.data?.result ?? [];
  for (let si = 0; si < result.length; si++) {
    const stream = result[si];
    const container =
      stream.stream['container_name'] ??
      stream.stream['container'] ??
      stream.stream['job'] ??
      'unknown';
    for (let vi = 0; vi < (stream.values?.length ?? 0); vi++) {
      const [ts, line] = stream.values[vi];
      entries.push({
        id: `${si}-${vi}`,
        timestamp: nsToIso(ts),
        container,
        message: line,
        level: detectLevel(line),
      });
    }
  }

  entries.sort((a, b) => (a.timestamp > b.timestamp ? -1 : 1));

  return NextResponse.json({ entries });
}
