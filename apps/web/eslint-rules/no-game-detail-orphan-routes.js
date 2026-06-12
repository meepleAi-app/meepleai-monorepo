/**
 * ESLint Custom Rule: no-game-detail-orphan-routes
 *
 * Issue #2204 / ADR-061 — Game Detail Tab Inventory Canonical.
 *
 * **Problem:**
 * Three routes were shipped as "gap-fix" scaffolds (commits 0881856df + 54c504920
 * via #1411 nav-map analysis) but never wired into the tab nav UI:
 *   - /games/[id]/reviews
 *   - /games/[id]/strategies
 *   - /games/[id]/chat
 *
 * Their backend endpoints (`/api/v1/games/{id}/reviews` + `/strategies`)
 * returned 404. Spec-panel review (#2203) reached 7/7 consensus on Opzione B
 * (remove orphan routes) — locked in ADR-061.
 *
 * **Solution:**
 * Forbid string literals or template literals that re-introduce these paths.
 * If Reviews/Strategies/Chat are revived by a future product decision, the
 * restoration MUST be backed by a follow-up ADR explicitly superseding ADR-061
 * (proper BE endpoint design + mockup commission + E2E coverage), not by gap-fix
 * scaffolding.
 *
 * **Per-line opt-out** (rare — only for tests asserting the deletion, or for
 * a deliberate ADR-superseding restoration):
 *   // eslint-disable-next-line local/no-game-detail-orphan-routes
 *
 * **References:**
 * - Issue #2204 / #2203
 * - ADR-061 (Game Detail Tab Inventory Canonical)
 * - docs/superpowers/decisions/2026-06-12-2203-game-detail-tab-inventory.md
 */

'use strict';

// Match `/games/{id}/{reviews,strategies,chat}` (the routes ADR-061 deleted).
// Negative lookbehind excludes legitimate prefixes like `/library/games/...`
// (game detail INSIDE library context) and `/api/v1/games/...` API client paths,
// neither of which are the orphan UI routes the ADR forbids.
//
// Note: API client methods that still call removed BE endpoints (e.g.
// /api/v1/games/{id}/reviews) need to be addressed by removing the methods
// themselves, not by this lint — they're a different gap layered on top.
const ORPHAN_ROUTE_PATTERN =
  /(?<!\/library)(?<!\/api\/v\d+)\/games\/(\[id\]|\$\{[^}]+\}|[a-z0-9-]+)\/(reviews|strategies|chat)(\/|$|['"`])/i;

function isOffender(value) {
  return typeof value === 'string' && ORPHAN_ROUTE_PATTERN.test(value);
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Prevent re-scaffolding deleted orphan game-detail routes (ADR-061). If reviving, file a follow-up ADR.',
      recommended: true,
    },
    schema: [],
    messages: {
      forbidden:
        'Forbidden game-detail orphan route: "{{value}}". ADR-061 removed /games/[id]/{reviews,strategies,chat}. If reviving, file an ADR superseding ADR-061.',
    },
  },
  create(context) {
    return {
      Literal(node) {
        if (isOffender(node.value)) {
          context.report({
            node,
            messageId: 'forbidden',
            data: { value: String(node.value).slice(0, 80) },
          });
        }
      },
      TemplateLiteral(node) {
        const raw = context.sourceCode.getText(node);
        if (isOffender(raw)) {
          context.report({
            node,
            messageId: 'forbidden',
            data: { value: raw.slice(0, 80) },
          });
        }
      },
    };
  },
};
