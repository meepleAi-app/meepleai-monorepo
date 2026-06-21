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
 * Stub for Task 6. Real implementation will scan events for active reverts
 * (those without a terminal outcome).
 */
export function findActiveRevert() {
  return null;
}
