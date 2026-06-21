// scripts/release-gate/lib/auto-revert.mjs
// Phase 2b (#1445) — pure decision function for auto-revert workflow.
//
// Pure: no I/O, no octokit. The caller (run-auto-revert.mjs) hydrates input
// from GitHub APIs + JSONL state + .github/release-gates.yml + clock.
//
// Tested by __tests__/auto-revert.test.mjs.

export const COOLDOWN_MS_DEFAULT = 15 * 60 * 1000; // 900_000

/**
 * @typedef {Object} DecisionInput
 * @property {boolean} killSwitchEnabled
 * @property {boolean} dryRunMode
 * @property {Object|null} latestMergedRelease
 * @property {string} currentHeadSha
 * @property {number} cooldownMs
 * @property {Date} now
 * @property {Array} blockers
 * @property {Object|null} preMergeBotComment
 * @property {Array} fixForwards
 * @property {Array} jsonlEvents
 */

/**
 * @typedef {Object} DecisionOutput
 * @property {'open_revert'|'abort'|'noop_idempotent'|'noop_no_blockers'|'noop_no_recent_merge'} action
 * @property {string} [reason]
 * @property {Object} [rationale]
 */

export function decideRevertAction(input) {
  // Step [1] AC-4 — kill switch FIRST (zero external API calls when off)
  if (!input.killSwitchEnabled) {
    return { action: "abort", reason: "kill_switch_active" };
  }

  // Placeholder return — replaced by steps [2]-[10] across Tasks 9-15 (TDD incremental).
  // Current skeleton scope: ONLY step [1] kill switch.
  return { action: "noop_no_blockers" };
}
