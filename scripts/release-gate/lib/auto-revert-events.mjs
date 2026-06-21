// scripts/release-gate/lib/auto-revert-events.mjs
// Phase 2b (#1445) — pure JSONL event helpers.
// Tested by __tests__/auto-revert-events.test.mjs.
//
// Pure: no I/O, no octokit. Caller (run-auto-revert.mjs / reconcile-revert-outcomes.mjs)
// is responsible for reading/writing the file on the side branch.

import { createHash } from "node:crypto";

export const EVENT_SCHEMA_VERSION = 1;

export const EVENT_TYPES = Object.freeze([
  "revert_opened",
  "revert_aborted",
  "revert_aborted_at_merge",
  "outcome_updated",
]);

/**
 * Deterministic event id from (eventType, mergeSha, timestamp).
 * Stable across runs → useful for debugging cross-tick.
 */
export function buildEventId(eventType, mergeSha, timestamp) {
  const hash = createHash("sha256").update(`${eventType}|${mergeSha}|${timestamp}`).digest("hex");
  return hash.slice(0, 26);
}

/**
 * Serialize a single event to one JSONL line (with trailing newline).
 */
export function serializeEvent(event) {
  return JSON.stringify(event) + "\n";
}

/**
 * Parse a JSONL log text into typed events.
 * Skips malformed lines + emits console.warn (does NOT throw).
 */
export function parseEventLog(text) {
  if (!text) return [];
  const events = [];
  const lines = text.split("\n");
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line) continue;
    try {
      const parsed = JSON.parse(line);
      events.push(parsed);
    } catch (err) {
      console.warn(`[auto-revert-events] line ${i + 1} malformed, skipping: ${err.message}`);
    }
  }
  return events;
}

/**
 * B5 idempotency — returns the active revert_opened event for (mergeSha, checkName)
 * or null if no active revert exists.
 *
 * Lock semantics:
 *   - revert_opened → state = active_revert (returned)
 *   - revert_aborted_at_merge for same revertPr → state = closed (lock released, returns null)
 *   - revert_aborted (pre-create) → state = aborted (terminal, never affected lock)
 *   - outcome_updated → state stays active (terminal outcome does NOT unlock — prevents re-revert post-confirmation)
 */
export function findActiveRevert(events, mergeSha, checkName) {
  const matching = events
    .filter(e => e.mergeSha === mergeSha && (e.blockerCheck?.name === checkName))
    .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());

  let active = null;
  for (const ev of matching) {
    if (ev.eventType === "revert_opened") {
      active = ev;
    } else if (ev.eventType === "revert_aborted_at_merge" && active && ev.revertPr === active.revertPr) {
      active = null; // race-abort released the lock
    }
    // outcome_updated and revert_aborted don't affect the lock
  }
  return active;
}

const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * A4 outcome lifecycle — pure reconciler.
 *
 * For each eligible revert PR (state=merged + labeled auto-revert+phase2b + NOT dry-run):
 *   - Skip if outcome_updated event already exists for this revertPr (idempotent)
 *   - Priority: label revert-outcome:false-positive → emit false_positive (label_explicit)
 *   - Priority: label revert-outcome:true-positive → emit true_positive_confirmed (label_explicit)
 *   - Else: mergedAt + 7gg elapsed → emit true_positive_confirmed (silent_confirmation_7d_elapsed)
 *   - Else: pending (no event this run)
 *
 * Returns new OutcomeUpdatedEvent[] to be appended to JSONL.
 *
 * @param {Array<{number, state, mergedAt: Date|string, labels: string[], createdAt: Date|string}>} revertPRs
 * @param {Array<Event>} events  existing JSONL events
 * @param {Date} now
 */
export function reconcileOutcomes(revertPRs, events, now) {
  const newEvents = [];

  for (const pr of revertPRs) {
    if (pr.state !== "merged") continue;
    if (!pr.labels.includes("auto-revert")) continue;
    if (!pr.labels.includes("phase2b")) continue;
    if (pr.labels.includes("dry-run")) continue;

    const openedEvent = events.find(e => e.eventType === "revert_opened" && e.revertPr === pr.number);
    if (!openedEvent) continue; // drift tolerance — PR existed before JSONL adoption

    const alreadyFinalized = events.some(
      e => e.eventType === "outcome_updated" && e.revertPr === pr.number,
    );
    if (alreadyFinalized) continue;

    const hasFalse = pr.labels.includes("revert-outcome:false-positive");
    const hasTrue = pr.labels.includes("revert-outcome:true-positive");

    let newOutcome;
    let trigger;
    let rationale = null;

    if (hasFalse && hasTrue) {
      console.warn(
        `[reconcile-outcomes] PR #${pr.number} has BOTH revert-outcome labels — defaulting to false-positive`,
      );
      newOutcome = "false_positive";
      trigger = "label_explicit";
    } else if (hasFalse) {
      newOutcome = "false_positive";
      trigger = "label_explicit";
    } else if (hasTrue) {
      newOutcome = "true_positive_confirmed";
      trigger = "label_explicit";
    } else {
      // Silent confirmation gate
      // Normalize mergedAt: accept Date or ISO string (gh API returns string)
      const mergedAt = pr.mergedAt instanceof Date ? pr.mergedAt : new Date(pr.mergedAt);
      const elapsedMs = now.getTime() - mergedAt.getTime();
      if (elapsedMs < SEVEN_DAYS_MS) continue; // < 7gg → pending
      newOutcome = "true_positive_confirmed";
      trigger = "silent_confirmation_7d_elapsed";
      rationale = `auto-confirmed: no operator override label after ${Math.floor(elapsedMs / 86400000)}d`;
    }

    const ts = now.toISOString();
    newEvents.push({
      schemaVersion: EVENT_SCHEMA_VERSION,
      eventId: buildEventId("outcome_updated", openedEvent.mergeSha, ts),
      eventType: "outcome_updated",
      timestamp: ts,
      runUrl: process.env.GITHUB_SERVER_URL && process.env.GITHUB_REPOSITORY && process.env.GITHUB_RUN_ID
        ? `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`
        : "local",
      mode: openedEvent.mode,
      originalPr: openedEvent.originalPr,
      revertPr: pr.number,
      mergeSha: openedEvent.mergeSha,
      previousOutcome: "true_positive_pending",
      newOutcome,
      trigger,
      rationale,
    });
  }

  return newEvents;
}
