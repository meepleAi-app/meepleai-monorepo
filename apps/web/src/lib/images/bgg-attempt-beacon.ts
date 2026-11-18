/**
 * Issue #2123 — the SLO=0 beacon for a blocked BGG asset render attempt.
 *
 * Each call fires a fire-and-forget beacon to `/api/metrics/bgg-attempt`, from
 * which the API increments the `meepleai_bgg_url_attempted_render_total`
 * Prometheus counter. Shared by the `next/image` loader (`safe-loader.ts`) and
 * `SafeImg` (#5072), so a blocked `<img>` counts like a blocked `<Image>`.
 *
 * Best-effort: uses `navigator.sendBeacon` when available (survives page
 * unload), falls back to `fetch` with `keepalive`, and no-ops on SSR.
 */

const MAX_BEACON_SRC_LEN = 512;

export function reportBggAttempt(src: string): void {
  if (typeof window === 'undefined') return;

  // Truncate before serialization to bound the on-wire payload size. The
  // backend route truncates again at 512 chars as defense-in-depth, but
  // capping here avoids sending multi-KB src strings over a beacon channel
  // (mitigates code-review followup #3 on PR #2125).
  const truncatedSrc =
    src.length > MAX_BEACON_SRC_LEN ? src.slice(0, MAX_BEACON_SRC_LEN) + '…' : src;
  const payload = JSON.stringify({
    src: truncatedSrc,
    path: window.location?.pathname ?? '',
    ts: Date.now(),
  });

  try {
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function') {
      const blob = new Blob([payload], { type: 'application/json' });
      navigator.sendBeacon('/api/metrics/bgg-attempt', blob);
      return;
    }
    void fetch('/api/metrics/bgg-attempt', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: payload,
      keepalive: true,
    }).catch(() => {
      /* swallow — metric reporting is best-effort */
    });
  } catch {
    /* swallow — never let a beacon failure break image rendering */
  }
}
