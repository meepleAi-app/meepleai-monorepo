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
 * Prometheus counter `meepleai_bgg_url_attempted_render_total_attempts_total{path}`
 * — il nome ESPOSTO, con l'unit `attempts` appesa dall'esportatore OTel. Il nome breve
 * `meepleai_bgg_url_attempted_render_total` e' quello dell'istrumento C# e non e' mai stato
 * esposto: una regola che lo interrogasse sarebbe cieca, ed e' il caso che
 * `infra/scripts/verify-alert-metric-names.sh` blocca.
 * If the backend is unreachable, the beacon is dropped — better a missed
 * signal than a noisy 5xx spinning the FE.
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md §6.3 AC-11
 *   ADR : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md §5
 */

import { NextRequest, NextResponse } from 'next/server';

import { getApiBase } from '@/lib/api/core/httpClient';

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

  // 🔴 Qui c'era `BACKEND_URL ?? NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:8080'`, e
  // **nessuna delle due variabili e' definita da nessuna parte nel repo**: non in `.env.local`,
  // non in `.env.development.example`, non in uno dei cinque compose, non in un workflow.
  // Quindi si ricadeva sempre su `http://localhost:8080`, che dentro il container `web` e' il
  // container stesso — dove nessuno ascolta sulla 8080. Il `catch` qui sotto e' muto per
  // costruzione («beacon is best-effort») e la rotta risponde 204 comunque, quindi il beacon
  // sparisce senza una riga di log.
  //
  // Perche' conta piu' di un beacon perso: questa rotta e' l'unico percorso del contatore
  // `meepleai_bgg_url_attempted_render_total_attempts_total`, SLO=0, su cui
  // `infra/prometheus/alerts/bgg-tos-compliance.yml` alza un **P1** di conformita' ToS (#2123,
  // ADR-059 §5). L'alert esiste, usa il nome esposto corretto e ha unit test promtool; e
  // `safe-loader.ts` manda davvero il beacon qui, con `navigator.sendBeacon`. Era la catena in
  // mezzo a non arrivare: il rilevatore di un freeze attivo era cieco.
  //
  // Misurato con due chiamate dallo stesso istante e `path` distinti, poi contando le serie su
  // `:8080/metrics`: quella diretta a `:8080/api/v1/metrics/bgg-attempt` crea la sua serie,
  // quella passata da questa rotta **no**. Dopo la correzione entrambe la creano.
  //
  // `getApiBase()` e' la funzione giusta proprio perche' qui siamo server-side: ritorna
  // `API_BASE_URL` (`http://api:8080`, l'hostname della rete Docker), che e' definita in tutti
  // e cinque i compose. La sua forma browser — stringa vuota — non si applica a un route
  // handler.
  const url = `${getApiBase().replace(/\/+$/, '')}/api/v1/metrics/bgg-attempt`;

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
