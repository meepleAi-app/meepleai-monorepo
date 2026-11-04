// apps/web/src/lib/play-records/sendIfChanged.ts

/**
 * The value of a free-text field for an update request: `undefined` (not sent) when the user left it as it was
 * drawn, the typed text otherwise (#4798).
 *
 * The edit form is drawn with `record.notes ?? ''`, so a field stored as `null` comes back from an untouched
 * form as `''`. The server reads an absent field as «leave it» and any string, the empty one included, as a
 * value: sending the `''` rewrote the stored `null`. A field the user emptied still differs from its loaded
 * text, so it is sent as `''` and can be cleared.
 *
 * The comparison is on the trimmed text, because the server trims what it stores: `'gg '` over `'gg'`, or
 * spaces over `null`, would change nothing there. A field the form did not deliver (`undefined`) is not sent
 * either, as before: «absent» keeps meaning «leave it», and only a delivered `''` clears.
 */
export function sendIfChanged(
  typed: string | undefined,
  loaded: string | null | undefined
): string | undefined {
  if (typed === undefined) return undefined;
  return typed.trim() === (loaded ?? '').trim() ? undefined : typed;
}
