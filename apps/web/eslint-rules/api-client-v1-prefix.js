/**
 * ESLint rule: require `/api/v1/` prefix on HttpClient HTTP calls.
 *
 * The browser-side `HttpClient.baseUrl` is `''` (see
 * `apps/web/src/lib/api/core/httpClient.ts:54-56`). Requests therefore go
 * to a relative URL on the Next.js host (`localhost:3000`). The
 * `apps/web/src/app/api/v1/[...path]/route.ts` catch-all proxy forwards
 * only paths that begin with `/api/v1/` to the backend. Any other absolute
 * path lands on Next.js itself and 404s.
 *
 * This rule statically enforces the convention "every
 * `<client>.get/post/put/patch/delete/head/options('/...')` literal MUST
 * start with `/api/v1/`" where `<client>` is any local identifier that
 * either (a) was imported as `apiClient` from a `client` module or (b)
 * was instantiated with `new HttpClient(...)` in the same file. Dynamic
 * paths (variables, function calls, BinaryExpression) are intentionally
 * skipped to avoid false positives — those need to be vetted in code
 * review.
 *
 * The two-mode tracking is required because the codebase uses both
 * patterns: the shared singleton `apiClient` (from
 * `apps/web/src/lib/api/client.ts`) and local per-feature
 * `const httpClient = new HttpClient()` instances (43+ files in admin/
 * tabs, settings pages, etc.). All consume the same baseUrl='' contract,
 * so all must respect the `/api/v1/` prefix.
 *
 * Refs:
 *   - PR #1229: fix(web) add /api/v1 prefix to 6 discover hooks (refs #1160)
 *   - PR #1230: extend rule to track `new HttpClient()` instances
 *   - apps/web/src/lib/api/core/httpClient.ts:54-56 (baseUrl='' rationale)
 *   - apps/web/src/app/api/v1/[...path]/route.ts (proxy catch-all)
 *
 * To suppress in a legitimate exception:
 *   // eslint-disable-next-line local/api-client-v1-prefix -- <reason>
 */

'use strict';

const HTTP_METHODS = new Set(['get', 'post', 'put', 'patch', 'delete', 'head', 'options']);
const REQUIRED_PREFIX = '/api/v1/';

/**
 * Extract the static head of the path argument, or null when the path is
 * fully dynamic and cannot be statically reasoned about.
 *
 *   '/api/v1/foo'           → '/api/v1/foo'
 *   `/api/v1/games/${id}`   → '/api/v1/games/'
 *   `${BASE}/games/${id}`   → ''  (first quasi is empty)
 *   someVar                 → null
 *   prefix + '/foo'         → null
 */
function extractStaticPrefix(node) {
  if (!node) return null;
  if (node.type === 'Literal' && typeof node.value === 'string') {
    return node.value;
  }
  if (node.type === 'TemplateLiteral' && node.quasis.length > 0) {
    // `quasis[0].value.raw` is the static text before the first ${expr}.
    return node.quasis[0].value.raw || '';
  }
  return null;
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Require /api/v1/ prefix on apiClient.{get,post,put,patch,delete,head,options} path literals.',
      recommended: true,
    },
    schema: [],
    messages: {
      missingPrefix:
        'apiClient.{{method}}() path must start with "{{prefix}}" — browser baseUrl is empty and only paths under /api/v1/ are proxied to the backend. Got: "{{actual}}".',
    },
  },
  create(context) {
    // Per-file set of local identifier names that resolve to an HttpClient
    // instance. Populated by:
    //   - ImportDeclaration: `import { apiClient [as X] } from '.../client'`
    //   - VariableDeclarator: `const X = new HttpClient(...)`
    // ESLint walks the AST top-down so imports and declarations are seen
    // before any CallExpression on the file.
    const trackedNames = new Set();

    // Heuristic: only trust `apiClient` imports whose source path ends in
    // `client` (e.g. `@/lib/api/client`, `./client`, `../client`). Stops
    // false positives from unrelated modules that happen to export an
    // identifier named `apiClient`.
    function isClientModuleSource(source) {
      if (typeof source !== 'string') return false;
      return /(^|\/)client(\.[tj]sx?)?$/.test(source);
    }

    return {
      ImportDeclaration(node) {
        if (!isClientModuleSource(node.source && node.source.value)) return;
        for (const specifier of node.specifiers) {
          if (
            specifier.type === 'ImportSpecifier' &&
            specifier.imported &&
            specifier.imported.name === 'apiClient' &&
            specifier.local &&
            specifier.local.name
          ) {
            trackedNames.add(specifier.local.name);
          }
        }
      },

      VariableDeclarator(node) {
        if (
          node.init &&
          node.init.type === 'NewExpression' &&
          node.init.callee.type === 'Identifier' &&
          node.init.callee.name === 'HttpClient' &&
          node.id.type === 'Identifier'
        ) {
          trackedNames.add(node.id.name);
        }
      },

      CallExpression(node) {
        const callee = node.callee;

        // Match `<tracked-name>.<method>(...)` (not computed).
        if (
          !callee ||
          callee.type !== 'MemberExpression' ||
          callee.computed ||
          callee.object.type !== 'Identifier' ||
          !trackedNames.has(callee.object.name) ||
          callee.property.type !== 'Identifier' ||
          !HTTP_METHODS.has(callee.property.name)
        ) {
          return;
        }

        const firstArg = node.arguments[0];
        const staticHead = extractStaticPrefix(firstArg);

        // Skip dynamic paths (null) and non-absolute paths (does not start
        // with `/`). Empty string is treated as non-absolute (caller intent
        // is unclear and the rule should not over-fire).
        if (staticHead === null) return;
        if (!staticHead.startsWith('/')) return;

        // Correct prefix — must include the trailing slash to avoid
        // matching paths like `/api/v1catalog/...` that the proxy rejects.
        if (staticHead.startsWith(REQUIRED_PREFIX)) return;

        context.report({
          node: firstArg,
          messageId: 'missingPrefix',
          data: {
            method: callee.property.name,
            prefix: REQUIRED_PREFIX,
            actual: staticHead.length > 60 ? `${staticHead.slice(0, 57)}...` : staticHead,
          },
        });
      },
    };
  },
};
