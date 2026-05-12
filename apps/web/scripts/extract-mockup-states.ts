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
  // Match "Stati:" + everything until next field header or end of comment.
  // Use `[ \t]*` (inline whitespace only) instead of `\s*` to PRESERVE the
  // newline + indentation of the first multi-line state declaration, so the
  // indent-depth heuristic below can distinguish state lines from
  // description continuations.
  const match = commentText.match(
    /Stati:[ \t]*([\s\S]+?)(?=\n\s*(?:Route|Persona|Scope|Mockup|Vincoli|Sorgente|Phase|CSS|Note)[:\s]|$)/
  );
  if (!match) return [];

  const block = match[1];
  // First non-blank line drives inline-vs-multiline detection
  const firstLine =
    block
      .split('\n')
      .find(l => l.trim().length > 0)
      ?.trim() ?? '';

  // Inline form: contains `·` (middot) or `|` separator on first line.
  // `,` is intentionally excluded — descriptions on multi-line declarations
  // often contain commas (e.g. `default (foo, bar)`) which would defeat
  // detection. `·` is the canonical separator in admin-mockups/.
  if (/[·|]/.test(firstLine)) {
    return firstLine
      .split(/[·|]/)
      .map(s => s.trim())
      .filter(Boolean);
  }

  // Multi-line form: state declarations have the *minimum* indentation among
  // non-blank lines in the block; description continuations are indented more
  // deeply (and often start with `(`). Distinguish by indent depth.
  const nonBlankLines = block.split('\n').filter(l => l.trim().length > 0);
  if (nonBlankLines.length === 0) return [];

  const indents = nonBlankLines.map(l => (l.match(/^(\s*)/)?.[1] ?? '').length);
  const minIndent = Math.min(...indents);

  return nonBlankLines
    .filter((_, i) => indents[i] === minIndent)
    .map(l => l.trim().split(/\s+/)[0])
    .filter(token => /^[a-zA-Z][\w-]*$/.test(token));
}

/**
 * Extract `Route:` value from comment text.
 * Returns null if not declared.
 */
export function extractRouteFromComment(commentText: string): string | null {
  const match = commentText.match(/Route:\s*(\S+)/);
  return match ? match[1] : null;
}
