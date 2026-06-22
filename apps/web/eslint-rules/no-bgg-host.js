/**
 * ESLint Custom Rule: no-bgg-host
 *
 * Issue #2123 — BGG ToS compliance hard ban.
 *
 * **Problem:**
 * ADR-059 §5 forbids user-side asset traffic to BoardGameGeek hosts
 * (`cf.geekdo-images.com`, `geekdo-images.com`, `images.geekdo.com`,
 * `*.boardgamegeek.com`). The catalog enrichment pipeline (#1823 M3-M8)
 * provides self-hosted R2 covers; covers that fail to resolve fall back to a
 * deterministic placeholder via `lib/games/cover-utils.ts`.
 *
 * Re-introducing a BGG hostname literal in source code is therefore a
 * regression: the value would either be rendered directly to the browser
 * (ToS violation) or be silently demoted to a placeholder (which is the
 * graceful failure of `<Cover>` but masks an intent bug).
 *
 * **Solution:**
 * Always pass `SharedGameDto.coverUrl` (R2 presigned URL resolved server-side
 * by `CoverUrlResolver`) into `<Cover>` / `<Image>`, never a BGG URL.
 *
 * For the admin-only server-side pipeline that legitimately calls BGG XML API
 * (admin BGG fetcher, server-to-server only), use a per-line opt-out:
 *   // eslint-disable-next-line local/no-bgg-host
 *
 * **Scope:**
 * Applied to all source files under apps/web/src (ts, tsx, js, jsx).
 * Storybook fixtures (.stories.tsx) and tests get the rule too — a literal
 * in a test fixture would be misleading; replace with a MOCK_COVER_URL
 * constant.
 *
 * **References:**
 * - Issue #2123
 * - ADR-059 (Catalog Seed Legal Posture)
 * - docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md
 */

'use strict';

const BGG_PATTERN =
  /(cf\.geekdo-images\.com|geekdo-images\.com|images\.geekdo\.com|boardgamegeek\.com)/i;

function isOffender(value) {
  return typeof value === 'string' && BGG_PATTERN.test(value);
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Forbid BGG hostname literals in source code (ToS compliance, issue #2123)',
      category: 'Possible Errors',
      recommended: true,
    },
    messages: {
      bggHost:
        'BGG host literal "{{value}}" forbidden by ADR-059 §5 (ToS compliance, issue #2123). Use SharedGameDto.coverUrl (R2-resolved) instead. Admin-only server-to-server callers may opt out with `// eslint-disable-next-line local/no-bgg-host`.',
    },
    schema: [],
  },

  create(context) {
    function check(node, value) {
      if (!isOffender(value)) return;
      const truncated = value.length > 80 ? value.slice(0, 80) + '…' : value;
      context.report({ node, messageId: 'bggHost', data: { value: truncated } });
    }

    return {
      Literal(node) {
        check(node, node.value);
      },
      TemplateElement(node) {
        check(node, node.value && node.value.cooked);
      },
    };
  },
};
