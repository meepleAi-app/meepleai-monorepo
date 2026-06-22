/**
 * SSR-safe i18n helper.
 *
 * The client-side `IntlProvider` is only mounted on the client (see
 * `apps/web/src/app/providers.tsx`). Components reached during SSR —
 * server components, `generateMetadata` exports, the global 404, etc. —
 * cannot use `useTranslation()` without producing React error #418 on
 * hydration: SSR renders the raw message-id keys while the client
 * renders the translated strings.
 *
 * This helper resolves localized strings statically from the IT
 * locale catalogue and is safe to call in any server context.
 *
 * @example
 * ```ts
 * const NOT_FOUND = getSsrMessages('pages.errors.notFound');
 * // NOT_FOUND.title === 'Pagina non trovata'
 *
 * const META = getSsrMessages('pages.sharedGameDetail.metadata');
 * ```
 *
 * The path argument is type-safe: typos and stale references fail
 * compilation. When SSR locale negotiation lands (Issue #1103), the
 * signature will gain an optional `locale` parameter and this helper
 * becomes the single switching point for every SSR callsite.
 *
 * @see Issue #1101 (helper extraction)
 * @see Issue #1076 (root cause)
 * @see Issue #1103 (future locale negotiation)
 */

import itMessages from '@/locales/it.json';

type Messages = typeof itMessages;

/**
 * Recursive dot-path keys over the message catalogue. Excludes leaves
 * (strings, arrays) so consumers can only ask for object subtrees;
 * leaf access happens via the returned object's property syntax, which
 * preserves better IDE support and reads more naturally at call sites.
 */
type DotPath<T, Prefix extends string = ''> =
  T extends Record<string, unknown>
    ? {
        [K in keyof T & string]:
          | (T[K] extends Record<string, unknown> ? `${Prefix}${K}` : never)
          | (T[K] extends Record<string, unknown> ? DotPath<T[K], `${Prefix}${K}.`> : never);
      }[keyof T & string]
    : never;

/** Resolve a dot-path against the catalogue and return the deeply-typed value. */
type Resolve<T, P extends string> = P extends `${infer Head}.${infer Rest}`
  ? Head extends keyof T
    ? Resolve<T[Head], Rest>
    : never
  : P extends keyof T
    ? T[P]
    : never;

/** All valid dot-paths that resolve to an object subtree of the catalogue. */
export type SsrMessagesPath = DotPath<Messages>;

/**
 * Returns the deeply-typed object subtree at `path` from the IT message
 * catalogue. Compile error on unknown paths.
 *
 * @throws {Error} if the runtime catalogue is missing a path that the
 * compiler thought existed. This indicates `it.json` was modified
 * without keeping the type system in sync — fix the catalogue, not the
 * call site.
 */
export function getSsrMessages<P extends SsrMessagesPath>(path: P): Resolve<Messages, P> {
  const segments = path.split('.');
  let cursor: unknown = itMessages;
  for (const segment of segments) {
    if (typeof cursor !== 'object' || cursor === null || !(segment in cursor)) {
      throw new Error(
        `getSsrMessages: path "${path}" does not exist in it.json (failed at segment "${segment}")`
      );
    }
    cursor = (cursor as Record<string, unknown>)[segment];
  }
  return cursor as Resolve<Messages, P>;
}
