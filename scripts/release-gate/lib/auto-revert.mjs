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

  // Step [2] No recent merged release PR
  if (!input.latestMergedRelease) {
    return { action: "noop_no_recent_merge" };
  }

  // Step [3] AC-8 cascade prevention (two-key check applied upstream by caller; we honor the flag)
  if (input.latestMergedRelease.isAutoRevertPr) {
    return {
      action: "abort",
      reason: "cascade_prevented",
      rationale: { originalPr: input.latestMergedRelease.prNumber, mergeSha: input.latestMergedRelease.mergeSha },
    };
  }

  // [4] AC-1 — cooldown elapsed
  const elapsedMs = input.now.getTime() - input.latestMergedRelease.mergeTime.getTime();
  if (elapsedMs < input.cooldownMs) {
    return {
      action: "abort",
      reason: "cooldown_not_elapsed",
      rationale: { elapsedMs, cooldownMs: input.cooldownMs },
    };
  }

  // Step [10] DECISION: open_revert (placeholder until subsequent tasks add steps [4]-[9])
  return {
    action: "open_revert",
    mergeSha: input.latestMergedRelease.mergeSha,
    blockerCheck: input.blockers[0] ?? null,
    rationale: {},
  };
}
