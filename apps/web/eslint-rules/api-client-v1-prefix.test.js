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

test('api-client-v1-prefix', () => {
  ruleTester.run('api-client-v1-prefix', rule, {
    valid: [
      // Correct prefix on each verb.
      { code: `apiClient.get('/api/v1/foo');` },
      { code: `apiClient.post('/api/v1/foo', body);` },
      { code: `apiClient.put('/api/v1/foo', body);` },
      { code: `apiClient.patch('/api/v1/foo', body);` },
      { code: `apiClient.delete('/api/v1/foo');` },
      { code: `apiClient.head('/api/v1/foo');` },
      { code: `apiClient.options('/api/v1/foo');` },
      // Template literal whose static head matches the prefix.
      { code: 'apiClient.get(`/api/v1/games/${id}`);' },
      { code: 'apiClient.get(`/api/v1/games/${id}/chunks/${c}`);' },
      // Dynamic path arguments — cannot statically verify, do not flag.
      { code: `apiClient.get(someVar);` },
      { code: `apiClient.post(buildUrl(id), body);` },
      { code: `apiClient.get(prefix + '/foo');` },
      // Path that is not absolute is out of scope (caller signals intent).
      { code: `apiClient.get('relative/path');` },
      { code: `apiClient.get('');` },
      // Method not in the HTTP verb list — different API surface.
      { code: `apiClient.fooBar('/foo');` },
      // Different client identifier — only `apiClient` is checked here.
      { code: `dashboardClient.get('/foo');` },
      { code: `httpClient.get('/foo');` },
      // Computed property (rare) is conservatively skipped.
      { code: `apiClient['get']('/foo');` },
      // Optional chaining — should still verify, but ESLint default parser
      // shows it as a CallExpression with a ChainExpression callee. Keep the
      // simple AST shape here as a no-op so we don't false-positive on
      // unrelated patterns. Production code uses `apiClient.get(...)` directly.
    ],
    invalid: [
      {
        code: `apiClient.get('/foo');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `apiClient.post('/users/123', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `apiClient.put('/catalog/games', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `apiClient.patch('/agents/popular', body);`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `apiClient.delete('/sessions/abc');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      // Template literal whose static head does NOT match the prefix.
      {
        code: 'apiClient.get(`/foo/${id}`);',
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: 'apiClient.post(`/users/${id}/data`, body);',
        errors: [{ messageId: 'missingPrefix' }],
      },
      // Almost-correct prefix but missing trailing slash → still wrong because
      // the proxy matches /api/v1/<anything> not /api/v1<anything>.
      {
        code: `apiClient.get('/api/v1catalog/trending');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
      {
        code: `apiClient.get('/api/v2/foo');`,
        errors: [{ messageId: 'missingPrefix' }],
      },
    ],
  });
});
