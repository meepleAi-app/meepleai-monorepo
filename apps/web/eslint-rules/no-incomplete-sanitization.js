/**
 * ESLint Custom Rule: no-incomplete-sanitization
 *
 * Prevents incomplete string sanitization that can lead to injection vulnerabilities.
 *
 * **Problem:**
 * Using `.replace(/"/g, '\\"')` alone doesn't escape backslashes, allowing bypass attacks.
 * Example attack: input `test\"` becomes `test\\"` which when parsed becomes `test"` (unescaped).
 *
 * **Solution:**
 * Always escape backslashes BEFORE quotes, or use the provided utility functions:
 * - `escapePrometheusLabelValue()` for Prometheus metrics
 * - `sanitizeHtml()` for HTML content
 * - General rule: escape \ before "
 *
 * **References:**
 * - CWE-116: Incomplete string escaping or encoding
 * - CodeQL: js/incomplete-sanitization
 */

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Prevent incomplete sanitization that can lead to injection vulnerabilities',
      category: 'Security',
      recommended: true,
      url: 'https://codeql.github.com/codeql-query-help/javascript/js-incomplete-sanitization/',
    },
    messages: {
      incompleteSanitization:
        'Incomplete sanitization detected. Use a proper escaping utility function instead of manual `.replace()`. ' +
        'For Prometheus metrics, use `escapePrometheusLabelValue()` from `./prometheusUtils`. ' +
        'Manual escaping with `.replace(/"/g, \'\\"\')` is unsafe because it doesn\'t escape backslashes first. ' +
        '(CWE-116: Incomplete string escaping)',
    },
    schema: [],
  },

  create(context) {
    return {
      CallExpression(node) {
        // Check for .replace() method calls
        if (
          node.callee.type === 'MemberExpression' &&
          node.callee.property.name === 'replace' &&
          node.arguments.length >= 2
        ) {
          const firstArg = node.arguments[0];
          const secondArg = node.arguments[1];

          // Check if it's replacing quotes: /"/g or /'/g
          const isReplacingQuotes =
            firstArg.type === 'Literal' &&
            firstArg.regex &&
            (firstArg.regex.pattern === '"' ||
              firstArg.regex.pattern === "'" ||
              firstArg.regex.pattern === '`');

          // Check if the replacement is an escape sequence
          const isEscapingQuotes =
            secondArg.type === 'Literal' &&
            (secondArg.value === '\\"' ||
              secondArg.value === "\\'" ||
              secondArg.value === '\\`');

          if (isReplacingQuotes && isEscapingQuotes) {
            context.report({
              node,
              messageId: 'incompleteSanitization',
            });
          }

          // Also check for common patterns like .replace(/\\/g, '\\\\') without following quote escape
          // This is more complex and would require flow analysis, so we keep it simple for now
        }
      },
    };
  },
};
