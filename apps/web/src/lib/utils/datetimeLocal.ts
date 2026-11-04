/**
 * The value of an `<input type="datetime-local">` for a stored instant (#4773).
 *
 * That input has no zone: its string is a wall clock, and `new Date("2026-11-07T21:00")` reads it in the zone of
 * the browser. A stored instant therefore has to be written with the *local* fields of its `Date`.
 * `toISOString().slice(0, 16)` writes the UTC ones instead, and the round trip then moves the time by the
 * browser's UTC offset every time the form is saved.
 *
 * One hour a year cannot be written back faithfully: when the clocks go back, the repeated hour exists twice
 * and the input carries no offset to tell the two apart, so the string reads as the first occurrence. A night
 * at 02:30 on that morning would move by an hour; nobody schedules one there. The reverse hour does not exist
 * at all when the clocks go forward: a wall clock typed inside it reads as the hour after.
 *
 * Returns `''` for a missing or invalid instant, which the input shows as empty.
 */
export function isoToDatetimeLocalValue(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n: number): string => n.toString().padStart(2, '0');
  // Four digits for the year, as the input requires: typing «2026» digit by digit passes through 0002-….
  const year = date.getFullYear().toString().padStart(4, '0');
  return `${year}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * The instant, as an ISO string, that the value of an `<input type="datetime-local">` names (#4840), or `null`
 * when the value is empty or is not a date.
 *
 * The input lets the user make up a string the browser's own parser does not read: a year of more than four digits
 * is accepted, and `new Date('12345-10-30T20:00')` is an invalid `Date` whose `toISOString()` throws `RangeError`.
 * A caller that converts the raw value therefore throws in its handler (or, if it keeps the `Date`, in a later
 * render). The value is read as a wall clock in the zone of the browser, the inverse of `isoToDatetimeLocalValue`.
 */
export function datetimeLocalValueToIso(value: string | null | undefined): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
