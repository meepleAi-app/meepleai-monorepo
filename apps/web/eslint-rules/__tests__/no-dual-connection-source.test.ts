/**
 * Tests for the no-dual-connection-source ESLint rule.
 *
 * Uses ESLint's built-in RuleTester. Authored in TypeScript so that
 * Vitest's default include pattern (`**\/__tests__/**\/*.test.ts`)
 * picks the file up. The rule itself is a CJS module, so we import
 * via `createRequire` to keep the interop explicit.
 *
 * `tester.run()` is invoked at the module top level: RuleTester
 * detects the global Vitest `describe`/`it` (injected by `globals: true`
 * in vitest.config.ts) and registers each valid/invalid case as its
 * own `it()` block, so Vitest reports 7 individual tests rather than
 * a single wrapper. Nesting `tester.run()` inside a Vitest `it()`
 * would defer the assertions until after the outer test resolves and
 * any failures would be silently swallowed.
 */
import { createRequire } from 'node:module';

import { RuleTester } from 'eslint';

// CJS interop: the rule file is `module.exports`, but Vitest runs ESM here.
const require = createRequire(import.meta.url);
// eslint-disable-next-line @typescript-eslint/no-require-imports
const rule = require('../no-dual-connection-source.js');

const tester = new RuleTester({
  languageOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
});

tester.run('no-dual-connection-source', rule, {
  valid: [
    { code: '<MeepleCard navItems={x} />' },
    { code: '<MeepleCard connections={y} />' },
    { code: '<MeepleCard manaPips={z} />' },
    { code: '<MeepleCard />' },
    { code: '<SomeOther connections={a} navItems={b} />' },
  ],
  invalid: [
    {
      code: '<MeepleCard connections={[]} navItems={[]} />',
      errors: [{ messageId: 'dualSource' }],
    },
    {
      code: '<MeepleCard connections={y} manaPips={z} />',
      errors: [{ messageId: 'dualSource' }],
    },
  ],
});
