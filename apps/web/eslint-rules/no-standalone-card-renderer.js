'use strict';

/**
 * ESLint Custom Rule: no-standalone-card-renderer (import-boundary)
 *
 * Issue #2858 (C1) — canonical card taxonomy guard.
 *
 * Forbids value-imports of meeple-card INTERNALS (parts/ and variants/) from
 * files outside ui/data-display/meeple-card/. Only the public `MeepleCard`
 * export (the dir root) is consumable, so a card cannot be re-assembled from
 * the atomic parts.
 *
 * NOT in this rule (by design):
 *   - name-based compose-check — infeasible: the codebase has 100+ generic
 *     `*Card` components unrelated to MeepleCard.
 *   - inline cover/stars/badge body-inspection — that is C4 (#2861).
 *
 * Exemptions:
 *   - files inside ui/data-display/meeple-card/ (the internals themselves)
 *   - test files (import internals to test them)
 *   - `import type` (type-only imports carry no rendering logic)
 *   - PATH_ALLOWLIST (audited value-util imports)
 */

const PART_IMPORT_RE = /\/meeple-card\/(?:parts|variants)\//;
const INSIDE_MEEPLE_CARD_RE = /\/ui\/data-display\/meeple-card\//;
const TEST_FILE_RE = /(?:\.test\.[jt]sx?$|\/__tests__\/)/;

// Audited value-util imports from parts/ that are NOT card re-assembly.
// Matched as a suffix of the (normalized) filename.
const PATH_ALLOWLIST = [
  // getKbPipColor: a pip-color utility that happens to live in parts/ManaPips.
  'src/hooks/queries/useGameManaPips.ts',
];

function normalize(p) {
  return p.replace(/\\/g, '/');
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Forbid value-imports of meeple-card internals (parts/, variants/) from outside the canonical dir. Compose the public <MeepleCard> instead. Issue #2858 (C1).',
      category: 'Best Practices',
      recommended: false,
    },
    messages: {
      deepImport:
        'Do not value-import meeple-card internals ("{{source}}"). Compose the public <MeepleCard> instead; only ui/data-display/meeple-card/ may reach into parts/ and variants/. If this is an audited utility import, add the file to PATH_ALLOWLIST in eslint-rules/no-standalone-card-renderer.js. (#2858)',
    },
    schema: [],
  },

  create(context) {
    const filename = normalize(context.getFilename());

    if (INSIDE_MEEPLE_CARD_RE.test(filename)) return {};
    if (TEST_FILE_RE.test(filename)) return {};
    if (PATH_ALLOWLIST.some(suffix => filename.endsWith(suffix))) return {};

    return {
      ImportDeclaration(node) {
        // Skip whole `import type { ... } from '...'`.
        if (node.importKind === 'type') return;

        const source = node.source.value;
        if (typeof source !== 'string') return;
        if (!PART_IMPORT_RE.test(normalize(source))) return;

        // Skip if EVERY specifier is `import { type Foo }` (inline type-only).
        const hasValueSpecifier = node.specifiers.some(
          spec => spec.type !== 'ImportSpecifier' || spec.importKind !== 'type'
        );
        if (!hasValueSpecifier) return;

        context.report({ node, messageId: 'deepImport', data: { source } });
      },
    };
  },
};
