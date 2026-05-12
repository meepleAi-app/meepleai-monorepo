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
        errors: [{ messageId: 'hardcoded' }, { messageId: 'hardcoded' }],
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
    ],
  });
});
