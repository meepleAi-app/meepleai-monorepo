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
const BACKUP = /^([A-Z0-9]{4})-?([A-Z0-9]{4})$/;

/**
 * Reads what was typed into the code field. Returns the code as the server wants it, or `null` while
 * it is not (yet) one of the two shapes.
 */
export function parseTwoFactorCode(input: string): TwoFactorCode | null {
  const compact = input.toUpperCase().replace(/\s+/g, '');

  if (TOTP.test(compact)) return { kind: 'totp', value: compact };

  const backup = BACKUP.exec(compact);
  if (backup) return { kind: 'backup', value: `${backup[1]}-${backup[2]}` };

  return null;
}
