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
 * @param {boolean} [opts.allowLocalBlobImages=false] - When true, widen `img-src`
 *   to include `http://localhost:9000` (E2E-only opt-in for MinIO presigned covers).
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

  // #3498 — the cover R2-strict E2E job serves presigned covers from a MinIO
  // container published on http://localhost:9000. The default `img-src` allows
  // only `https:` for remote hosts, so the browser blocks the image and the
  // real-load assertion (naturalWidth > 0) can never pass. Opt-in ONLY from the
  // E2E workflow's web.env.dev — prod/staging keep the closed default.
  const imgSources = ["'self'", 'data:', 'https:'];
  if (opts.allowLocalBlobImages === true) {
    imgSources.push('http://localhost:9000');
  }

  return [
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline'",
    `img-src ${imgSources.join(' ')}`,
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

/**
 * Resolve the #3498 E2E opt-in from a build-time env var. Same defensive parse
 * as isCfAccessAllowed — only the literal "true" widens img-src.
 *
 * @param {string | undefined} envValue
 * @returns {boolean}
 */
function isLocalBlobAllowed(envValue) {
  return envValue === 'true';
}

module.exports = {
  buildCspHeader,
  isCfAccessAllowed,
  isLocalBlobAllowed,
};
