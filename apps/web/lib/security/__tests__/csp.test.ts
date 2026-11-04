/**
 * CSP header builder — #1816 P2-3 per-env staging-only opt-in.
 *
 * Asserts:
 *   1. Prod default: `manifest-src 'self'` (no CF Access subdomain)
 *   2. Staging opt-in: `manifest-src 'self' https://*.cloudflareaccess.com`
 *   3. `apiBaseUrl` is reflected verbatim in `connect-src`
 *   4. `isCfAccessAllowed` only enables on literal "true" (defensive)
 *
 * Audit ref: docs/for-developers/audits/2026-06-02-mobile-golden-path-audit.md
 * § P2 CSP manifest. Security note (Nygard): prod CSP must NEVER include
 * `cloudflareaccess.com`. The regression guards below enforce that contract.
 */

import { describe, it, expect } from 'vitest';

// CommonJS import — csp.js is consumed by next.config.js which is CJS.
import { buildCspHeader, isCfAccessAllowed, isLocalBlobAllowed, toWebSocketOrigin } from '../csp';

const PROD_API = 'https://api.meepleai.app';
const STAGING_API = 'https://api.meepleai-staging.cloudflareaccess.com';

describe('buildCspHeader — #1816 P2-3 per-env CSP manifest-src', () => {
  describe('prod default (allowCfAccess=false)', () => {
    it("emits `manifest-src 'self'` without cloudflareaccess.com", () => {
      const csp = buildCspHeader({ apiBaseUrl: PROD_API, allowCfAccess: false });

      expect(csp).toContain("manifest-src 'self'");
      // 🔴 Security regression guard — prod CSP must NOT widen to CF Access.
      expect(csp).not.toContain('cloudflareaccess.com');
    });

    it('reflects the prod apiBaseUrl in connect-src', () => {
      const csp = buildCspHeader({ apiBaseUrl: PROD_API, allowCfAccess: false });
      expect(csp).toContain(`connect-src 'self' ${PROD_API}`);
    });

    it('treats `allowCfAccess` undefined as false (prod-safe default)', () => {
      const csp = buildCspHeader({ apiBaseUrl: PROD_API });

      expect(csp).toContain("manifest-src 'self'");
      expect(csp).not.toContain('cloudflareaccess.com');
    });
  });

  describe('staging opt-in (allowCfAccess=true)', () => {
    it('widens `manifest-src` to include `https://*.cloudflareaccess.com`', () => {
      const csp = buildCspHeader({ apiBaseUrl: STAGING_API, allowCfAccess: true });

      expect(csp).toContain("manifest-src 'self' https://*.cloudflareaccess.com");
    });

    it('keeps the rest of the directive set unchanged', () => {
      const csp = buildCspHeader({ apiBaseUrl: STAGING_API, allowCfAccess: true });

      // Stable invariants that prod + staging share.
      expect(csp).toContain("default-src 'self'");
      expect(csp).toContain("script-src 'self' 'unsafe-inline'");
      expect(csp).toContain("frame-ancestors 'none'");
      expect(csp).toContain("base-uri 'self'");
      expect(csp).toContain("form-action 'self'");
    });
  });

  describe('directive ordering & format', () => {
    it('emits semicolon-separated directives (one per line equivalent)', () => {
      const csp = buildCspHeader({ apiBaseUrl: PROD_API, allowCfAccess: false });

      // Each top-level directive must be present.
      const directives = csp.split('; ');
      expect(directives).toEqual(
        expect.arrayContaining([
          "default-src 'self'",
          "script-src 'self' 'unsafe-inline'",
          "style-src 'self' 'unsafe-inline'",
          "img-src 'self' data: https:",
          "font-src 'self' data:",
          "manifest-src 'self'",
          `connect-src 'self' ${PROD_API} wss://api.meepleai.app`,
          "frame-ancestors 'none'",
          "base-uri 'self'",
          "form-action 'self'",
        ])
      );
    });
  });
});

describe('buildCspHeader — #3498 E2E-only img-src opt-in', () => {
  it('keeps img-src closed by default (no localhost blob host)', () => {
    const csp = buildCspHeader({ apiBaseUrl: PROD_API });

    expect(csp).toContain("img-src 'self' data: https:");
    // 🔴 Security regression guard — prod CSP must NOT allow an http loopback origin.
    expect(csp).not.toContain('http://localhost:9000');
  });

  it('widens img-src to the MinIO presign host when opted in', () => {
    const csp = buildCspHeader({ apiBaseUrl: PROD_API, allowLocalBlobImages: true });

    expect(csp).toContain("img-src 'self' data: https: http://localhost:9000");
  });

  it('leaves every other directive untouched when opted in', () => {
    const csp = buildCspHeader({ apiBaseUrl: PROD_API, allowLocalBlobImages: true });

    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("manifest-src 'self'");
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).not.toContain('cloudflareaccess.com');
  });
});

describe('isLocalBlobAllowed — defensive env var parsing', () => {
  it.each([
    ['true', true],
    ['false', false],
    ['1', false],
    ['yes', false],
    ['TRUE', false], // case-sensitive on purpose — opt-in must be explicit "true"
    ['', false],
    [undefined, false],
  ])('isLocalBlobAllowed(%j) === %s', (input, expected) => {
    expect(isLocalBlobAllowed(input as string | undefined)).toBe(expected);
  });
});

describe('isCfAccessAllowed — defensive env var parsing', () => {
  it.each([
    ['true', true],
    ['false', false],
    ['1', false],
    ['yes', false],
    ['TRUE', false], // case-sensitive on purpose — opt-in must be explicit "true"
    ['', false],
    [undefined, false],
  ])('isCfAccessAllowed(%j) === %s', (input, expected) => {
    expect(isCfAccessAllowed(input as string | undefined)).toBe(expected);
  });
});

// ──────────────────────────────────────────────────────────────────────────────
// #4059 — l'origine WebSocket in connect-src
// ──────────────────────────────────────────────────────────────────────────────

describe('buildCspHeader — #4059 origine WebSocket in connect-src', () => {
  /**
   * Il difetto che questi test fissano: `connect-src 'self' http://localhost:8080` NON
   * autorizza `ws://localhost:8080`, perche' per la CSP i due schemi sono distinti. Il
   * trasporto WebSockets di SignalR veniva bloccato DOPO un negotiate riuscito — quindi
   * `/sessions/{id}/live` restava senza eventi, con il solo sintomo di un `setConnected(false)`.
   *
   * Misurato nel browser prima della correzione:
   *   Connecting to 'ws://localhost:8080/hubs/gamestate?id=...' violates the following
   *   Content Security Policy directive: "connect-src 'self' http://api:8080 ..."
   */
  const connectSrcOf = (csp: string) =>
    csp.split('; ').find(d => d.startsWith('connect-src')) ?? '';

  it('accosta wss:// a un apiBaseUrl https', () => {
    const csp = buildCspHeader({ apiBaseUrl: 'https://api.meepleai.app' });
    expect(connectSrcOf(csp)).toBe(
      "connect-src 'self' https://api.meepleai.app wss://api.meepleai.app"
    );
  });

  it('accosta ws:// a un apiBaseUrl http (dev)', () => {
    const csp = buildCspHeader({ apiBaseUrl: 'http://localhost:8080' });
    expect(connectSrcOf(csp)).toBe("connect-src 'self' http://localhost:8080 ws://localhost:8080");
  });

  it('NON declassa https a ws: un wss resta wss', () => {
    // Il controllo che impedisce la forma piu' probabile dell'errore: una sostituzione
    // `http -> ws` applicata senza distinguere lo schema produrrebbe `ws://api.meepleai.app`
    // su un sito HTTPS, che il browser blocca come mixed content.
    const csp = buildCspHeader({ apiBaseUrl: 'https://api.meepleai.app' });
    expect(connectSrcOf(csp)).not.toContain('ws://api.meepleai.app');
  });

  it("non aggiunge nulla per un apiBaseUrl che non e' http(s)", () => {
    const csp = buildCspHeader({ apiBaseUrl: "'self'" });
    expect(connectSrcOf(csp)).toBe("connect-src 'self' 'self'");
  });

  it('toWebSocketOrigin: mappa i due schemi e rifiuta il resto', () => {
    expect(toWebSocketOrigin('http://localhost:8080')).toBe('ws://localhost:8080');
    expect(toWebSocketOrigin('https://api.example.test')).toBe('wss://api.example.test');
    expect(toWebSocketOrigin('ws://already')).toBeNull();
    expect(toWebSocketOrigin("'self'")).toBeNull();
    expect(toWebSocketOrigin('')).toBeNull();
    expect(toWebSocketOrigin(undefined as unknown as string)).toBeNull();
  });
});
