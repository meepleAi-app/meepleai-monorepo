/**
 * WS-F.1 — Mockup ownership header parser.
 *
 * Extracts the structured `@key value` block from a mockup HTML file:
 *
 *     <!--
 *       @route /library/{gameId}
 *       @last-verified 2026-05-12
 *       @verified-by maintainer
 *       @status canonical
 *     -->
 *
 * Tolerates multiple comments and freeform commentary (e.g. Nanolith-style
 * `Mockup:`/`Route:`/`Scope:` blocks) — the parser picks the comment that
 * contains a `@route` line and parses the `@`-prefixed fields from it.
 *
 * Refs: #1072 (WS-F), #1066 (umbrella), AC-F.5.
 */

export const VERIFIED_BY_VALUES = [
  'designer',
  'maintainer',
  'spec-panel-review',
  'auto-bootstrap',
] as const;
export type VerifiedBy = (typeof VERIFIED_BY_VALUES)[number];

export const STATUS_VALUES = [
  'canonical',
  'verified',
  'drifted',
  'pending-implementation',
  'archived',
] as const;
export type Status = (typeof STATUS_VALUES)[number];

export interface MockupHeader {
  /** First entry in `@route` list (for single-route ergonomics). */
  route: string;
  /** All `@route` values (space-separated → array). Always non-empty. */
  routes: string[];
  /** ISO date YYYY-MM-DD. */
  lastVerified: string;
  verifiedBy: VerifiedBy;
  status: Status;
}

export type ParseResult = { ok: true; header: MockupHeader } | { ok: false; error: string };

const COMMENT_RE = /<!--([\s\S]*?)-->/g;
const FIELD_RE = /@([a-z][a-z-]*)\s+([^@\n]+?)(?=\s*(?:@[a-z]|$))/gi;
const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export function parseMockupHeader(html: string): ParseResult {
  const candidates: string[] = [];
  for (const match of html.matchAll(COMMENT_RE)) {
    if (match[1].includes('@route')) candidates.push(match[1]);
  }
  if (candidates.length === 0) {
    return { ok: false, error: 'No HTML comment containing @route found (AC-F.2)' };
  }
  // Use the first comment that contains @route; in practice each mockup has one
  // dedicated ownership block.
  const body = candidates[0];

  const fields = new Map<string, string>();
  for (const m of body.matchAll(FIELD_RE)) {
    fields.set(m[1].toLowerCase(), m[2].trim());
  }

  const route = fields.get('route');
  if (!route) return { ok: false, error: 'Missing @route' };
  const routes = route.split(/\s+/).filter(Boolean);
  if (routes.length === 0) return { ok: false, error: 'Empty @route' };
  for (const r of routes) {
    if (!r.startsWith('/')) {
      return { ok: false, error: `@route "${r}" must start with /` };
    }
  }

  const lastVerified = fields.get('last-verified');
  if (!lastVerified) return { ok: false, error: 'Missing @last-verified' };
  if (!ISO_DATE_RE.test(lastVerified)) {
    return { ok: false, error: `@last-verified "${lastVerified}" must be YYYY-MM-DD` };
  }

  const verifiedBy = fields.get('verified-by');
  if (!verifiedBy) return { ok: false, error: 'Missing @verified-by' };
  if (!VERIFIED_BY_VALUES.includes(verifiedBy as VerifiedBy)) {
    return {
      ok: false,
      error: `@verified-by "${verifiedBy}" not in enum ${VERIFIED_BY_VALUES.join('|')}`,
    };
  }

  const status = fields.get('status');
  if (!status) return { ok: false, error: 'Missing @status' };
  if (!STATUS_VALUES.includes(status as Status)) {
    return {
      ok: false,
      error: `@status "${status}" not in enum ${STATUS_VALUES.join('|')}`,
    };
  }

  return {
    ok: true,
    header: {
      route: routes[0],
      routes,
      lastVerified,
      verifiedBy: verifiedBy as VerifiedBy,
      status: status as Status,
    },
  };
}
