/**
 * #5072 — the guarded way to put an image URL into CSS (`background`, `backgroundImage`).
 *
 * A URL from the API can name a BGG host (#2123, ADR-059 §5). `SafeImg` guards an `<img>`; a CSS background has no
 * element to guard, so the URL passes here first. A blocked or missing URL gives `undefined`: the caller renders what
 * it renders without an image. No beacon: this is a pure function, and the SLO counter is fed by `SafeImg` and the
 * `next/image` loader.
 *
 * The URL goes into a quoted CSS string, so `"` and `\` are escaped, and so are line breaks, which would end it.
 */

import { isBlockedImageHost } from '@/lib/games/cover-utils';

const CSS_STRING_ESCAPES: Readonly<Record<string, string>> = {
  '"': '\\"',
  '\\': '\\\\',
  '\n': '\\a ',
  '\r': '\\d ',
  '\f': '\\c ',
};

export function cssImageUrl(url: string | null | undefined): string | undefined {
  const trimmed = url?.trim();
  if (!trimmed || isBlockedImageHost(trimmed)) return undefined;

  return `url("${trimmed.replace(/["\\\n\r\f]/g, c => CSS_STRING_ESCAPES[c] ?? c)}")`;
}
