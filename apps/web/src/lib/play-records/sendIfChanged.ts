// apps/web/src/lib/play-records/sendIfChanged.ts

/**
 * The value of a free-text field for an update request: `undefined` (not sent) when the user left it as it was
 * drawn, the typed text otherwise (#4798).
 *
 * The edit form is drawn with `record.notes ?? ''`, so a field stored as `null` comes back from an untouched
 * form as `''`. The server reads an absent field as «leave it» and any string, the empty one included, as a
 * value: sending the `''` rewrote the stored `null`. A field the user emptied still differs from its loaded
 * text, so it is sent as `''` and can be cleared.
 */
export function sendIfChanged(
  typed: string | undefined,
  loaded: string | null | undefined
): string | undefined {
  const next = typed ?? '';
  return next === (loaded ?? '') ? undefined : next;
}
