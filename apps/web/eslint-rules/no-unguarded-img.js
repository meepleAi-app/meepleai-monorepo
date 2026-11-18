/**
 * ESLint Custom Rule: no-unguarded-img
 *
 * Issue #5072 — central guard of the BGG asset ban (#2123, ADR-059 §5).
 *
 * **Problem:**
 * The ban is enforced for `next/image` by the custom loader (`src/lib/images/safe-loader.ts`). Two forms skip it:
 * a plain `<img>`, and an `<Image unoptimized>`. Each component that rendered a URL from the API had to remember
 * `shouldUsePlaceholder` on its own, and several did not.
 *
 * **Solution:**
 * `SafeImg` (`src/components/ui/data-display/safe-img.tsx`) is the guarded `<img>`. This rule reports:
 *   - `<img>` whose `src` is not a constant;
 *   - `next/image` with `unoptimized` whose `src` is not a constant;
 *   - `SafeImg allowBggHost` outside the paths in the `allowBggHostIn` option (the admin exception of #5072).
 *
 * A `src` counts as constant, and so needs no guard, when the code itself fixes it:
 *   - a string literal, `undefined` or `null`, or a template literal without expressions;
 *   - a template literal that starts with `data:` or `blob:` (a local preview);
 *   - a direct `URL.createObjectURL(…)` call.
 * A spread (`{...props}`) on an `<img>` may carry any `src`, so it is reported. A preview kept in state is not
 * constant to the rule: it goes through `SafeImg`, which lets `blob:` and `data:` through unchanged.
 *
 * **References:**
 * - Issue #5072, #2123
 * - ADR-059 §5 (and §5.6, the admin exception)
 */

'use strict';

const LOCAL_PREVIEW_PREFIX = /^(data|blob):/i;

function isConstantExpression(node) {
  if (!node) return true;
  switch (node.type) {
    case 'Literal':
      return node.value === null || typeof node.value === 'string';
    case 'Identifier':
      return node.name === 'undefined';
    case 'TemplateLiteral':
      return (
        node.expressions.length === 0 ||
        LOCAL_PREVIEW_PREFIX.test(node.quasis[0].value.cooked ?? '')
      );
    case 'CallExpression':
      return (
        node.callee.type === 'MemberExpression' &&
        node.callee.object.type === 'Identifier' &&
        node.callee.object.name === 'URL' &&
        node.callee.property.type === 'Identifier' &&
        node.callee.property.name === 'createObjectURL'
      );
    case 'TSAsExpression':
    case 'TSNonNullExpression':
      return isConstantExpression(node.expression);
    default:
      return false;
  }
}

function attribute(openingElement, name) {
  return openingElement.attributes.find(
    a => a.type === 'JSXAttribute' && a.name.type === 'JSXIdentifier' && a.name.name === name
  );
}

function hasSpread(openingElement) {
  return openingElement.attributes.some(a => a.type === 'JSXSpreadAttribute');
}

function isConstantSrc(srcAttribute) {
  if (!srcAttribute || !srcAttribute.value) return true;
  if (srcAttribute.value.type === 'Literal') return true;
  if (srcAttribute.value.type === 'JSXExpressionContainer') {
    const expression = srcAttribute.value.expression;
    return expression.type === 'JSXEmptyExpression' || isConstantExpression(expression);
  }
  return false;
}

/** `unoptimized`, `unoptimized={true}` or any expression other than a literal `false`. */
function isUnoptimized(openingElement) {
  const attr = attribute(openingElement, 'unoptimized');
  if (!attr) return false;
  if (!attr.value) return true;
  return !(
    attr.value.type === 'JSXExpressionContainer' &&
    attr.value.expression.type === 'Literal' &&
    attr.value.expression.value === false
  );
}

function elementName(openingElement) {
  return openingElement.name.type === 'JSXIdentifier' ? openingElement.name.name : null;
}

function normalizePath(filename) {
  return filename.split('\\').join('/');
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Every <img> and unoptimized next/image with a variable src goes through SafeImg (BGG asset ban, issue #5072)',
      category: 'Possible Errors',
      recommended: true,
    },
    messages: {
      img: 'A plain <img> with a variable src skips the BGG guard (#5072, ADR-059 §5). Use <SafeImg> from @/components/ui/data-display/safe-img.',
      unoptimized:
        'next/image with `unoptimized` skips the loader and its BGG guard (#5072, ADR-059 §5). Drop `unoptimized`, or use <SafeImg> from @/components/ui/data-display/safe-img.',
      allowBggHost:
        '`allowBggHost` is the admin BGG search exception (#5072, ADR-059 §5.6): it is allowed only under {{paths}}.',
    },
    schema: [
      {
        type: 'object',
        properties: {
          allowBggHostIn: { type: 'array', items: { type: 'string' } },
        },
        additionalProperties: false,
      },
    ],
  },

  create(context) {
    const options = context.options[0] || {};
    const allowBggHostIn = options.allowBggHostIn || [];
    const filename = normalizePath(context.filename ?? context.getFilename());
    const nextImageNames = new Set();

    return {
      ImportDeclaration(node) {
        if (node.source.value !== 'next/image') return;
        for (const specifier of node.specifiers) {
          if (
            specifier.type === 'ImportDefaultSpecifier' ||
            (specifier.type === 'ImportSpecifier' &&
              specifier.imported.type === 'Identifier' &&
              specifier.imported.name === 'default')
          ) {
            nextImageNames.add(specifier.local.name);
          }
        }
      },

      JSXOpeningElement(node) {
        const name = elementName(node);
        if (!name) return;

        if (name === 'img') {
          if (hasSpread(node) || !isConstantSrc(attribute(node, 'src'))) {
            context.report({ node, messageId: 'img' });
          }
          return;
        }

        if (nextImageNames.has(name)) {
          if (isUnoptimized(node) && (hasSpread(node) || !isConstantSrc(attribute(node, 'src')))) {
            context.report({ node, messageId: 'unoptimized' });
          }
          return;
        }

        if (name === 'SafeImg') {
          const allow = attribute(node, 'allowBggHost');
          const allowed = allowBggHostIn.some(prefix => filename.includes(`/${prefix}`));
          if (allow && !allowed) {
            context.report({
              node: allow,
              messageId: 'allowBggHost',
              data: { paths: allowBggHostIn.join(', ') || '(no path)' },
            });
          }
        }
      },
    };
  },
};
