/**
 * ESLint Custom Rule: no-store-scores-direct
 *
 * Issue #2389 Block A.7 — store API hygiene post-polymorphic-scoring.
 *
 * **Problem:**
 * After #2389 Block A (polymorphic ScoreType + SignalR `ScoringConfigured`),
 * `useLiveSessionStore`'s `scores` field is a legacy back-compat slice that
 * holds the derived legacy Map<userId,number>. New consumers SHOULD read
 * either:
 *   - `scoreData` for the polymorphic payload (single source of truth), or
 *   - the derived `scores` returned by `useSessionScores()` (memoized hook).
 *
 * Reading `useLiveSessionStore(s => s.scores)` directly bypasses the hook's
 * derivation logic and bakes in the assumption that `scores` is always a
 * useful structure — which will no longer hold once polymorphic types
 * (BinaryWin / Objectives / Ranking) become the default.
 *
 * **Solution:**
 * For new code, swap to `useSessionScores()` (returns `{ scores, scoringType,
 * scoreData }`) or read `s.scoreData` directly when only the polymorphic
 * payload is needed.
 *
 * **Severity:**
 * Registered at `warn` (not `error`) in eslint.config.mjs so the legacy
 * `ScoreBoard.tsx` consumer keeps CI green while flagging new regressions.
 *
 * **References:**
 * - Issue #2389 Block A
 * - apps/web/src/hooks/useSessionScores.ts (canonical replacement)
 */

'use strict';

module.exports = {
  meta: {
    type: 'suggestion',
    docs: {
      description:
        'Disallow new direct reads of `useLiveSessionStore(s => s.scores)`. Use the derived `scores` from `useSessionScores()` or read `scoreData` directly. Tracked in #2389 Block A.',
      category: 'Best Practices',
      recommended: false,
    },
    messages: {
      noScoresDirect:
        '`useLiveSessionStore(s => s.scores)` is deprecated (#2389 Block A). Use `useSessionScores()` for the derived legacy map, or read `s.scoreData` for the polymorphic payload.',
    },
    schema: [],
  },

  create(context) {
    return {
      CallExpression(node) {
        if (
          node.callee.type !== 'Identifier' ||
          node.callee.name !== 'useLiveSessionStore' ||
          node.arguments.length !== 1
        ) {
          return;
        }

        const selector = node.arguments[0];
        if (selector.type !== 'ArrowFunctionExpression') return;

        const body = selector.body;
        if (
          body.type === 'MemberExpression' &&
          body.property.type === 'Identifier' &&
          body.property.name === 'scores'
        ) {
          context.report({ node: body, messageId: 'noScoresDirect' });
        }
      },
    };
  },
};
