/**
 * Parser for the "paste BGG mechanic tags" admin importer
 * (ADR-051 Sprint 2 / Task 18).
 *
 * Input format: each non-blank line is `Category<TAB>Name`, the same shape an
 * admin gets when copying the mechanic / theme block from a BoardGameGeek
 * game page. The parser is deliberately permissive — it never throws,
 * always returns `{ rows, errors }`, and reports errors per-line so the
 * dialog can show the live preview alongside red error chips.
 *
 * Dedup happens here on `(category, name)` (case-sensitive, Ordinal — same
 * comparison the server uses in `MechanicGoldenBggTagRepository`'s
 * `seenInBatch` HashSet) so the live preview count matches the server's
 * inserted count for the new-tags case. Duplicates against existing rows
 * remain the server's responsibility and surface via the
 * `BggImportResult.Skipped` count.
 */

export type BggTagRow = {
  readonly category: string;
  readonly name: string;
};

export type BggTsvParseResult = {
  readonly rows: BggTagRow[];
  readonly errors: string[];
};

/**
 * Parse a paste of TAB-separated `Category<TAB>Name` rows. Blank lines are
 * silently skipped; line numbers in error messages are 1-indexed and count
 * blank lines (so they line up with the textarea the user sees).
 */
export function parseBggTsv(input: string): BggTsvParseResult {
  const rows: BggTagRow[] = [];
  const errors: string[] = [];
  const seen = new Set<string>();

  // Normalize Windows CRLF and bare CR to LF before splitting so we don't
  // emit phantom errors on `\r`-terminated lines.
  const lines = input.replace(/\r\n?/g, '\n').split('\n');

  for (let i = 0; i < lines.length; i++) {
    const lineNumber = i + 1;
    const raw = lines[i];

    // Skip lines that are blank or whitespace-only — pasting a trailing
    // newline shouldn't show up as "Line N: expected TAB separator".
    if (raw.trim().length === 0) {
      continue;
    }

    const tabIndex = raw.indexOf('\t');
    if (tabIndex === -1) {
      errors.push(`Line ${lineNumber}: expected TAB separator`);
      continue;
    }

    const category = raw.slice(0, tabIndex).trim();
    const name = raw.slice(tabIndex + 1).trim();

    if (category.length === 0) {
      errors.push(`Line ${lineNumber}: category is required`);
      continue;
    }
    if (name.length === 0) {
      errors.push(`Line ${lineNumber}: name is required`);
      continue;
    }

    const dedupeKey = `${category}\u0000${name}`;
    if (seen.has(dedupeKey)) {
      // Silent in-paste dedupe; the live preview shows the unique count.
      continue;
    }
    seen.add(dedupeKey);
    rows.push({ category, name });
  }

  return { rows, errors };
}
