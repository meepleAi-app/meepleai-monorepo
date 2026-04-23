/**
 * ESLint Custom Rule: no-dual-connection-source
 *
 * Prevents passing both `connections` and `navItems`/`manaPips` as props
 * to `<MeepleCard>` simultaneously.
 *
 * **Rationale:**
 * MeepleCard renderer integration (Step 1.6) centralises connection
 * rendering on a single source of truth. The `connections` prop is the
 * canonical input; `navItems` and `manaPips` are deprecated adapter
 * inputs bridged via `useConnectionSource()`. Passing both at the same
 * call site creates an ambiguous render contract and defeats the
 * adapter fallback, so we forbid the co-presence statically.
 *
 * **References:**
 * - Spec: docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md §R1.6.3 "Single-source invariant"
 * - Plan: docs/superpowers/plans/2026-04-23-connectionchip-step-1.6-renderer-integration.md §8
 */

/** @type {import('eslint').Rule.RuleModule} */
module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Disallow passing both `connections` and `navItems`/`manaPips` to <MeepleCard>.',
      category: 'Possible Errors',
      recommended: true,
    },
    messages: {
      dualSource:
        'Cannot mix `connections` with `navItems`/`manaPips` on the same MeepleCard. Pick one source.',
    },
    schema: [],
  },

  create(context) {
    return {
      JSXOpeningElement(node) {
        if (node.name.type !== 'JSXIdentifier') return;
        if (node.name.name !== 'MeepleCard') return;

        const attrNames = new Set(
          node.attributes
            .filter(a => a.type === 'JSXAttribute' && a.name && a.name.type === 'JSXIdentifier')
            .map(a => a.name.name)
        );

        const hasConnections = attrNames.has('connections');
        const hasNav = attrNames.has('navItems');
        const hasMana = attrNames.has('manaPips');

        if (hasConnections && (hasNav || hasMana)) {
          context.report({ node, messageId: 'dualSource' });
        }
      },
    };
  },
};
