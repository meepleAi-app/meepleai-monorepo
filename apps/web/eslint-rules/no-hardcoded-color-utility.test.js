/**
 * Tests for `local/no-hardcoded-color-utility`.
 *
 * Run with:
 *   node --test apps/web/eslint-rules/no-hardcoded-color-utility.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./no-hardcoded-color-utility.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

test('no-hardcoded-color-utility', () => {
  ruleTester.run('no-hardcoded-color-utility', rule, {
    valid: [
      // Semantic tokens — OK
      { code: `const x = <div className="bg-background text-foreground" />;` },
      { code: `const x = <div className="bg-card text-muted-foreground" />;` },
      { code: `const x = <div className="bg-muted border-border" />;` },
      // Entity utilities — OK
      { code: `const x = <div className="bg-entity-game text-entity-session" />;` },
      { code: `const x = <div className="ring-entity-event/30" />;` },
      // Hue-based palettes (NOT in neutral families) — OK
      { code: `const x = <div className="bg-red-500 text-emerald-700" />;` },
      { code: `const x = <div className="bg-amber-200 ring-blue-400" />;` },
      // No className at all
      { code: `const x = <div data-slot="foo" />;` },
      // clsx with semantic tokens
      { code: `const x = <div className={clsx('bg-card', 'text-foreground')} />;` },
      // DS-4 exemption: text-white on colored bg (entity, gradient, hue, arbitrary)
      { code: `const x = <div className="bg-entity-game text-white" />;` },
      {
        code: `const x = <div className="bg-gradient-to-r from-purple-600 to-purple-500 text-white" />;`,
      },
      { code: `const x = <div className="bg-[hsl(25,90%,45%)] text-white" />;` },
      { code: `const x = <button className="bg-blue-500 text-white border-white/20" />;` },
      { code: `const x = <div className="bg-emerald-500/20 text-white" />;` },
      // Semantic shadcn colored tokens
      { code: `const x = <div className="bg-primary text-white" />;` },
      { code: `const x = <div className="bg-secondary text-white border-white/20" />;` },
      { code: `const x = <div className="bg-destructive text-white" />;` },
      // #4513 — cva(): base e varianti con token semantici
      {
        code: `const v = cva('inline-flex rounded-md border px-2.5', {
          variants: {
            variant: {
              default: 'border-transparent bg-primary text-primary-foreground',
              secondary: 'border-transparent bg-secondary text-secondary-foreground',
              outline: 'text-foreground border-border/50 dark:border-border/70',
            },
          },
          defaultVariants: { variant: 'default' },
        });`,
      },
      // #4513 — cva(): esenzione DS-4 anche dentro una variante (testo bianco su fondo colorato)
      {
        code: `const v = cva('p-2', { variants: { tone: { brand: 'bg-amber-500 text-white' } } });`,
      },
    ],
    invalid: [
      // Plain bg-white
      {
        code: `const x = <div className="bg-white" />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // bg-slate-50
      {
        code: `const x = <div className="bg-slate-50 p-4" />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // dark: variant
      {
        code: `const x = <div className="dark:bg-slate-900" />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // text-gray-* + border-zinc-*
      {
        code: `const x = <div className="text-gray-700 border-zinc-200" />;`,
        errors: [{ messageId: 'hardcoded' }], // PERTURBAZIONE #4533: da revertire
      },
      // Template literal
      {
        code: 'const x = <div className={`bg-slate-100 ${other}`} />;',
        errors: [{ messageId: 'hardcoded' }],
      },
      // clsx arg
      {
        code: `const x = <div className={clsx('bg-white', 'p-4')} />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // Object key in clsx
      {
        code: `const x = <div className={clsx({ 'bg-slate-50': active })} />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // Opacity modifier `/50`
      {
        code: `const x = <div className="bg-slate-900/50" />;`,
        errors: [{ messageId: 'hardcoded' }],
      },
      // #4513 — cva(): valore di una variante (il caso del Badge legacy)
      {
        code: `const v = cva('inline-flex', {
          variants: { variant: { outline: 'text-slate-900 border-slate-300' } },
        });`,
        errors: [
          { messageId: 'hardcoded', data: { cls: 'text-slate-900' } },
          { messageId: 'hardcoded', data: { cls: 'border-slate-300' } },
        ],
      },
      // #4513 — cva(): argomento base
      {
        code: `const v = cva('inline-flex bg-white p-2');`,
        errors: [{ messageId: 'hardcoded', data: { cls: 'bg-white' } }],
      },
      // #4513 — cva(): base come array di stringhe
      {
        code: `const v = cva(['inline-flex', 'text-gray-700']);`,
        errors: [{ messageId: 'hardcoded', data: { cls: 'text-gray-700' } }],
      },
      // #4513 — cva(): valore di variante come template literal e come array
      {
        code: 'const v = cva("p-2", { variants: { size: { sm: `bg-zinc-100 ${x}`, lg: ["p-4", "dark:text-stone-300"] } } });',
        errors: [
          { messageId: 'hardcoded', data: { cls: 'bg-zinc-100' } },
          // il prefisso `dark:` non fa parte della classe riportata
          { messageId: 'hardcoded', data: { cls: 'text-stone-300' } },
        ],
      },
      // #4513 — cva(): compoundVariants, sia `class` sia `className`
      {
        code: `const v = cva('p-2', {
          variants: { a: { x: 'p-1' }, b: { y: 'p-3' } },
          compoundVariants: [
            { a: 'x', b: 'y', class: 'bg-neutral-50' },
            { a: 'x', className: 'text-slate-600' },
          ],
        });`,
        errors: [
          { messageId: 'hardcoded', data: { cls: 'bg-neutral-50' } },
          { messageId: 'hardcoded', data: { cls: 'text-slate-600' } },
        ],
      },
    ],
  });
});
