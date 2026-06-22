/**
 * Tests for `local/prefer-use-game-title` (issue #2339 sub-PR 2/3).
 *
 * Run with:
 *   node --test apps/web/eslint-rules/prefer-use-game-title.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./prefer-use-game-title.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

test('prefer-use-game-title', () => {
  ruleTester.run('prefer-use-game-title', rule, {
    valid: [
      // Resolved title from useGameTitle in JSX — happy path
      {
        code: `function Card({ game }) {
  const { value: title } = useGameTitle(game);
  return <h3>{title}</h3>;
}`,
        filename: 'apps/web/src/components/games/GameCard.tsx',
      },
      // aria-label is allowed to reference canonical EN (DEC-FE-9)
      {
        code: `function Card({ game, title }) {
  return <h3 aria-label={\`Localized: \${game.title}\`}>{title}</h3>;
}`,
        filename: 'apps/web/src/components/games/GameCard.tsx',
      },
      // Heuristic: line containing useGameTitle is allowed even with raw access
      {
        code: `function Card({ game }) {
  const { value: title } = useGameTitle(game); /* fallback: game.title */
  return <h3>{title}</h3>;
}`,
        filename: 'apps/web/src/components/games/GameCard.tsx',
      },
      // Test files are skipped via allow-list
      {
        code: `function Card({ game }) { return <h3>{game.title}</h3>; }`,
        filename: 'apps/web/src/components/__tests__/GameCard.test.tsx',
      },
      // Storybook fixtures skipped
      {
        code: `function Card({ game }) { return <h3>{game.title}</h3>; }`,
        filename: 'apps/web/src/components/games/GameCard.stories.tsx',
      },
      // The i18n hook itself uses game.title internally — allowed
      {
        code: `function resolveTitle(game) { return game.title; }`,
        filename: 'apps/web/src/lib/i18n/use-game-title.ts',
      },
      // Non-JSX context (e.g. plain TS) — rule does not fire
      {
        code: `function logTitle(game) { console.log(game.title); }`,
        filename: 'apps/web/src/lib/utils/log.ts',
      },
      // Different identifier (not `game`) — rule does not fire
      {
        code: `function Card({ board }) { return <h3>{board.title}</h3>; }`,
        filename: 'apps/web/src/components/games/Card.tsx',
      },
    ],
    invalid: [
      {
        code: `function Card({ game }) { return <h3>{game.title}</h3>; }`,
        filename: 'apps/web/src/components/games/GameCard.tsx',
        errors: [{ messageId: 'preferHook' }],
      },
      {
        code: `function Hero({ game }) {
  return (
    <div>
      <h1>{game.title}</h1>
      <p>Subtitle</p>
    </div>
  );
}`,
        filename: 'apps/web/src/components/discover/Hero.tsx',
        errors: [{ messageId: 'preferHook' }],
      },
    ],
  });
});
