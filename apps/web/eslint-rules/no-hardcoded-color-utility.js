/**
 * ESLint rule: no hardcoded Tailwind color utilities.
 *
 * Disallows Tailwind classes that hardcode neutral colors (white, black,
 * slate-*, gray-*, zinc-*, neutral-*, stone-*) in `className` props and
 * `clsx()`/`cn()`-style argument literals. Components must consume the
 * semantic tokens introduced by the canonical token system
 * (`bg-background`, `bg-card`, `text-muted-foreground`, etc.) so that the
 * mockup palette flows through every surface.
 *
 * Scope: src/**\/*.{ts,tsx} (excluding tests, scripts, stories).
 *
 * Allowed (whitelist):
 *   - Entity utilities: bg-entity-*, text-entity-*, border-entity-*, ring-entity-*
 *   - Status accents that have no semantic token yet: bg-red-*, bg-emerald-*, etc.
 *     are NOT linted here — only the neutral-greyscale palette is forbidden.
 *   - `text-white|black` / `border-white|black` / `ring-white|black` when the
 *     SAME className already declares a colored background (entity utility,
 *     Tailwind hue class, arbitrary HSL/RGB, or a gradient). Rationale: the
 *     mockup convention `.e-bg { color: #fff }` puts white text on colored
 *     surfaces — that pattern is intentional and should not be flagged.
 *
 * To suppress in unavoidable cases (e.g. legacy parity until cluster migration):
 *   // eslint-disable-next-line local/no-hardcoded-color-utility -- <reason>
 *
 * Refs:
 *   - Spec: docs/for-developers/specs/2026-05-12-token-canonicalization.md (DS-2)
 *   - Amendment: DS-4 (2026-05-12) — colored-bg context exemption.
 *   - Bridge map: docs/for-developers/frontend/token-bridge-map.md
 */

'use strict';

// Tailwind neutral-greyscale palette (the most common "legacy bg" offenders).
// Hue-based palettes (red, amber, blue, …) are intentionally NOT forbidden:
// some surfaces (toasts, status badges, danger states) still need them until
// a dedicated semantic token lands.
const NEUTRAL_FAMILIES = ['slate', 'gray', 'zinc', 'neutral', 'stone'];
const NEUTRAL_FAMILY_PATTERN = NEUTRAL_FAMILIES.join('|');

// Matches:
//   bg-white | bg-black | text-white | …
//   bg-slate-50 | text-gray-700 | border-zinc-200 | …
//   ring-stone-300 | divide-neutral-200 | …
//   light/dark variants: dark:bg-slate-900, hover:text-gray-500, etc.
const FORBIDDEN_CLASS_REGEX = new RegExp(
  // Optional variant prefix(es) like `dark:`, `hover:`, `md:dark:`, …
  '(?:^|\\s)' +
    '(?:[a-z0-9-]+:)*' +
    '(' +
    // bg/text/border/ring/divide/outline/accent/caret/fill/stroke + -white/-black
    '(?:bg|text|border|ring|divide|outline|accent|caret|fill|stroke)-(?:white|black)' +
    '|' +
    // ... + -<family>-<shade>
    '(?:bg|text|border|ring|divide|outline|accent|caret|fill|stroke)-(?:' +
    NEUTRAL_FAMILY_PATTERN +
    ')-(?:50|100|200|300|400|500|600|700|800|900|950)' +
    ')' +
    '(?=\\s|$|/)', // followed by space, end, or opacity modifier (e.g. `/50`)
  'g'
);

// DS-4 amendment: if the same className already paints a colored surface,
// pairing it with `text-white` / `border-white` etc. is the mockup-faithful
// pattern (`.e-bg { color: #fff }`) and should NOT be flagged.
// We look for these signals in the same string:
//   - bg-entity-*                                 → semantic entity utility
//   - bg-gradient-to-* / via-* / from-* / to-*    → Tailwind gradient
//   - bg-[hsl(*)] / bg-[rgb(*)] / bg-[#abc]       → arbitrary value
//   - bg-{red,orange,amber,yellow,lime,green,emerald,teal,cyan,sky,blue,
//        indigo,violet,purple,fuchsia,pink,rose}-{shade}
//     (hue palettes — NOT in the neutral families forbidden above)
const COLORED_BG_REGEX = new RegExp(
  '(?:^|\\s)' +
    '(?:[a-z0-9-]+:)*' +
    '(?:' +
    'bg-entity-[a-z]+' +
    '|' +
    'bg-gradient-(?:to-[a-z]+|[a-z]+)' +
    '|' +
    '(?:from|via|to)-[a-z0-9-]+' +
    '|' +
    'bg-\\[[^\\]]+\\]' + // arbitrary value bg-[...]
    '|' +
    'bg-(?:red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-(?:50|100|200|300|400|500|600|700|800|900|950)' +
    '|' +
    // Semantic colored tokens — these resolve to brand-colored surfaces and
    // routinely pair with white/dark text (shadcn convention).
    'bg-(?:primary|secondary|accent|destructive|brand)(?:/[0-9]+)?' +
    ')'
);

// Classes whose match should be SUPPRESSED if a colored bg is also present.
const COLOR_ON_BG_EXEMPT = /^(?:bg|text|border|ring|divide|outline|caret|fill|stroke)-(?:white|black)$/;

function findForbiddenMatches(value) {
  if (typeof value !== 'string' || value.length === 0) return [];
  // Detect colored-bg context once per string.
  const hasColoredBg = COLORED_BG_REGEX.test(value);
  const matches = [];
  // matchAll yields all matches in one go; safer than a stateful regex loop.
  for (const m of value.matchAll(FORBIDDEN_CLASS_REGEX)) {
    const cls = m[1];
    // DS-4 exemption: `text-white` & friends are legitimate on a colored bg
    // (mockup `.e-bg { color: #fff }` pattern).
    if (hasColoredBg && COLOR_ON_BG_EXEMPT.test(cls)) continue;
    matches.push(cls);
  }
  return matches;
}

module.exports = {
  meta: {
    type: 'suggestion',
    docs: {
      description:
        'Disallow hardcoded Tailwind neutral-greyscale color utilities. ' +
        'Use semantic tokens from the canonical design system instead ' +
        '(bg-background, bg-card, text-muted-foreground, …).',
      category: 'Design System',
      recommended: false,
    },
    schema: [],
    messages: {
      hardcoded:
        'Hardcoded Tailwind color class "{{cls}}". Use a semantic token ' +
        '(bg-background, bg-card, bg-muted, text-foreground, text-muted-foreground, …) ' +
        'mapped to the canonical mockup palette. See ' +
        'docs/for-developers/frontend/token-bridge-map.md.',
    },
  },

  create(context) {
    function report(node, value) {
      const found = findForbiddenMatches(value);
      for (const cls of found) {
        context.report({
          node,
          messageId: 'hardcoded',
          data: { cls },
        });
      }
    }

    function visitLiteralLike(node) {
      // String literal: "bg-white text-slate-700"
      if (node.type === 'Literal' && typeof node.value === 'string') {
        report(node, node.value);
        return;
      }
      // Template literal without expressions: `bg-slate-50 ${foo}` — check each quasi.
      if (node.type === 'TemplateLiteral') {
        for (const quasi of node.quasis) {
          report(quasi, quasi.value.cooked || '');
        }
        return;
      }
    }

    return {
      // <div className="bg-white ..." />
      JSXAttribute(node) {
        if (!node.name || node.name.name !== 'className') return;
        if (!node.value) return;

        // className="..."
        if (node.value.type === 'Literal') {
          visitLiteralLike(node.value);
          return;
        }
        // className={"..."} or className={`...`} or className={clsx(...)}
        if (node.value.type === 'JSXExpressionContainer') {
          const expr = node.value.expression;
          if (!expr) return;
          if (expr.type === 'Literal' || expr.type === 'TemplateLiteral') {
            visitLiteralLike(expr);
          }
          // CallExpression (clsx, cn, classnames, twMerge): inspect every
          // string-literal argument and template-literal quasi.
          if (expr.type === 'CallExpression') {
            for (const arg of expr.arguments) {
              if (arg.type === 'Literal' || arg.type === 'TemplateLiteral') {
                visitLiteralLike(arg);
              }
              // Object args like { 'bg-white': condition } — check the key.
              if (arg.type === 'ObjectExpression') {
                for (const prop of arg.properties) {
                  if (
                    prop.type === 'Property' &&
                    (prop.key.type === 'Literal' || prop.key.type === 'TemplateLiteral')
                  ) {
                    visitLiteralLike(prop.key);
                  }
                }
              }
            }
          }
        }
      },
    };
  },
};
