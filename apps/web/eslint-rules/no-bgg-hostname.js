/**
 * ESLint rule: no BoardGameGeek (BGG) hostnames in source.
 *
 * ADR-1903 (BGG ToS, Issue #2123): user-side BGG access is forbidden. The
 * browser must never GET an asset from `cf.geekdo-images.com`, any
 * `*.boardgamegeek.com` host, or any other BGG-owned domain. The backend may
 * still call the BGG XML API server-to-server (admin-only flow under
 * `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProvider.cs`),
 * but that path never touches `apps/web/`.
 *
 * This rule statically flags any string literal — `"..."`, `'...'`, or
 * `` `...` `` — anywhere under `apps/web/src/` that mentions one of the
 * forbidden hostnames. It is the **fail-closed gate** for #2123 because:
 *
 *   1. The Next.js `images.remotePatterns` allowlist still has a trailing
 *      wildcard `**` (kept for badge / arbitrary CDN images until #2123 can
 *      replace it with an explicit allowlist), so dropping the explicit
 *      `cf.geekdo-images.com` entry from `next.config.js` is NOT sufficient
 *      on its own — a literal BGG URL would still pass.
 *   2. Even outside `<Image>`, anywhere a raw `<img src>` or a `fetch()` is
 *      pointed at a BGG host, the user's browser performs the GET and the
 *      ToS violation is committed.
 *
 * To suppress in the rare legitimate case (e.g. a comment quoting a URL for
 * documentation purposes, or admin-only code where the policy is genuinely
 * different):
 *
 *   // eslint-disable-next-line local/no-bgg-hostname -- <reason>
 *
 * Refs:
 *   - ADR-1903 (BGG legal constraint)
 *   - Issue #2123 (manifest + whitelist sweep)
 *   - Epic #1823 (Wikidata / Wikimedia / R2 replacement pipeline)
 */

'use strict';

const FORBIDDEN_HOSTNAME_PATTERN =
  /(?:^|[^a-z0-9])(?:cf\.geekdo-images\.com|geekdo-images\.com|[a-z0-9-]+\.boardgamegeek\.com|boardgamegeek\.com)/i;

function reportIfForbidden(context, node, raw) {
  if (typeof raw !== 'string' || raw.length === 0) return;
  if (!FORBIDDEN_HOSTNAME_PATTERN.test(raw)) return;
  context.report({
    node,
    messageId: 'forbidden',
    data: { value: raw.length > 80 ? `${raw.slice(0, 77)}...` : raw },
  });
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Forbid BoardGameGeek-owned hostnames in source (ADR-1903 / Issue #2123 ToS).',
      category: 'Best Practices',
      recommended: true,
    },
    schema: [],
    messages: {
      forbidden:
        'BGG-owned hostname is forbidden in user-side code (ADR-1903, Issue #2123). Found: {{value}}',
    },
  },
  create(context) {
    return {
      Literal(node) {
        reportIfForbidden(context, node, node.value);
      },
      TemplateElement(node) {
        // node.value.cooked is the interpolation-free string between `${}`.
        reportIfForbidden(context, node, node.value && node.value.cooked);
      },
    };
  },
};
