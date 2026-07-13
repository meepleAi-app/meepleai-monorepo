'use strict';

const test = require('node:test');
const { RuleTester } = require('eslint');
const tsParser = require('@typescript-eslint/parser');
const rule = require('./no-standalone-card-renderer.js');

const ruleTester = new RuleTester({
  languageOptions: {
    parser: tsParser,
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
});

test('no-standalone-card-renderer (import-boundary)', () => {
  ruleTester.run('no-standalone-card-renderer', rule, {
    valid: [
      // Composes the public MeepleCard — fine.
      {
        filename: 'apps/web/src/components/games/MeepleGameCard.tsx',
        code:
          "import { MeepleCard } from '@/components/ui/data-display/meeple-card';\n" +
          'export function MeepleGameCard() { return <MeepleCard entity="game" title="x" />; }',
      },
      // Inside the canonical dir — internals may reach into parts.
      {
        filename: 'apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx',
        code:
          "import { Cover } from '@/components/ui/data-display/meeple-card/parts/Cover';\n" +
          'export function GridCard() { return <div><Cover /></div>; }',
      },
      // Test file — may import internals to test them.
      {
        filename: 'apps/web/src/__tests__/components/meeple-card/ManaPips.test.tsx',
        code:
          "import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'ManaPips;',
      },
      // Type-only import — carries no rendering logic.
      {
        filename: 'apps/web/src/hooks/queries/useSomething.ts',
        code:
          "import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'const x = [] as ManaPip[];\nx;',
      },
      // Allowlisted value-util import.
      {
        filename: 'apps/web/src/hooks/queries/useGameManaPips.ts',
        code:
          "import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';\n" +
          'getKbPipColor;',
      },
    ],
    invalid: [
      // Value deep-import of a part from outside the canonical dir.
      {
        filename: 'apps/web/src/components/games/RogueCard.tsx',
        code:
          "import { Cover } from '@/components/ui/data-display/meeple-card/parts/Cover';\n" +
          'export function RogueCard() { return <Cover />; }',
        errors: [{ messageId: 'deepImport' }],
      },
      // Value deep-import of a variant.
      {
        filename: 'apps/web/src/components/games/RogueCard2.tsx',
        code:
          "import { GridCard } from '@/components/ui/data-display/meeple-card/variants/GridCard';\n" +
          'export function RogueCard2() { return <GridCard entity="game" title="x" />; }',
        errors: [{ messageId: 'deepImport' }],
      },
    ],
  });
});
