/**
 * Tests for `local/api-client-v1-prefix`.
 *
 * Run with:
 *   node --test apps/web/eslint-rules/api-client-v1-prefix.test.js
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./api-client-v1-prefix.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

// Helpers that prepend a tracking declaration to each fixture so the rule
// has something to bind against. Two flavors:
//   - `withImport(code)` — singleton imported as apiClient.
//   - `withNew(code, name)` — local `const <name> = new HttpClient()`.
const IMPORT = `import { apiClient } from '@/lib/api/client';\n`;
const IMPORT_ALIAS = `import { apiClient as api } from '@/lib/api/client';\n`;
const NEW_DEFAULT = `const httpClient = new HttpClient();\n`;
const NEW_NAMED = name => `const ${name} = new HttpClient();\n`;

test('api-client-v1-prefix', () => {
  ruleTester.run('api-client-v1-prefix', rule, {
    valid: [
      // ── Imported apiClient: correct prefix on each verb. ───────────────
      { code: `${IMPORT}apiClient.get('/api/v1/foo');` },
      { code: `${IMPORT}apiClient.post('/api/v1/foo', body);` },
      { code: `${IMPORT}apiClient.put('/api/v1/foo', body);` },
      { code: `${IMPORT}apiClient.patch('/api/v1/foo', body);` },
      { code: `${IMPORT}apiClient.delete('/api/v1/foo');` },
      { code: `${IMPORT}apiClient.head('/api/v1/foo');` },
      { code: `${IMPORT}apiClient.options('/api/v1/foo');` },
      // Template literal whose static head matches the prefix.
      { code: `${IMPORT}apiClient.get(\`/api/v1/games/\${id}\`);` },
      { code: `${IMPORT}apiClient.get(\`/api/v1/games/\${id}/chunks/\${c}\`);` },
      // Dynamic path arguments — cannot statically verify, do not flag.
      { code: `${IMPORT}apiClient.get(someVar);` },
      { code: `${IMPORT}apiClient.post(buildUrl(id), body);` },
      { code: `${IMPORT}apiClient.get(prefix + '/foo');` },
      // Path that is not absolute is out of scope (caller signals intent).
      { code: `${IMPORT}apiClient.get('relative/path');` },
      { code: `${IMPORT}apiClient.get('');` },
      // Method not in the HTTP verb list — different API surface.
      { code: `${IMPORT}apiClient.fooBar('/foo');` },
      // Computed property (rare) is conservatively skipped.
      { code: `${IMPORT}apiClient['get']('/foo');` },

      // ── Aliased import: tracked under the alias name. ──────────────────
      { code: `${IMPORT_ALIAS}api.get('/api/v1/foo');` },

      // ── `new HttpClient()`: tracked by declared name. ──────────────────
      { code: `${NEW_DEFAULT}httpClient.get('/api/v1/foo');` },
      { code: `${NEW_DEFAULT}httpClient.post('/api/v1/users', body);` },
      { code: `${NEW_NAMED('api')}api.put('/api/v1/users/me', body);` },

      // ── No tracking declaration: bare `apiClient`/`httpClient` calls do
      // not flag, because the rule only acts on identifiers it has seen
      // bound to an HttpClient. This is the conservative choice — false
      // positives in code that uses a same-named local for something else
      // are worse than missing a few unbound calls (code review catches
      // those). ────────────────────────────────────────────────────────────
      { code: `apiClient.get('/foo');` },
      { code: `httpClient.get('/foo');` },

      // ── Import from a non-`client` source: not tracked. ────────────────
      {
        code: `import { apiClient } from '@/lib/auth';\napiClient.get('/foo');`,
      },
      // ── Import a non-`apiClient` name from a client module: not tracked.
      {
        code: `import { someOtherClient } from '@/lib/api/client';\nsomeOtherClient.get('/foo');`,
      },

      // ── Different client identifier, never bound to HttpClient: skip. ─
      { code: `${IMPORT}dashboardClient.get('/foo');` },
    ],
    invalid: [
      // ── Imported apiClient: each verb. ─────────────────────────────────
      {
        code: `${IMPORT}apiClient.get('/foo');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.post('/users/123', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.put('/catalog/games', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.patch('/agents/popular', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.delete('/sessions/abc');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      // Template literal whose static head does NOT match the prefix.
      {
        code: `${IMPORT}apiClient.get(\`/foo/\${id}\`);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.post(\`/users/\${id}/data\`, body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      // Almost-correct prefix but missing trailing slash → still wrong
      // because the proxy matches /api/v1/<anything> not /api/v1<anything>.
      {
        code: `${IMPORT}apiClient.get('/api/v1catalog/trending');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${IMPORT}apiClient.get('/api/v2/foo');`,
        errors: [{ messageId: 'missingPrefix' }],
      },

      // ── Aliased import: flagged via the alias name. ────────────────────
      {
        code: `${IMPORT_ALIAS}api.get('/foo');`,
        errors: [{ messageId: 'missingPrefix' }],
      },

      // ── `new HttpClient()`: flagged on the tracked local name. ─────────
      {
        code: `${NEW_DEFAULT}httpClient.get('/admin/alert-rules');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${NEW_DEFAULT}httpClient.post('/admin/alert-test', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${NEW_NAMED('api')}api.delete('/admin/alert-rules/123');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `${NEW_NAMED('api')}api.put(\`/admin/alert-rules/\${id}\`, body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
    ],
  });
});
