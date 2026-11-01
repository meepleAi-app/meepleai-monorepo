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

/**
 * Reads what was typed into the code field. Returns the code as the server wants it, or `null` while
 * it is not (yet) one of the two shapes.
 */
export function parseTwoFactorCode(input: string): TwoFactorCode | null {
  const compact = input.replace(INVISIBLE, '').replace(DASHES, '-').replace(/\s+/g, '');

  if (TOTP.test(compact)) return { kind: 'totp', value: compact };

  const backup = BACKUP.exec(compact);
  if (backup) return { kind: 'backup', value: `${backup[1]}-${backup[2]}`.toUpperCase() };

  return null;
}
