/**
 * ESLint rule: require `/api/v1/` prefix on apiClient HTTP calls.
 *
 * The browser-side `HttpClient.baseUrl` is `''` (see
 * `apps/web/src/lib/api/core/httpClient.ts:54-56`). Requests therefore go
 * to a relative URL on the Next.js host (`localhost:3000`). The
 * `apps/web/src/app/api/v1/[...path]/route.ts` catch-all proxy forwards
 * only paths that begin with `/api/v1/` to the backend. Any other absolute
 * path lands on Next.js itself and 404s.
 *
 * This rule statically enforces the convention "every `apiClient.get/post/
 * put/patch/delete/head/options('/...')` literal MUST start with
 * `/api/v1/`". Dynamic paths (variables, function calls, BinaryExpression)
 * are intentionally skipped to avoid false positives — those need to be
 * vetted in code review.
 *
 * Refs:
 *   - PR #1229: fix(web) add /api/v1 prefix to 6 discover hooks (refs #1160)
 *   - apps/web/src/lib/api/core/httpClient.ts:54-56 (baseUrl='' rationale)
 *   - apps/web/src/app/api/v1/[...path]/route.ts (proxy catch-all)
 *
 * To suppress in a legitimate exception:
 *   // eslint-disable-next-line local/api-client-v1-prefix -- <reason>
 */

'use strict';

const API_CLIENT_NAME = 'apiClient';
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
    return {
      CallExpression(node) {
        const callee = node.callee;

        // Match exactly `apiClient.<method>(...)` (not computed, not chained).
        if (
          !callee ||
          callee.type !== 'MemberExpression' ||
          callee.computed ||
          callee.object.type !== 'Identifier' ||
          callee.object.name !== API_CLIENT_NAME ||
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
