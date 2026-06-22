/**
 * Tests for `local/no-store-scores-direct` (issue #2389 Block A.7).
 *
 * Run with:
 *   node --test apps/web/eslint-rules/no-store-scores-direct.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./no-store-scores-direct.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
  },
});

test('no-store-scores-direct', () => {
  ruleTester.run('no-store-scores-direct', rule, {
    valid: [
      // reads scoringType — fine
      { code: 'const t = useLiveSessionStore(s => s.scoringType);' },
      // reads scoreData — fine
      { code: 'const d = useLiveSessionStore(s => s.scoreData);' },
      // unrelated hook — fine
      { code: 'const x = useOtherStore(s => s.scores);' },
    ],
    invalid: [
      {
        code: 'const sc = useLiveSessionStore(s => s.scores);',
        errors: [{ messageId: 'noScoresDirect' }],
      },
    ],
  });
});
