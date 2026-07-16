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
    expect(escapeCSVField(null)).toBe('');
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
