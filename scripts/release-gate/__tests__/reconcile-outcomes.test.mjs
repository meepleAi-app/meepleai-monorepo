// scripts/release-gate/__tests__/reconcile-outcomes.test.mjs
// Phase 2b (#1445) — unit tests for pure reconcileOutcomes (A4 silent + label).

import { describe, it, expect, vi } from "vitest";

import { reconcileOutcomes } from "../lib/auto-revert-events.mjs";

const NOW = new Date("2026-06-30T07:00:00Z");
const MERGE_SHA = "abc12345";
const OPENED_EVENT = {
  schemaVersion: 1,
  eventId: "opened-001",
  eventType: "revert_opened",
  timestamp: "2026-06-23T08:00:00Z",
  runUrl: "https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/42",
  mode: "live",
  originalPr: 1234,
  revertPr: 1235,
  mergeSha: MERGE_SHA,
  blockerCheck: { name: "Backend - Unit Tests", conclusion: "failure", checkRunUrl: "...", classifiedAt: "..." },
  decisionRationale: {},
  outcome: "true_positive_pending",
};

function buildPr(overrides = {}) {
  return {
    number: 1235,
    state: "merged",
    mergedAt: new Date("2026-06-23T08:05:00Z"),
    labels: ["auto-revert", "phase2b"],
    createdAt: new Date("2026-06-23T08:00:00Z"),
    ...overrides,
  };
}

describe("reconcileOutcomes — A4 silent confirmation + label override", () => {
  it("emits silent_confirmation event for PR mergiata >= 7gg ago, no label", () => {
    const pr = buildPr({ mergedAt: new Date("2026-06-23T07:00:00Z") }); // exactly 7gg before NOW
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(1);
    expect(events[0].eventType).toBe("outcome_updated");
    expect(events[0].newOutcome).toBe("true_positive_confirmed");
    expect(events[0].trigger).toBe("silent_confirmation_7d_elapsed");
  });

  it("does NOT emit event for PR mergiata < 7gg ago, no label", () => {
    const pr = buildPr({ mergedAt: new Date("2026-06-24T08:00:00Z") }); // 6gg before NOW
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(0);
  });

  it("emits false_positive event immediately when label revert-outcome:false-positive present (5gg ago)", () => {
    const pr = buildPr({
      mergedAt: new Date("2026-06-25T08:00:00Z"),
      labels: ["auto-revert", "phase2b", "revert-outcome:false-positive"],
    });
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(1);
    expect(events[0].newOutcome).toBe("false_positive");
    expect(events[0].trigger).toBe("label_explicit");
  });

  it("emits true_positive_confirmed immediately when label revert-outcome:true-positive present (5gg ago)", () => {
    const pr = buildPr({
      mergedAt: new Date("2026-06-25T08:00:00Z"),
      labels: ["auto-revert", "phase2b", "revert-outcome:true-positive"],
    });
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(1);
    expect(events[0].newOutcome).toBe("true_positive_confirmed");
    expect(events[0].trigger).toBe("label_explicit");
  });

  it("prioritizes false-positive label when BOTH labels present + emits warning", () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    const pr = buildPr({
      labels: ["auto-revert", "phase2b", "revert-outcome:false-positive", "revert-outcome:true-positive"],
    });
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events[0].newOutcome).toBe("false_positive");
    expect(warnSpy).toHaveBeenCalled();
    warnSpy.mockRestore();
  });

  it("does NOT re-emit when outcome_updated event already exists for this revertPr (idempotency)", () => {
    const alreadyFinalized = {
      ...OPENED_EVENT,
      eventId: "outcome-001",
      eventType: "outcome_updated",
      previousOutcome: "true_positive_pending",
      newOutcome: "true_positive_confirmed",
      trigger: "silent_confirmation_7d_elapsed",
      rationale: "previously confirmed",
    };
    const pr = buildPr({ mergedAt: new Date("2026-06-23T07:00:00Z") });
    const events = reconcileOutcomes([pr], [OPENED_EVENT, alreadyFinalized], NOW);
    expect(events.length).toBe(0);
  });

  it("excludes PRs with state=closed (not merged)", () => {
    const pr = buildPr({ state: "closed", mergedAt: null });
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(0);
  });

  it("excludes PRs with dry-run label", () => {
    const pr = buildPr({ labels: ["auto-revert", "phase2b", "dry-run"] });
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(0);
  });

  it("normalizes mergedAt when passed as ISO string (gh API format)", () => {
    const pr = buildPr({ mergedAt: "2026-06-23T07:00:00Z" }); // ISO string, not Date
    const events = reconcileOutcomes([pr], [OPENED_EVENT], NOW);
    expect(events.length).toBe(1);
    expect(events[0].trigger).toBe("silent_confirmation_7d_elapsed");
  });
});
