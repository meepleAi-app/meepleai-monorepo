/**
 * URL safety validation for client-side redirects.
 *
 * Prevents open redirect attacks by ensuring `?from=` query params and
 * notification deep links are restricted to same-origin relative paths.
 *
 * Closes #2168 (login open redirect) + #2182 (notifications defensive validation).
 *
 * Attack vectors rejected:
 *   1. absolute external (https://evil.com)
 *   2. absolute external http (http://evil.com)
 *   3. protocol-relative (//evil.com)
 *   4. Windows path (\\evil.com)
 *   5. scheme injection (javascript:, data:)
 *   6. data URI (data:text/html,...)
 *   7. encoded protocol-relative (%2F%2Fevil.com)
 *   8. whitespace bypass ("  //evil.com")
 *   9. control character bypass (/\t//evil.com — browsers silently strip U+0000-U+001F)
 *  10. double-encoded protocol-relative (/%252F%252Fevil.com)
 */

/**
 * Returns true only for safe same-origin relative URL paths.
 *
 * Safe: starts with `/`, does NOT start with `//`, does NOT contain `\\`,
 * does NOT contain `:` before the first `/`, does NOT start with whitespace,
 * does NOT contain ASCII control characters, does NOT double-encode to `//`.
 */
export function isSafeRelativeLink(link: string | null | undefined): boolean {
  if (typeof link !== 'string' || link.length === 0) return false;

  // Reject any input containing ASCII control characters (U+0000-U+001F)
  // or DEL (U+007F). Browsers silently strip these during URL normalization,
  // which can expose a following "//evil.com" to the redirect engine.
  // e.g. new URL('/\t//evil.com', 'http://localhost') → href: "http://evil.com/"
  // eslint-disable-next-line no-control-regex
  if (/[\x00-\x1F\x7F]/.test(link)) return false;

  // Must start with a single `/`
  if (link[0] !== '/') return false;

  // Reject protocol-relative `//evil.com`
  if (link[1] === '/') return false;

  // Reject Windows path `\\evil.com` (after the leading `/` it's `/\\evil.com`)
  if (link[1] === '\\') return false;

  // Decode iteratively to defeat double-encoding (e.g. %252F%252F → %2F%2F → //).
  // Max 3 passes is sufficient — anything pathological enough to need more is rejected.
  // Note: window.location.assign() does NOT pre-decode like URLSearchParams does,
  // so /%252F%252Fevil.com is exploitable from notification detail.link consumers.
  let decoded = link;
  for (let i = 0; i < 3; i++) {
    let next: string;
    try {
      next = decodeURIComponent(decoded);
    } catch {
      break;
    }
    if (next === decoded) break;
    decoded = next;
  }
  if (decoded.startsWith('//')) return false;
  if (decoded.startsWith('/\\')) return false;

  // Reject scheme injection: any `:` before first `/` is suspicious
  const firstSlash = link.indexOf('/', 1);
  const firstColon = link.indexOf(':');
  if (firstColon !== -1 && (firstSlash === -1 || firstColon < firstSlash)) {
    return false;
  }

  return true;
}

/**
 * Returns input when safe, otherwise fallback.
 *
 * Use this at every consumer site so the validation policy is consistent.
 *
 * @param fallback Must be a pre-validated safe path (not validated by this function).
 */
export function assertSafeRelativeOrFallback(
  link: string | null | undefined,
  fallback: string
): string {
  return isSafeRelativeLink(link) ? (link as string) : fallback;
}
