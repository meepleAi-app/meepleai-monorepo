// scripts/release-gate/__tests__/auto-revert-events.test.mjs
// Phase 2b (#1445) — unit tests for pure JSONL event helpers.

import { describe, it, expect } from "vitest";

import {
  EVENT_SCHEMA_VERSION,
  serializeEvent,
  parseEventLog,
  findActiveRevert,
  buildEventId,
} from "../lib/auto-revert-events.mjs";

const SAMPLE_REVERT_OPENED = {
  schemaVersion: 1,
  eventId: "test-id-001",
  eventType: "revert_opened",
  timestamp: "2026-06-23T08:00:00Z",
  runUrl: "https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/42",
  mode: "live",
  originalPr: 1234,
  revertPr: 1235,
  mergeSha: "abc12345",
  blockerCheck: {
    name: "Backend - Unit Tests",
    conclusion: "failure",
    checkRunUrl: "https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/41",
    classifiedAt: "2026-06-23T07:55:00Z",
  },
  decisionRationale: {
    cooldownElapsedMs: 900_001,
    fixForwardCheck: "none",
    shaPinned: "abc12345",
    isNewBlocker: true,
    cascadeCheck: "pass",
  },
  outcome: "true_positive_pending",
};

describe("constants", () => {
  it("EVENT_SCHEMA_VERSION is 1", () => {
    expect(EVENT_SCHEMA_VERSION).toBe(1);
  });
});

describe("serializeEvent + parseEventLog round-trip", () => {
  it("serializeEvent produces a single line with trailing newline", () => {
    const line = serializeEvent(SAMPLE_REVERT_OPENED);
    expect(typeof line).toBe("string");
    expect(line.endsWith("\n")).toBe(true);
    expect(line.split("\n").filter(Boolean).length).toBe(1);
  });

  it("parseEventLog(emptyString) returns empty array", () => {
    expect(parseEventLog("")).toEqual([]);
  });

  it("parseEventLog round-trips serialized event with all fields preserved", () => {
    const text = serializeEvent(SAMPLE_REVERT_OPENED);
    const parsed = parseEventLog(text);
    expect(parsed).toEqual([SAMPLE_REVERT_OPENED]);
  });

  it("parseEventLog skips malformed lines + logs warning (no throw)", () => {
    const text = serializeEvent(SAMPLE_REVERT_OPENED) + "this is not json\n" + serializeEvent(SAMPLE_REVERT_OPENED);
    const parsed = parseEventLog(text);
    expect(parsed.length).toBe(2);
  });
});

describe("buildEventId — deterministic from (eventType, mergeSha, ts)", () => {
  it("returns same id for same triple", () => {
    const id1 = buildEventId("revert_opened", "abc12345", "2026-06-23T08:00:00Z");
    const id2 = buildEventId("revert_opened", "abc12345", "2026-06-23T08:00:00Z");
    expect(id1).toBe(id2);
  });

  it("returns different ids for different eventTypes", () => {
    const id1 = buildEventId("revert_opened", "abc12345", "2026-06-23T08:00:00Z");
    const id2 = buildEventId("revert_aborted", "abc12345", "2026-06-23T08:00:00Z");
    expect(id1).not.toBe(id2);
  });
});
