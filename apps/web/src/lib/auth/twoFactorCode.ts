/**
 * The code that proves a second factor is one of two things (#4806):
 *
 * - the 6 digits the authenticator app shows, or
 * - one of the backup codes, `XXXX-XXXX` — the way back in for someone who lost the phone.
 *
 * The server tells them apart by trying the first and then the second, and compares a backup code
 * EXACTLY as it was generated (uppercase, with the dash): `AbCd 2345` or `ABCD2345` is a wrong code
 * to it. So a person typing what they read off a sheet of paper — lowercase, a space instead of the
 * dash, no dash at all — is helped here, before it becomes a refusal.
 *
 * The characters of a backup code are not checked against the server's alphabet (it leaves out
 * the look-alikes O, I, 0, 1 today): if that changes, this must not turn a valid code away.
 */

export type TwoFactorCode =
  | { readonly kind: 'totp'; readonly value: string }
  | { readonly kind: 'backup'; readonly value: string };

const TOTP = /^\d{6}$/;
// Case-insensitive WITHOUT the `u` flag on purpose: in that mode `ſ` (long s), `ı` (dotless i) and the
// Kelvin sign do not match `[a-z]`, so no non-ASCII letter can pass for one and be turned into another by
// `toUpperCase` (`ß` → `SS`, `ﬁ` → `FI`) into a code the person never typed.
const BACKUP = /^([a-z0-9]{4})-?([a-z0-9]{4})$/i;

// What pasting from a chat, a word processor or a note app brings along: zero-width characters and soft
// hyphens that are invisible, and typographic dashes in place of the hyphen.
const INVISIBLE = /[\u200B-\u200F\u2060\u00AD\uFEFF]/g;
const DASHES = /[\u2010-\u2015\u2212]/g;

function compactCode(input: string): string {
  return input.replace(INVISIBLE, '').replace(DASHES, '-').replace(/\s+/g, '');
}

const NOT_PART_OF_A_CODE = /[^0-9A-Za-z-]/g;

/**
 * What the code field shows while the person types or pastes (#4817): the same clean-up as
 * {@link parseTwoFactorCode} (invisible characters gone, typographic dashes made into the hyphen, spaces
 * dropped), then only the characters a code is made of, in capitals.
 *
 * It does NOT put the dash in: someone typing `ABCD2345` is not corrected under their fingers, and
 * `parseTwoFactorCode` adds the dash when the code is sent. It does NOT cut the value to the nine characters of
 * `XXXX-XXXX` either: a pasted `1. ABCD-2345` is shown whole so the person can see what is wrong and delete the
 * `1`, where a cut would drop the end of the code for good. The field's `maxLength` bounds what can be pasted.
 * The filter runs before `toUpperCase`, so no non-ASCII letter can reach it (`ß` → `SS`) and be shown as a
 * code the person never typed.
 */
export function typeTwoFactorCode(input: string): string {
  return compactCode(input).replace(NOT_PART_OF_A_CODE, '').toUpperCase();
}

/**
 * Reads what was typed into the code field. Returns the code as the server wants it, or `null` while
 * it is not (yet) one of the two shapes.
 */
export function parseTwoFactorCode(input: string): TwoFactorCode | null {
  const compact = compactCode(input);

  if (TOTP.test(compact)) return { kind: 'totp', value: compact };

  const backup = BACKUP.exec(compact);
  if (backup) return { kind: 'backup', value: `${backup[1]}-${backup[2]}`.toUpperCase() };

  return null;
}
