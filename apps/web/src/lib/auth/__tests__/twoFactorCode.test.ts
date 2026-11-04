import { describe, expect, it } from 'vitest';

import { parseTwoFactorCode, typeTwoFactorCode } from '../twoFactorCode';

describe('parseTwoFactorCode (#4806)', () => {
  it.each([
    ['123456', 'totp', '123456'],
    [' 123456 ', 'totp', '123456'],
    ['123 456', 'totp', '123456'],
    ['ABCD-2345', 'backup', 'ABCD-2345'],
    // What a person types reading it off a sheet of paper: the server compares the code exactly as it
    // generated it, so each of these is a wrong code to it unless it is normalised here.
    ['abcd-2345', 'backup', 'ABCD-2345'],
    ['abcd 2345', 'backup', 'ABCD-2345'],
    ['ABCD2345', 'backup', 'ABCD-2345'],
    ['  ab cd - 23 45 ', 'backup', 'ABCD-2345'],
    // Pasted from a chat or a note app: a typographic dash for the hyphen, an invisible character at the end.
    ['ABCD\u20132345', 'backup', 'ABCD-2345'],
    ['ABCD\u22122345', 'backup', 'ABCD-2345'],
    ['123456\u200B', 'totp', '123456'],
    ['\uFEFFABCD-2345', 'backup', 'ABCD-2345'],
  ])('reads %j as a %s code and hands the server %j', (input, kind, value) => {
    expect(parseTwoFactorCode(input)).toStrictEqual({ kind, value });
  });

  it.each([
    [''],
    ['   '],
    ['12345'], // one digit short
    ['1234567'], // one digit too many
    ['ABCD-234'], // a backup code cut short
    ['ABCD-23456'], // and one too long
    ['ABC-12345'], // the dash in the wrong place
    ['ABCD_2345'], // not a dash
    ['AB!D-2345'], // a character that is no part of any code
    // Non-ASCII letters that `toUpperCase` would turn into something else: they are not a code.
    ['\u00dfbc2345'], // ß → SS
    ['\ufb01cd2345'], // ﬁ → FI
    ['\u017fBCD-2345'], // ſ → S
    ['\u0131BCD-2345'], // ı → I
  ])('is not a code yet: %j', input => {
    expect(parseTwoFactorCode(input)).toBeNull();
  });

  it('does not turn a six-digit TOTP into a backup code, nor the reverse', () => {
    expect(parseTwoFactorCode('123456')?.kind).toBe('totp');
    expect(parseTwoFactorCode('A23456')?.kind).toBeUndefined(); // six characters, not six digits
    expect(parseTwoFactorCode('1234-5678')?.kind).toBe('backup'); // eight characters of a backup code
  });
});

describe('typeTwoFactorCode (#4817)', () => {
  it.each([
    ['', ''],
    ['123456', '123456'],
    // A backup code is nine characters with the dash: the field must hold all of them.
    ['abcd-2345', 'ABCD-2345'],
    ['ABCD-2345', 'ABCD-2345'],
    // Spaces are dropped, the dash is not invented: `parseTwoFactorCode` adds it on submit.
    ['abcd 2345', 'ABCD2345'],
    ['  ab cd - 23 45 ', 'ABCD-2345'],
    // From a chat or a note app.
    ['ABCD\u20132345', 'ABCD-2345'],
    ['123456\u200b', '123456'],
    // Characters that are no part of any code are not kept, nor turned into one.
    ['abc!@#', 'ABC'],
    ['\u00dfbc', 'BC'],
    ['\u017fBCD-2345', 'BCD-2345'],
  ])('shows %j as %j', (input, shown) => {
    expect(typeTwoFactorCode(input)).toBe(shown);
  });

  it('does not cut what it cannot read: the person sees the whole value and can fix it', () => {
    // A cut at nine characters would drop the end of the code for good once a stray prefix took the slots.
    expect(typeTwoFactorCode('1. ABCD-2345')).toBe('1ABCD-2345');
    expect(typeTwoFactorCode('ABCD-23456789')).toBe('ABCD-23456789');
  });

  it('shows what parseTwoFactorCode reads: every shape it accepts survives typing', () => {
    for (const input of ['123456', 'abcd-2345', 'abcd 2345', 'ABCD2345', 'ABCD\u20132345 ']) {
      const parsed = parseTwoFactorCode(input);
      expect(parsed).not.toBeNull();
      expect(parseTwoFactorCode(typeTwoFactorCode(input))).toStrictEqual(parsed);
    }
  });
});
