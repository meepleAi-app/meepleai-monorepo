/**
 * Custom ESLint Rules for Testing Best Practices
 *
 * To enable, add to .eslintrc.json:
 * {
 *   "extends": ["./.eslintrc-testing-rules.js"]
 * }
 */

module.exports = {
  rules: {
    // Prevent use of container.firstChild anti-pattern
    'testing-library/no-container': 'warn',
    'testing-library/no-node-access': 'warn',

    // Prefer semantic queries
    'testing-library/prefer-screen-queries': 'error',

    // Ensure proper async handling
    'testing-library/await-async-queries': 'error',
    'testing-library/await-async-utils': 'error',

    // Prevent getBy in waitFor
    'testing-library/no-wait-for-multiple-assertions': 'error',

    // Prefer user-event over fireEvent
    'testing-library/prefer-user-event': 'warn',

    // Ensure proper cleanup
    'testing-library/no-render-in-lifecycle': 'error',

    // Prefer findBy for async elements
    'testing-library/prefer-find-by': 'error',

    // No debugging utilities in committed code
    'testing-library/no-debugging-utils': 'warn',

    // Prefer presence assertions
    'testing-library/prefer-presence-assertions': 'error',
  },

  // Override for specific test patterns
  overrides: [
    {
      // Allow container for specific cases (snapshots, styling)
      files: ['**/*.snapshot.test.{ts,tsx}', '**/*Snapshot.test.{ts,tsx}'],
      rules: {
        'testing-library/no-container': 'off',
        'testing-library/no-node-access': 'off',
      },
    },
  ],
};
