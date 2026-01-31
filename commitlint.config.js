/**
 * Commitlint Configuration
 *
 * Enforces Conventional Commits specification for this monorepo.
 * https://www.conventionalcommits.org/
 *
 * Commit format: <type>(<scope>): <subject>
 *
 * Types:
 *   feat     - New feature
 *   fix      - Bug fix
 *   docs     - Documentation only changes
 *   test     - Adding or correcting tests
 *   refactor - Code change that neither fixes a bug nor adds a feature
 *   perf     - Performance improvement
 *   ci       - Changes to CI configuration
 *   chore    - Other changes that don't modify src or test files
 *   style    - Code style changes (formatting, missing semicolons, etc.)
 *   build    - Changes that affect the build system or external dependencies
 *   revert   - Reverts a previous commit
 *
 * Scopes (optional):
 *   frontend, backend, api, web, infra, docs, deps, auth, rag, admin, config
 *
 * Examples:
 *   feat(frontend): add dark mode toggle
 *   fix(api): resolve null reference in auth handler
 *   docs: update README with new installation steps
 *   test(backend): add unit tests for RagService
 */

module.exports = {
  // Note: Not extending config-conventional due to monorepo module resolution
  // All necessary rules are defined inline below
  rules: {
    // Type must be one of the allowed values
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'test',
        'refactor',
        'perf',
        'ci',
        'chore',
        'style',
        'build',
        'revert',
      ],
    ],

    // Type must be lowercase
    'type-case': [2, 'always', 'lower-case'],

    // Type is required
    'type-empty': [2, 'never'],

    // Scope must be lowercase if provided
    'scope-case': [2, 'always', 'lower-case'],

    // Subject is required
    'subject-empty': [2, 'never'],

    // Subject must not end with period
    'subject-full-stop': [2, 'never', '.'],

    // Subject max length
    'subject-max-length': [2, 'always', 72],

    // Header max length (type + scope + subject)
    'header-max-length': [2, 'always', 100],

    // Body max line length (warning only)
    'body-max-line-length': [1, 'always', 100],

    // Footer max line length (warning only)
    'footer-max-line-length': [1, 'always', 100],
  },
};
