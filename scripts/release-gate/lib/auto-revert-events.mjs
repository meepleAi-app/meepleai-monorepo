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
