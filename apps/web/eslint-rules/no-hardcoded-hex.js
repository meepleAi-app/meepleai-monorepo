/**
 * ESLint Custom Rule: no-hardcoded-hex
 *
 * Prevents hardcoded color literals in v2 component code.
 *
 * **Problem:**
 * V2 components must reference design tokens (`hsl(var(--c-{entity}))`) so that
 * theme switches, dark mode, and entity recoloring work without per-component
 * patches. Inline `hsl(38, 92%, 50%)` / `#7c3aed` / `rgb(124, 58, 237)` literals
 * defeat that contract and reintroduce the V1 fragmentation that issue #571
 * (V2 Design Migration) is closing out.
 *
 * **Solution:**
 * Use the `entityHsl(entity, alpha?)` helper from `@/lib/color-utils`, which
 * emits `hsl(var(--c-{entity}))` (or `hsl(var(--c-{entity}) / alpha)`).
 *
 * For semantic colors, reference `--c-success`, `--c-warning`, `--c-danger`,
 * `--c-info` directly: `hsl(var(--c-success))`.
 *
 * **Scope:**
 * This rule only applies to v2 component code (src/components/ui/v2/). V1
 * components, app routes, and admin pages can still contain hex/hsl literals
 * pending Phase 1+ migrations. The scope is controlled by an override block
 * in eslint.config.mjs (search for "no-hardcoded-hex").
 *
 * **References:**
 * - Issue #571 (V2 Design Migration umbrella)
 * - Issue #572 (entityHsl helper + this rule)
 * - docs/frontend/token-audit-2026-04-26.md
 */

'use strict';

// hsl(38, 92%, 50%) / hsl(38,92%,50%) / hsl(38 92% 50%) — but NOT hsl(var(--anything))
// Comma-separated and space-separated are both valid CSS but only the literal-number forms are forbidden.
const HSL_LITERAL = /hsl\(\s*\d+\s*[,\s]\s*\d+%?\s*[,\s]\s*\d+%?\s*(?:[,/]\s*[\d.]+%?\s*)?\)/i;
// #abc / #abcd / #abcdef / #abcdef00 — 3, 4, 6, or 8 hex digits (with optional alpha)
const HEX_LITERAL = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/;
// rgb(124, 58, 237) / rgba(124,58,237,0.5)
const RGB_LITERAL = /rgba?\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*(?:,\s*[\d.]+\s*)?\)/i;

function findOffender(value) {
  if (typeof value !== 'string') return null;
  if (HSL_LITERAL.test(value)) return 'hsl';
  if (HEX_LITERAL.test(value)) return 'hex';
  if (RGB_LITERAL.test(value)) return 'rgb';
  return null;
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Forbid hardcoded color literals (hex, rgb, hsl-with-numbers) in v2 component code; use entityHsl() or hsl(var(--c-*)) instead',
      category: 'Stylistic Issues',
      recommended: false,
    },
    messages: {
      hardcodedHsl:
        'Hardcoded hsl() literal "{{value}}". Use entityHsl(entity, alpha?) from @/lib/color-utils, or reference a CSS var: hsl(var(--c-{entity})). See docs/frontend/token-audit-2026-04-26.md.',
      hardcodedHex:
        'Hardcoded hex color "{{value}}". Use entityHsl(entity, alpha?) from @/lib/color-utils, or reference a CSS var: hsl(var(--c-{entity})). See docs/frontend/token-audit-2026-04-26.md.',
      hardcodedRgb:
        'Hardcoded rgb() literal "{{value}}". Use entityHsl(entity, alpha?) from @/lib/color-utils, or reference a CSS var: hsl(var(--c-{entity})). See docs/frontend/token-audit-2026-04-26.md.',
    },
    schema: [],
  },

  create(context) {
    function check(node, value) {
      const kind = findOffender(value);
      if (!kind) return;
      const messageId =
        kind === 'hsl' ? 'hardcodedHsl' : kind === 'hex' ? 'hardcodedHex' : 'hardcodedRgb';
      context.report({
        node,
        messageId,
        data: { value: value.length > 50 ? value.slice(0, 50) + '…' : value },
      });
    }

    return {
      Literal(node) {
        check(node, node.value);
      },
      TemplateElement(node) {
        // node.value.raw / node.value.cooked
        check(node, node.value && node.value.cooked);
      },
    };
  },
};
