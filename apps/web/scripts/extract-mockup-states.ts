/**
 * WS-D Foundation: parser estrae stati canonici dichiarati nei commenti
 * HTML dei mockup (`admin-mockups/design_files/*.html`).
 *
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 * Issue: #1070 (umbrella #1066)
 */

/**
 * Extract state names from a single comment text block.
 * Supports two declaration styles:
 *  - Inline: `Stati:  default · loading · error · not-found`
 *  - Multi-line: `Stati:` on first line, indented `<name> (description)` lines after
 *
 * Returns empty array if no `Stati:` line found.
 */
export function extractStatesFromComment(commentText: string): string[] {
  // Match "Stati:" + everything until next field header or end of comment
  const match = commentText.match(
    /Stati:\s*([\s\S]+?)(?=\n\s*(?:Route|Persona|Scope|Mockup|Vincoli|Sorgente|Phase|CSS|Note)[:\s]|$)/
  );
  if (!match) return [];

  const block = match[1];
  const firstLine = block.split('\n')[0].trim();

  // Inline form: contains a known separator on first line
  if (/[·|,]/.test(firstLine)) {
    return firstLine
      .split(/[·|,]/)
      .map(s => s.trim())
      .filter(Boolean);
  }

  // Multi-line form: each non-blank line's first token is a state name.
  // Filter lines that don't start with a word char to skip description
  // continuation lines (e.g. lines starting with `(` or whitespace-only).
  return block
    .split('\n')
    .map(l => l.trim())
    .filter(Boolean)
    .filter(line => /^[a-zA-Z]/.test(line))
    .map(line => line.split(/\s+/)[0])
    .filter(Boolean);
}
