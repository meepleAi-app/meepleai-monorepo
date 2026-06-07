/**
 * CSP header builder — used by `next.config.js` `headers()` callback.
 *
 * Lives in a separate CommonJS module (not under `src/`) so that:
 *   1. `next.config.js` can `require()` it without TS tooling, and
 *   2. Vitest can import it from a `.test.ts` to assert the per-env contract.
 *
 * #1816 P2-3 — staging deploys hide `/manifest.json` behind a Cloudflare Access
 * gateway. When the user's CF Access cookie expires the browser is redirected
 * to `https://meepleai-staging.cloudflareaccess.com/...`, which the default
 * `manifest-src 'self'` directive blocks (audit § P2 CSP manifest). The fix
 * is to opt-in `https://*.cloudflareaccess.com` to `manifest-src` **only**
 * on the staging build — prod CSP must NOT widen to the CF Access subdomain.
 *
 * Opt-in flag: build-time env var `NEXT_PUBLIC_CSP_ALLOW_CF_ACCESS=true`.
 * Default (unset / any other value) keeps the prod-safe `manifest-src 'self'`.
 */

'use strict';

/**
 * Build the `Content-Security-Policy` header value.
 *
 * @param {object} opts
 * @param {string} opts.apiBaseUrl - URL allowed by `connect-src` (XHR/fetch target)
 * @param {boolean} [opts.allowCfAccess=false] - When true, widen `manifest-src`
 *   to include `https://*.cloudflareaccess.com` (staging-only opt-in for the
 *   PWA manifest behind Cloudflare Access gateway).
 * @returns {string} The full CSP header value (semicolon-separated directives).
 */
function buildCspHeader(opts) {
  const apiBaseUrl = opts.apiBaseUrl;
  const allowCfAccess = opts.allowCfAccess === true;

  const manifestSources = ["'self'"];
  if (allowCfAccess) {
    // `https://*.cloudflareaccess.com` covers any tenant subdomain CF Access
    // assigns (e.g. `meepleai-staging.cloudflareaccess.com`). Wildcard is the
    // minimum the CF Access redirect flow requires.
    manifestSources.push('https://*.cloudflareaccess.com');
  }

  return [
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https:",
    "font-src 'self' data:",
    `manifest-src ${manifestSources.join(' ')}`,
    `connect-src 'self' ${apiBaseUrl}`,
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join('; ');
}

/**
 * Resolve the opt-in flag from a build-time env var. Parses defensively — only
 * the string literal "true" enables the wider manifest-src; anything else
 * (missing, "false", "1", "yes", typos) stays on the prod-safe default.
 *
 * @param {string | undefined} envValue
 * @returns {boolean}
 */
function isCfAccessAllowed(envValue) {
  return envValue === 'true';
}

module.exports = {
  buildCspHeader,
  isCfAccessAllowed,
};
