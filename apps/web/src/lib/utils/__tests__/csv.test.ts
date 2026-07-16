import { describe, it, expect } from 'vitest';
import { escapeCSVField, rowsToCsv } from '../csv';

describe('escapeCSVField', () => {
  it('quotes fields containing comma, quote or newline', () => {
    expect(escapeCSVField('a,b')).toBe('"a,b"');
    expect(escapeCSVField('he said "hi"')).toBe('"he said ""hi"""');
    expect(escapeCSVField('line1\nline2')).toBe('"line1\nline2"');
  });
  it('passes through plain fields and stringifies numbers/null', () => {
    expect(escapeCSVField('plain')).toBe('plain');
    expect(escapeCSVField(42)).toBe('42');
    expect(escapeCSVField(-5)).toBe('-5');
    expect(escapeCSVField(null)).toBe('');
  });

  it('quotes fields containing a bare carriage return', () => {
    expect(escapeCSVField('a\rb')).toBe('"a\rb"');
  });

  it('prefixes formula-injection-prone strings with a single quote', () => {
    expect(escapeCSVField('=1+1')).toBe("'=1+1");
    expect(escapeCSVField('@cmd')).toBe("'@cmd");
    expect(escapeCSVField('+x')).toBe("'+x");
    expect(escapeCSVField('-x')).toBe("'-x");
  });

  it('does not treat a numeric -5 as formula-injection-prone', () => {
    // A genuine numeric cell must not be corrupted by the string-only guard.
    expect(escapeCSVField(-5)).toBe('-5');
  });
});

describe('rowsToCsv', () => {
  it('joins headers + rows with CRLF and escapes each cell', () => {
    const csv = rowsToCsv(
      ['A', 'B'],
      [
        ['x', 'y,z'],
        [1, null],
      ]
    );
    expect(csv).toBe('A,B\r\nx,"y,z"\r\n1,');
  });
});
