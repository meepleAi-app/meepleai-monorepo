/**
 * Tests for the BGG TSV paste parser (ADR-051 Sprint 2 / Task 18).
 *
 * Spec by example: an admin operator copies the "Mechanism" / "Theme" tag block
 * from a BoardGameGeek game page (Edit → tag table) and pastes it into the
 * importer dialog. Each line is `Category<TAB>Name`. The parser must:
 *  - return `{ rows, errors }` (never throw),
 *  - tolerate trailing whitespace + Windows CRLF endings,
 *  - skip blank lines silently,
 *  - report a per-line error for any line that does not contain a TAB,
 *  - report a per-line error for empty `Category` or `Name` after trimming,
 *  - dedupe within the parsed rows on `(Category, Name)` so the live preview
 *    in the dialog can show a clean count (the repository will dedupe again
 *    against existing rows server-side, but that's not the parser's job).
 */

import { describe, it, expect } from 'vitest';
import { parseBggTsv, type BggTagRow } from '../bgg-tsv';

describe('parseBggTsv', () => {
  it('parses 3 well-formed TAB-separated rows', () => {
    const input = [
      'Mechanism\tRole Selection',
      'Mechanism\tVariable Phase Order',
      'Theme\tEconomic',
    ].join('\n');

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual<BggTagRow[]>([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Mechanism', name: 'Variable Phase Order' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  it('reports a per-line error when the TAB separator is missing', () => {
    const input = 'Mechanism|Role Selection';

    const result = parseBggTsv(input);

    expect(result.rows).toEqual([]);
    expect(result.errors).toEqual(['Line 1: expected TAB separator']);
  });

  it('returns the valid rows alongside per-line errors (mixed input)', () => {
    const input = ['Mechanism\tRole Selection', 'broken-line-no-tab', 'Theme\tEconomic'].join('\n');

    const result = parseBggTsv(input);

    expect(result.rows).toEqual<BggTagRow[]>([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Theme', name: 'Economic' },
    ]);
    expect(result.errors).toEqual(['Line 2: expected TAB separator']);
  });

  it('skips blank lines silently (no error reported)', () => {
    const input = ['Mechanism\tRole Selection', '', '   ', 'Theme\tEconomic'].join('\n');

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual<BggTagRow[]>([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  it('trims whitespace inside category and name', () => {
    const input = '  Mechanism  \t  Role Selection  ';

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual<BggTagRow[]>([{ category: 'Mechanism', name: 'Role Selection' }]);
  });

  it('handles Windows CRLF line endings', () => {
    const input = 'Mechanism\tRole Selection\r\nTheme\tEconomic\r\n';

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual<BggTagRow[]>([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  it('reports an error when the category is empty after trimming', () => {
    const input = '\tRole Selection';

    const result = parseBggTsv(input);

    expect(result.rows).toEqual([]);
    expect(result.errors).toEqual(['Line 1: category is required']);
  });

  it('reports an error when the name is empty after trimming', () => {
    const input = 'Mechanism\t   ';

    const result = parseBggTsv(input);

    expect(result.rows).toEqual([]);
    expect(result.errors).toEqual(['Line 1: name is required']);
  });

  it('dedupes within the parsed rows on (category, name)', () => {
    const input = [
      'Mechanism\tRole Selection',
      'Mechanism\tRole Selection', // exact duplicate within the paste
      'Theme\tEconomic',
    ].join('\n');

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toEqual<BggTagRow[]>([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  it('treats categories case-sensitively for dedup (Mechanism != mechanism)', () => {
    // Server uses StringComparer.Ordinal for the in-batch HashSet, so the
    // parser mirrors that — no surprise normalization that would mask
    // typos before the user sees them in the live preview.
    const input = ['Mechanism\tRole Selection', 'mechanism\tRole Selection'].join('\n');

    const result = parseBggTsv(input);

    expect(result.errors).toEqual([]);
    expect(result.rows).toHaveLength(2);
  });

  it('returns empty rows + empty errors for an empty paste', () => {
    expect(parseBggTsv('')).toEqual({ rows: [], errors: [] });
    expect(parseBggTsv('   \n\n   ')).toEqual({ rows: [], errors: [] });
  });

  it('reports multiple errors on the same paste with stable line numbers', () => {
    const input = ['Mechanism\tRole Selection', 'broken1', '', 'broken2'].join('\n');

    const result = parseBggTsv(input);

    expect(result.rows).toEqual<BggTagRow[]>([{ category: 'Mechanism', name: 'Role Selection' }]);
    expect(result.errors).toEqual([
      'Line 2: expected TAB separator',
      'Line 4: expected TAB separator',
    ]);
  });
});
