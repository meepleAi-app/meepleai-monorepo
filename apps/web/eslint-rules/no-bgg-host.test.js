/**
 * Tests for `local/no-bgg-host` (issue #2123).
 *
 * Run with:
 *   node --test apps/web/eslint-rules/no-bgg-host.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./no-bgg-host.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

test('no-bgg-host', () => {
  ruleTester.run('no-bgg-host', rule, {
    valid: [
      // Non-BGG URLs are fine.
      { code: `const x = 'https://upload.wikimedia.org/wikipedia/commons/foo.jpg';` },
      { code: `const x = 'https://abc.r2.cloudflarestorage.com/covers/x.webp';` },
      { code: `const x = '/placeholder.svg';` },
      { code: `const x = \`https://picsum.photos/200/300\`;` },
      // Unrelated words containing 'bgg' as a substring are NOT triggers.
      { code: `const x = 'debugger';` },
      { code: `const x = 'embeddings';` },
      // Empty / non-string literals.
      { code: `const x = 42;` },
      { code: `const x = '';` },
    ],
    invalid: [
      // cf.geekdo-images.com (most common in legacy seeds)
      {
        code: `const x = 'https://cf.geekdo-images.com/foo/img.jpg';`,
        errors: [{ messageId: 'bggHost' }],
      },
      // geekdo-images.com (alternate subdomain stripped)
      {
        code: `const x = 'https://geekdo-images.com/foo.jpg';`,
        errors: [{ messageId: 'bggHost' }],
      },
      // images.geekdo.com (legacy CDN)
      {
        code: `const x = 'https://images.geekdo.com/foo.jpg';`,
        errors: [{ messageId: 'bggHost' }],
      },
      // boardgamegeek.com (root domain — the API host)
      {
        code: `const x = 'https://www.boardgamegeek.com/xmlapi2/thing?id=13';`,
        errors: [{ messageId: 'bggHost' }],
      },
      // Embedded in template literal
      {
        code: 'const x = `https://cf.geekdo-images.com/${id}.jpg`;',
        errors: [{ messageId: 'bggHost' }],
      },
      // Case-insensitive — BgG aliasing must not bypass.
      {
        code: `const x = 'https://CF.GEEKDO-IMAGES.COM/X.JPG';`,
        errors: [{ messageId: 'bggHost' }],
      },
    ],
  });
});
