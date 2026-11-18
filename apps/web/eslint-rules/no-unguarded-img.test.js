/**
 * Tests for `local/no-unguarded-img` (issue #5072).
 *
 * Run with:
 *   node --test apps/web/eslint-rules/no-unguarded-img.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./no-unguarded-img.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

const ADMIN = { allowBggHostIn: ['src/app/admin/', 'src/components/admin/'] };
const USER_FILE = '/repo/apps/web/src/components/library/Card.tsx';
const ADMIN_FILE = '/repo/apps/web/src/components/admin/shared-games/BggSearchPanel.tsx';

test('no-unguarded-img', () => {
  ruleTester.run('no-unguarded-img', rule, {
    valid: [
      // Constant sources need no guard.
      { code: `<img src="/logo.svg" alt="" />` },
      { code: `<img src={'/logo.svg'} alt="" />` },
      { code: `<img src={\`/icons/dice.svg\`} alt="" />` },
      { code: `<img src={undefined} alt="" />` },
      { code: `<img alt="" />` },
      { code: `<img src="/a.png" srcSet="/a.png 1x, /a@2x.png 2x" alt="" />` },
      {
        code: `<picture><source srcSet="/a.webp" type="image/webp" /><img src="/a.png" alt="" /></picture>`,
      },
      // Local previews the code itself builds.
      { code: `<img src={\`data:image/png;base64,\${b64}\`} alt="" />` },
      { code: `<img src={\`blob:\${id}\`} alt="" />` },
      { code: `<img src={URL.createObjectURL(file)} alt="" />` },
      // The guard itself.
      { code: `<SafeImg src={game.imageUrl} alt="" />` },
      // next/image without `unoptimized` goes through the loader.
      {
        code: `import Image from 'next/image'; <Image src={game.coverUrl} alt="" width={64} height={64} />`,
      },
      { code: `import Image from 'next/image'; <Image src={url} unoptimized={false} alt="" />` },
      { code: `import Image from 'next/image'; <Image src="/hero.png" unoptimized alt="" />` },
      // `unoptimized` on a component that is not next/image is not this rule's business.
      { code: `import Image from './my-image'; <Image src={url} unoptimized alt="" />` },
      // The admin exception, where it is allowed.
      {
        code: `<SafeImg src={result.thumbnailUrl} allowBggHost alt="" />`,
        options: [ADMIN],
        filename: ADMIN_FILE,
      },
      {
        code: `<SafeImg src={result.thumbnailUrl} allowBggHost alt="" />`,
        options: [ADMIN],
        filename: 'D:\\repo\\apps\\web\\src\\app\\admin\\(dashboard)\\games\\page.tsx',
      },
    ],
    invalid: [
      { code: `<img src={game.imageUrl} alt="" />`, errors: [{ messageId: 'img' }] },
      { code: `<img src={url} alt="" />`, errors: [{ messageId: 'img' }] },
      { code: `<img src={\`\${base}/cover.jpg\`} alt="" />`, errors: [{ messageId: 'img' }] },
      { code: `<img src={a ?? '/x.svg'} alt="" />`, errors: [{ messageId: 'img' }] },
      // A spread may carry any src.
      { code: `<img {...props} />`, errors: [{ messageId: 'img' }] },
      { code: `<img src="/x.svg" {...props} />`, errors: [{ messageId: 'img' }] },
      // A browser prefers srcSet to src, so it counts the same.
      { code: `<img src="/x.svg" srcSet={game.srcSet} alt="" />`, errors: [{ messageId: 'img' }] },
      { code: `<source srcSet={game.coverUrl} />`, errors: [{ messageId: 'source' }] },
      { code: `<source {...props} />`, errors: [{ messageId: 'source' }] },
      {
        code: `import Image from 'next/image'; <Image src={invitation.imageUrl} unoptimized alt="" />`,
        errors: [{ messageId: 'unoptimized' }],
      },
      {
        code: `import NextImage from 'next/image'; <NextImage src={url} unoptimized={true} alt="" />`,
        errors: [{ messageId: 'unoptimized' }],
      },
      {
        code: `import { default as Img } from 'next/image'; <Img src={url} unoptimized alt="" />`,
        errors: [{ messageId: 'unoptimized' }],
      },
      // The admin exception outside admin paths, or without the option.
      {
        code: `<SafeImg src={game.imageUrl} allowBggHost alt="" />`,
        options: [ADMIN],
        filename: USER_FILE,
        errors: [{ messageId: 'allowBggHost' }],
      },
      {
        code: `<SafeImg src={game.imageUrl} allowBggHost alt="" />`,
        filename: ADMIN_FILE,
        errors: [{ messageId: 'allowBggHost' }],
      },
      // A path that only contains the admin folder name further down is not an admin path.
      {
        code: `<SafeImg src={game.imageUrl} allowBggHost alt="" />`,
        options: [ADMIN],
        filename: '/repo/apps/web/src/components/library/admin-like/Card.tsx',
        errors: [{ messageId: 'allowBggHost' }],
      },
    ],
  });
});
