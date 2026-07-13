/**
 * Aggregator plugin manifest for the `local/*` custom ESLint rules.
 *
 * Per-rule modules are imported individually in `apps/web/eslint.config.mjs`
 * and wired under the `local` plugin namespace inline. This manifest is the
 * canonical place to add new rules so a single import keeps working should
 * the config later switch to the standard `plugins: { local: <pluginObject> }`
 * shape.
 *
 * Adding a new rule:
 *   1. Drop `<rule-name>.js` and `<rule-name>.test.js` in this folder.
 *   2. Export it here under the rule's kebab-case name.
 *   3. Register in eslint.config.mjs with the desired severity.
 */

'use strict';

const noStoreScoresDirect = require('./no-store-scores-direct.js');
const preferUseGameTitle = require('./prefer-use-game-title.js');
const noStandaloneCardRenderer = require('./no-standalone-card-renderer.js');

module.exports = {
  rules: {
    'no-store-scores-direct': noStoreScoresDirect,
    'prefer-use-game-title': preferUseGameTitle,
    'no-standalone-card-renderer': noStandaloneCardRenderer,
  },
};
