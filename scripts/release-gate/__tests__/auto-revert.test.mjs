// scripts/release-gate/__tests__/auto-revert.test.mjs
// Phase 2b (#1445) — unit tests for pure decideRevertAction.

import { describe, it, expect } from "vitest";

import { decideRevertAction, COOLDOWN_MS_DEFAULT } from "../lib/auto-revert.mjs";

const MERGE_TIME = new Date("2026-06-23T08:00:00Z");
const LATEST_MERGED_RELEASE = {
  prNumber: 1234,
  mergeSha: "abc12345",
  mergeTime: MERGE_TIME,
  isAutoRevertPr: false,
};
const BLOCKER = {
  name: "Backend - Unit Tests",
  conclusion: "failure",
  checkRunUrl: "https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/41",
};

function baseInput(overrides = {}) {
  return {
    killSwitchEnabled: true,
    dryRunMode: false,
    latestMergedRelease: LATEST_MERGED_RELEASE,
    currentHeadSha: "abc12345",
    cooldownMs: COOLDOWN_MS_DEFAULT,
    now: new Date(MERGE_TIME.getTime() + 16 * 60 * 1000), // 16min elapsed
    blockers: [BLOCKER],
    preMergeBotComment: null,
    fixForwards: [],
    jsonlEvents: [],
    ...overrides,
  };
}

describe("constants", () => {
  it("COOLDOWN_MS_DEFAULT is 900_000 (15min)", () => {
    expect(COOLDOWN_MS_DEFAULT).toBe(900_000);
  });
});

describe("AC-4 — kill switch", () => {
  it("returns abort+kill_switch_active when killSwitchEnabled=false", () => {
    const result = decideRevertAction(baseInput({ killSwitchEnabled: false }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("kill_switch_active");
  });

  it("kill switch takes precedence over all other checks (even with valid blockers)", () => {
    const result = decideRevertAction(baseInput({
      killSwitchEnabled: false,
      blockers: [BLOCKER, BLOCKER, BLOCKER],
    }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("kill_switch_active");
  });
});

describe("no recent merge", () => {
  it("returns noop_no_recent_merge when latestMergedRelease is null", () => {
    const result = decideRevertAction(baseInput({ latestMergedRelease: null }));
    expect(result.action).toBe("noop_no_recent_merge");
  });
});

describe("AC-8 — cascade prevention (two-key check)", () => {
  it("returns abort+cascade_prevented when latest PR isAutoRevertPr=true (title prefix + body link)", () => {
    const result = decideRevertAction(baseInput({
      latestMergedRelease: { ...LATEST_MERGED_RELEASE, isAutoRevertPr: true },
    }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("cascade_prevented");
  });

  it("proceeds normally when isAutoRevertPr=false (single-key insufficient handled upstream)", () => {
    const result = decideRevertAction(baseInput({
      latestMergedRelease: { ...LATEST_MERGED_RELEASE, isAutoRevertPr: false },
    }));
    expect(result.action).toBe("open_revert");
  });
});

describe("AC-1 — cooldown enforcement", () => {
  it("aborts when cooldown not elapsed (10min)", () => {
    const result = decideRevertAction(baseInput({
      now: new Date(MERGE_TIME.getTime() + 10 * 60 * 1000),
    }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("cooldown_not_elapsed");
  });

  it("proceeds exactly at boundary (15:00)", () => {
    const result = decideRevertAction(baseInput({
      now: new Date(MERGE_TIME.getTime() + 15 * 60 * 1000),
    }));
    expect(result.action).toBe("open_revert");
  });

  it("aborts just before boundary (14:59)", () => {
    const result = decideRevertAction(baseInput({
      now: new Date(MERGE_TIME.getTime() + 14 * 60 * 1000 + 59 * 1000),
    }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("cooldown_not_elapsed");
  });
});

describe("AC-2 — SHA pin (staleness)", () => {
  it("aborts when currentHeadSha differs from latestMergedRelease.mergeSha", () => {
    const result = decideRevertAction(baseInput({
      currentHeadSha: "def67890", // different from "abc12345"
    }));
    expect(result.action).toBe("abort");
    expect(result.reason).toBe("sha_moved_staleness");
    expect(result.rationale.currentHeadSha).toBe("def67890");
    expect(result.rationale.expectedSha).toBe("abc12345");
  });

  it("proceeds when currentHeadSha matches mergeSha", () => {
    const result = decideRevertAction(baseInput({
      currentHeadSha: "abc12345",
    }));
    expect(result.action).toBe("open_revert");
  });
});
