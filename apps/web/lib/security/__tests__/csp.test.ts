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
import { buildCspHeader, isCfAccessAllowed } from '../csp';

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
          `connect-src 'self' ${PROD_API}`,
          "frame-ancestors 'none'",
          "base-uri 'self'",
          "form-action 'self'",
        ])
      );
    });
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
