// scripts/release-gate/__tests__/integration-reconcile.test.mjs
// Phase 2b (#1445) — integration tests for reconcile-revert-outcomes.mjs.

import { describe, it, expect, vi, beforeEach } from "vitest";

import { computeMetrics } from "../reconcile-revert-outcomes.mjs";

describe("computeMetrics — AC-7 calculation", () => {
  const NOW = new Date("2026-07-23T08:00:00Z");
  const baseEvent = (overrides) => ({
    schemaVersion: 1,
    eventId: "x",
    eventType: "revert_opened",
    timestamp: NOW.toISOString(),
    runUrl: "url",
    mode: "live",
    originalPr: 1,
    revertPr: 2,
    mergeSha: "sha",
    blockerCheck: { name: "X", conclusion: "failure", checkRunUrl: "", classifiedAt: "" },
    decisionRationale: {},
    outcome: "true_positive_pending",
    ...overrides,
  });

  it("reports zero rate when no reverts in window", () => {
    const m = computeMetrics([], NOW);
    expect(m.total_reverts).toBe(0);
    expect(m.false_revert_rate).toBe(0);
    expect(m.breach).toBe(false);
  });

  it("reports breach when false_positive rate > 2%", () => {
    const events = [
      ...Array(40).fill(null).map((_, i) => baseEvent({ revertPr: i, eventType: "revert_opened" })),
      ...Array(38).fill(null).map((_, i) => baseEvent({ revertPr: i, eventType: "outcome_updated", newOutcome: "true_positive_confirmed", trigger: "label_explicit", previousOutcome: "true_positive_pending" })),
      ...Array(2).fill(null).map((_, i) => baseEvent({ revertPr: 100 + i, eventType: "outcome_updated", newOutcome: "false_positive", trigger: "label_explicit", previousOutcome: "true_positive_pending" })),
    ];
    const m = computeMetrics(events, NOW);
    expect(m.total_reverts).toBe(40);
    expect(m.false_positive).toBe(2);
    expect(m.false_revert_rate).toBe(0.05); // 2/40
    expect(m.breach).toBe(true); // > 0.02
  });

  it("excludes dry_run events from metrics", () => {
    const events = [
      baseEvent({ mode: "dry_run", eventType: "revert_opened" }),
      baseEvent({ mode: "live", eventType: "revert_opened", revertPr: 99 }),
    ];
    const m = computeMetrics(events, NOW);
    expect(m.total_reverts).toBe(1);
  });
});
