/**
 * The IANA zone of the browser, e.g. `Europe/Rome` (#4754).
 *
 * A game night's `scheduledAt` is an instant: it says when, not where on the clock. The server keeps the zone
 * the organizer scheduled in, so the invitation email can say 21:00 where the organizer meant 21:00 instead of
 * the UTC reading.
 *
 * Returns `undefined` — and the caller leaves the field out — whenever the zone cannot be read or does not look
 * like an IANA id. That is deliberate: the server refuses a malformed zone with a 422, and a value this helper
 * hands over must never be the reason a night fails to be created. The shape is the one the server's validator
 * checks (`GameNightTimeZoneRules`: a letter first, then letters, digits, `_`, `+`, `-`, in at most four
 * segments split by `/`, 64 characters at most); keep the two in step.
 */
const SEGMENT_SHAPE = /^[A-Za-z0-9_+-]+$/;
const STARTS_WITH_LETTER = /^[A-Za-z]/;
const MAX_LENGTH = 64;
const MAX_SEGMENTS = 4;

export function getBrowserTimeZone(): string | undefined {
  try {
    const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (typeof zone !== 'string' || zone.length > MAX_LENGTH || !STARTS_WITH_LETTER.test(zone)) {
      return undefined;
    }
    const segments = zone.split('/');
    if (segments.length > MAX_SEGMENTS) return undefined;
    return segments.every(segment => SEGMENT_SHAPE.test(segment)) ? zone : undefined;
  } catch {
    return undefined;
  }
}
