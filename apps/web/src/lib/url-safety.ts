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
 */

/**
 * Returns true only for safe same-origin relative URL paths.
 *
 * Safe: starts with `/`, does NOT start with `//`, does NOT contain `\\`,
 * does NOT contain `:` before the first `/`, does NOT start with whitespace.
 */
export function isSafeRelativeLink(link: string | null | undefined): boolean {
  if (typeof link !== 'string' || link.length === 0) return false;

  // No leading whitespace
  if (link[0] === ' ' || link[0] === '\t') return false;

  // Must start with a single `/`
  if (link[0] !== '/') return false;

  // Reject protocol-relative `//evil.com`
  if (link[1] === '/') return false;

  // Reject Windows path `\\evil.com` (after the leading `/` it's `/\\evil.com`)
  if (link[1] === '\\') return false;

  // Reject encoded protocol-relative `%2F%2F`
  const decoded = (() => {
    try {
      return decodeURIComponent(link);
    } catch {
      return link;
    }
  })();
  if (decoded.startsWith('//')) return false;
  if (decoded.startsWith('/\\\\')) return false;

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
 */
export function assertSafeRelativeOrFallback(
  link: string | null | undefined,
  fallback: string
): string {
  return isSafeRelativeLink(link) ? (link as string) : fallback;
}
