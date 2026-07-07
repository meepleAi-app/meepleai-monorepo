/**
 * Issue #2123 — custom Next.js Image loader runtime guard.
 *
 * Next.js's default loader builds a `/_next/image?url=…` proxy URL using the
 * passed-in `src`. With our explicit allowlist in next.config.js, requests
 * for non-allowed hostnames already fail at the optimizer boundary. This
 * loader is the LAST runtime defense before that boundary:
 *
 *   1. Any `src` whose hostname is in the BGG block list (via
 *      `shouldUsePlaceholder` from cover-utils.ts) is rewritten to the
 *      `/placeholder.svg` static asset — guaranteeing zero browser request
 *      to BGG hosts even if a caller bypasses the `<Cover>` wrapper.
 *   2. Each blocked attempt fires a fire-and-forget beacon to
 *      `/api/metrics/bgg-attempt`, which the API increments the
 *      `meepleai_bgg_url_attempted_render_total` Prometheus counter from.
 *      SLO = 0; the FE never tolerates nonzero traffic here.
 *
 * Wired in next.config.js via `images.loader: 'custom'` +
 * `images.loaderFile: './src/lib/images/safe-loader.ts'`.
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md §6.3
 *   ADR : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md §5
 */

import { isBlockedImageHost, shouldUsePlaceholder } from '@/lib/games/cover-utils';

interface LoaderArgs {
  src: string;
  width: number;
  quality?: number;
}

const PLACEHOLDER_PATH = '/placeholder.svg';

/**
 * Best-effort beacon. Uses `navigator.sendBeacon` when available (most modern
 * browsers; survives page unload), falls back to `fetch` with `keepalive` for
 * Safari/older browsers, and silently no-ops on SSR.
 */
const MAX_BEACON_SRC_LEN = 512;

function reportBggAttempt(src: string): void {
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

export default function safeImageLoader({ src, width, quality }: LoaderArgs): string {
  if (shouldUsePlaceholder(src)) {
    // Only fire the beacon for hosts in the BGG block list — not for the
    // null / empty / unparseable cases that `shouldUsePlaceholder` also
    // returns true for (those are not ToS issues, just missing-data states).
    //
    // `isBlockedImageHost` parses the URL and compares the hostname exactly
    // (issue #2655) — replacing an unanchored substring regex that mis-flagged
    // any src merely *containing* a BGG token (js/regex/missing-regexp-anchor).
    if (isBlockedImageHost(src)) {
      reportBggAttempt(src);
    }
    return PLACEHOLDER_PATH;
  }

  // Pass through to the standard Next.js optimizer with the same query-string
  // contract the default loader uses. The `images.remotePatterns` allowlist in
  // next.config.js continues to gate which upstreams are legitimately fetched.
  const q = Math.min(Math.max(Math.round(quality ?? 75), 1), 100);
  const separator = src.includes('?') ? '&' : '?';
  return `${src}${separator}w=${width}&q=${q}`;
}
