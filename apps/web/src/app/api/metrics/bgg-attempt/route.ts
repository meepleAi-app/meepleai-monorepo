/**
 * Issue #2123 — POST /api/metrics/bgg-attempt
 *
 * Fire-and-forget beacon endpoint receiving notifications from the custom
 * Next.js Image loader (`lib/images/safe-loader.ts`) when a browser tried
 * to render an image whose hostname matches the BGG block list. The loader
 * redirected to the placeholder, but the attempt itself MUST be observed
 * (SLO=0; any nonzero rate is a P1 incident).
 *
 * Posture:
 *   - Method: POST only. GET / HEAD / OPTIONS return 405.
 *   - Auth  : NONE. Anonymous users hitting a public BGG-violating route
 *             must still surface the metric — auth filtering would mask
 *             the most common surface (search engines, signed-out users).
 *   - Body  : `{ src: string, path: string, ts: number }`. Truncated /
 *             validated before forwarding. Malformed payloads silently
 *             absorb a single increment with `path="unknown"` rather
 *             than reject — never give a metric writer the ability to
 *             suppress the signal by sending garbage.
 *   - Output: 204 No Content always. The browser must not retry on error.
 *
 * The endpoint hands the increment to the backend metrics service via a
 * simple POST to `/api/v1/metrics/bgg-attempt`, which increments the
 * Prometheus counter `meepleai_bgg_url_attempted_render_total{path}`.
 * If the backend is unreachable, the beacon is dropped — better a missed
 * signal than a noisy 5xx spinning the FE.
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md §6.3 AC-11
 *   ADR : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md §5
 */

import { NextRequest, NextResponse } from 'next/server';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const MAX_FIELD_LEN = 512;
const BACKEND_TIMEOUT_MS = 1500;

interface BeaconPayload {
  src?: string;
  path?: string;
  ts?: number;
}

function truncate(value: unknown): string {
  if (typeof value !== 'string') return '';
  return value.length > MAX_FIELD_LEN ? value.slice(0, MAX_FIELD_LEN) + '…' : value;
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  let payload: BeaconPayload = {};
  try {
    payload = (await request.json()) as BeaconPayload;
  } catch {
    /* malformed JSON — proceed with empty payload so we still record */
  }

  const safe = {
    src: truncate(payload.src),
    path: truncate(payload.path) || 'unknown',
    ts: typeof payload.ts === 'number' && Number.isFinite(payload.ts) ? payload.ts : Date.now(),
  };

  const backend =
    process.env.BACKEND_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:8080';
  const url = `${backend.replace(/\/+$/, '')}/api/v1/metrics/bgg-attempt`;

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), BACKEND_TIMEOUT_MS);
  try {
    await fetch(url, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(safe),
      signal: controller.signal,
      // No credentials — this endpoint is anonymous by design.
      cache: 'no-store',
    });
  } catch {
    /* swallow — beacon is best-effort, never bubble */
  } finally {
    clearTimeout(timer);
  }

  return new NextResponse(null, { status: 204 });
}

export async function GET(): Promise<NextResponse> {
  return new NextResponse(null, { status: 405, headers: { Allow: 'POST' } });
}
