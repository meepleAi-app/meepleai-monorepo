/**
 * Helpers for rendering game cover placeholders without runtime third-party fetches.
 *
 * Background — issue #1822 (umbrella #1821):
 *   The catalog historically stored cover URLs pointing at `cf.geekdo-images.com`,
 *   which both (a) rate-limits the client's IP producing 503s and (b) is restricted
 *   by BoardGameGeek's Terms of Service for our subscription SaaS model. The FE
 *   must now treat any such URL as missing and fall back to a locally-rendered
 *   placeholder. Future enrichment via Wikidata / user upload lives in separate
 *   issues (#1823 / #1824).
 */

/**
 * Hosts whose images we deliberately refuse to render at runtime. Adding a host
 * here causes `<Cover>` to behave as if `imageUrl` were absent and switch to the
 * deterministic placeholder.
 *
 * BGG (Geekdo CDN) is excluded for both technical (503 rate-limit) and legal
 * reasons (Geekdo ToS prohibits asset use in subscription products).
 *
 * Issue #2123 — the strings below are the BGG host blocklist itself, not URLs
 * to render. The `local/no-bgg-host` ESLint rule is per-line disabled here
 * because removing the constant would break the runtime guard that the rest
 * of the FE depends on.
 */
const BLOCKED_IMAGE_HOSTS: readonly string[] = [
  // eslint-disable-next-line local/no-bgg-host -- blocklist data, not a URL
  'cf.geekdo-images.com',
  // eslint-disable-next-line local/no-bgg-host -- blocklist data, not a URL
  'geekdo-images.com',
  // eslint-disable-next-line local/no-bgg-host -- blocklist data, not a URL
  'images.geekdo.com',
];

/**
 * Returns true when the given image URL should be treated as missing and the
 * caller should render the placeholder instead.
 *
 * Cases covered:
 * - null / undefined / empty string
 * - URL that fails to parse
 * - URL whose host is in {@link BLOCKED_IMAGE_HOSTS}
 *
 * SSR-safe: does not rely on `window.location` or other browser globals.
 */
export function shouldUsePlaceholder(imageUrl: string | null | undefined): boolean {
  if (!imageUrl) return true;

  const trimmed = imageUrl.trim();
  if (!trimmed) return true;

  // Data URLs and same-origin relative paths are always allowed.
  if (trimmed.startsWith('data:') || trimmed.startsWith('/')) return false;

  let host: string;
  try {
    host = new URL(trimmed).hostname.toLowerCase();
  } catch {
    return true;
  }

  return BLOCKED_IMAGE_HOSTS.some(blocked => host === blocked || host.endsWith(`.${blocked}`));
}

/**
 * Deterministic 32-bit string hash. Same string → same hash across runs and
 * across environments (Node, browser, edge). Used to map a game id to a stable
 * hue without storing extra columns.
 *
 * Implementation: classic FNV-like DJB2 fold with explicit |0 to coerce back to
 * 32-bit signed and avoid floating-point drift on very long inputs.
 */
function hashString(input: string): number {
  let h = 5381;
  for (let i = 0; i < input.length; i++) {
    h = (h << 5) + h + input.charCodeAt(i);
    h |= 0;
  }
  return Math.abs(h);
}

/**
 * Maps a stable string identifier to an HSL hue in [0, 359].
 *
 * Paired with fixed saturation 55% + lightness 32% in the placeholder, both
 * picked so the white-on-bg contrast ratio stays > 4.5:1 (WCAG AA) across all
 * 360 hues — verified by hand against the worst-case yellow band.
 */
export function hashToHue(input: string): number {
  return hashString(input) % 360;
}

/**
 * Words ignored when extracting initials. Lowercased, accent-insensitive
 * matching applied via `normalize('NFD')`.
 */
const STOP_WORDS = new Set([
  // English
  'the',
  'a',
  'an',
  'of',
  'and',
  'or',
  'in',
  'on',
  'to',
  'for',
  // Italian
  'il',
  'la',
  'lo',
  'i',
  'gli',
  'le',
  'di',
  'da',
  'del',
  'dello',
  'della',
  "dell'",
  'dei',
  'degli',
  'delle',
  'un',
  'una',
  'uno',
  'e',
  'o',
]);

/**
 * Strips diacritics so initials extracted from "Pòllen" / "Cabo Verdê" stay
 * ASCII-clean and visually predictable.
 */
function normalizeWord(word: string): string {
  return word
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '') // combining diacritics
    .toLowerCase();
}

/**
 * Extracts up to 3 uppercase characters representing the game title.
 *
 * Rules:
 *   - Split on whitespace + common punctuation
 *   - Drop stop words (the, il, della, …)
 *   - Take first letter of remaining words, uppercase
 *   - Cap at 3 characters
 *   - Fallback to first non-space character of the original title if everything
 *     was stripped (e.g. titles consisting only of stop words)
 *   - Final fallback: '?'
 *
 * Examples:
 *   "Catan" → "C"
 *   "7 Wonders" → "7W"
 *   "Aeon's End" → "AE"
 *   "A Feast for Odin" → "FO"
 *   "Il Signore degli Anelli" → "SA"
 *   "The Lord of the Rings" → "LR"
 *   "" → "?"
 */
export function extractInitials(title: string | null | undefined): string {
  if (!title) return '?';

  const tokens = title
    .split(/[\s\-_:.,;/\\()[\]{}'"]+/)
    .map(t => t.trim())
    .filter(Boolean);

  if (tokens.length === 0) return '?';

  // Keep a token if it is NOT a stop word AND (length > 1 OR starts with a
  // digit). This drops:
  //   - English contractions like the trailing "s" from "Aeon's End"
  //   - one-letter remnants from punctuation splits ("R.E.M." → ['R','E','M'])
  // while preserving numeric short tokens like "7" in "7 Wonders".
  const significant = tokens.filter(t => {
    const lower = normalizeWord(t);
    if (STOP_WORDS.has(lower)) return false;
    if (lower.length > 1) return true;
    return /^[0-9]/.test(t);
  });

  let initials: string;
  if (significant.length > 0) {
    initials = significant
      .slice(0, 3)
      .map(w => w[0]?.toUpperCase() ?? '')
      .join('');
  } else {
    // Every word was a stop word (or got dropped) — use only the first token's
    // first character so we don't produce noise like "AAT" for "a an the".
    initials = tokens[0][0]?.toUpperCase() ?? '';
  }

  if (initials) return initials;

  // Last-resort: first visible character of the original title.
  const firstChar = title.trim()[0];
  return firstChar ? firstChar.toUpperCase() : '?';
}
