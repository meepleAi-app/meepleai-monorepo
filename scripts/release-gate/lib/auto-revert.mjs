// scripts/release-gate/lib/auto-revert.mjs
// Phase 2b (#1445) — pure decision function for auto-revert workflow.
//
// Pure: no I/O, no octokit. The caller (run-auto-revert.mjs) hydrates input
// from GitHub APIs + JSONL state + .github/release-gates.yml + clock.
//
// Tested by __tests__/auto-revert.test.mjs.

import { findActiveRevert } from "./auto-revert-events.mjs";

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

/**
 * AC-12 — a blocker is "new" iff it was NOT marked override_accepted in the
 * pre-merge bot comment. Exported for unit testability.
 */
export function isNewBlocker(checkName, preMergeBotComment) {
  if (!preMergeBotComment || !Array.isArray(preMergeBotComment.classifications)) {
    return true; // no comment / unknown → treat as new (conservative)
  }
  const match = preMergeBotComment.classifications.find(c => c.check_name === checkName);
  if (!match) return true;
  return !match.override_accepted;
}

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

  // [5] AC-2 — SHA pin (HEAD must still equal mergeSha at decision time)
  if (input.currentHeadSha !== input.latestMergedRelease.mergeSha) {
    return {
      action: "abort",
      reason: "sha_moved_staleness",
      rationale: {
        currentHeadSha: input.currentHeadSha,
        expectedSha: input.latestMergedRelease.mergeSha,
      },
    };
  }

  // [6] noop_no_blockers — nothing to revert
  if (input.blockers.length === 0) {
    return { action: "noop_no_blockers" };
  }

  // [7] AC-12 — filter NEW blockers only
  const newBlockers = input.blockers.filter(b => isNewBlocker(b.name, input.preMergeBotComment));
  if (newBlockers.length === 0) {
    return {
      action: "abort",
      reason: "skipped_pre_existing",
      rationale: { allBlockersWerePreMergeOverridden: input.blockers.map(b => b.name) },
    };
  }

  // [8] B5 — idempotency check (findActiveRevert imported from auto-revert-events.mjs)
  const targetBlocker = newBlockers[0];
  const activeRevert = findActiveRevert(input.jsonlEvents, input.latestMergedRelease.mergeSha, targetBlocker.name);
  if (activeRevert) {
    return { action: "noop_idempotent", existingRevertPr: activeRevert.revertPr };
  }

  // [10] DECISION: open_revert
  return {
    action: "open_revert",
    mergeSha: input.latestMergedRelease.mergeSha,
    blockerCheck: targetBlocker,
    rationale: { newBlockerCount: newBlockers.length, allNewBlockers: newBlockers.map(b => b.name) },
  };
}
