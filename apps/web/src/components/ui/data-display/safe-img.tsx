'use client';

/**
 * #5072 — the one `<img>` of the app. Every other `<img>` with a variable `src` goes through it
 * (`local/no-unguarded-img`).
 *
 * The BGG asset ban (#2123, ADR-059 §5) is enforced for `next/image` by the custom loader, but a plain `<img>`, an
 * `unoptimized` `<Image>` and a CSS background skip it. `SafeImg` applies the same rule to the `<img>`: a URL that a
 * browser would send to a BGG host is not rendered, the SLO beacon fires, and `fallback` takes its place. Anything
 * else passes unchanged, including local previews (`blob:`, `data:`) and paths on this origin.
 *
 * `allowBggHost` is the written exception of #5072: the admin BGG search panels show BGG's thumbnails so the admin
 * can tell the results apart. The ESLint rule accepts it only under `src/app/admin/` and `src/components/admin/`.
 */

import { useEffect, type ComponentPropsWithRef, type JSX, type ReactNode } from 'react';

import { isBlockedImageHost } from '@/lib/games/cover-utils';
import { reportBggAttempt } from '@/lib/images/bgg-attempt-beacon';

export type SafeImgProps = Omit<ComponentPropsWithRef<'img'>, 'src'> & {
  src: string | null | undefined;
  /** Rendered instead of the image when `src` is missing or names a BGG host. */
  fallback?: ReactNode;
  /** Admin BGG search only (#5072, ADR-059 §5.6). */
  allowBggHost?: boolean;
};

/**
 * True when `src` must not reach the browser. Fires the SLO beacon once per blocked `src`, as the `next/image`
 * loader does.
 */
export function useBlockedImageSrc(src: string | null | undefined, allowBggHost = false): boolean {
  const blocked = !allowBggHost && isBlockedImageHost(src);

  useEffect(() => {
    if (blocked && src) reportBggAttempt(src);
  }, [blocked, src]);

  return blocked;
}

export function SafeImg({
  src,
  fallback = null,
  allowBggHost = false,
  alt,
  ...props
}: SafeImgProps): JSX.Element {
  const blocked = useBlockedImageSrc(src, allowBggHost);

  if (!src?.trim() || blocked) return <>{fallback}</>;

  // eslint-disable-next-line @next/next/no-img-element, local/no-unguarded-img -- the guarded <img> itself (#5072)
  return <img src={src} alt={alt} {...props} />;
}
