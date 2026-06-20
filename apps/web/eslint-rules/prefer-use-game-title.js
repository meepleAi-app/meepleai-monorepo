/**
 * ESLint Custom Rule: prefer-use-game-title
 *
 * Issue #2339 sub-PR 2/3 — encourages adoption of localization-aware title
 * rendering. WARN-only in this PR; promote to ERROR in a follow-up PR after
 * 14gg of trajectory verde on main-dev (per DEC-FE-8).
 *
 * **Problem:**
 * Direct `game.title` JSX rendering bypasses the locale + source priority chain
 * documented in `docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`.
 * The canonical EN title is correct fallback, but localized users (IT, etc.)
 * miss the translation surfaced by the BE pipeline (PR #2370).
 *
 * **Solution:**
 * Use `useGameTitle(game)` from `@/lib/i18n/use-game-title` to resolve the
 * best-fit title for the viewer locale, then render `value` in JSX.
 *
 * **Allow-list:**
 * - Lines containing `useGameTitle` (heuristic — migration in progress)
 * - `aria-label` JSX attribute values (DEC-FE-9 references canonical EN intentionally
 *   inside the i18n placeholder for `common.localizedFromEnglish`)
 * - Files under `__tests__/`, `.stories.*`, `.test.*` (test fixtures)
 * - Files under `apps/web/src/lib/i18n/` (the hook itself uses `game.title`)
 *
 * **Scope:**
 * Applied to all source files under apps/web/src (ts, tsx).
 *
 * **References:**
 * - Issue #2339
 * - docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md
 * - DEC-FE-8 (warn-mode lock)
 */

'use strict';

module.exports = {
  meta: {
    type: 'suggestion',
    docs: {
      description: 'Prefer useGameTitle() hook over raw game.title in JSX',
      category: 'Best Practices',
      recommended: false,
    },
    schema: [],
    messages: {
      preferHook:
        'Use `useGameTitle(game)` instead of `game.title` directly in JSX. The hook resolves locale + source priority. See docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md.',
    },
  },

  create(context) {
    const filename = context.filename || context.getFilename();

    // Allow-list: skip test files, stories, and the i18n hook itself
    if (
      filename.includes('__tests__') ||
      filename.includes('.stories.') ||
      filename.includes('.test.') ||
      filename.includes('/lib/i18n/') ||
      filename.includes('\\lib\\i18n\\')
    ) {
      return {};
    }

    return {
      'JSXExpressionContainer MemberExpression[object.name="game"][property.name="title"]'(node) {
        const sourceCode = context.sourceCode || context.getSourceCode();
        const lineText = sourceCode.lines[node.loc.start.line - 1];
        if (lineText && lineText.includes('useGameTitle')) return;

        let parent = node.parent;
        while (parent && parent.type !== 'JSXAttribute') parent = parent.parent;
        if (parent && parent.name && parent.name.name === 'aria-label') return;

        context.report({ node, messageId: 'preferHook' });
      },
    };
  },
};
