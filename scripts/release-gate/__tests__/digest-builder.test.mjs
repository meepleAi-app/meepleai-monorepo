import { describe, it, expect } from "vitest";

import { buildDigest, isoWeek, EMPTY_STATE } from "../lib/digest-builder.mjs";

// Frozen "now" anchor — Monday 2026-05-25 at 09:00 Europe/Rome = ISO week 2026-W22.
const NOW = new Date("2026-05-25T08:00:00Z");

function makePr({ number, title, mergedAt, baseRef = "main-staging", botFailures = [] }) {
  return { number, title, mergedAt, baseRef, botFailures };
}

function makeFailure(check_name, severity = "warning", owner = "qa") {
  return {
    check_name,
    severity,
    owner,
    override_path: "exception-comment",
    notes: "test note",
    is_unknown: false,
  };
}

const GATES_DEFAULT = {
  bot: {
    phase2c: {
      enabled: true,
      escalation_threshold_weeks: 4,
    },
  },
};

describe("buildDigest — base behavior", () => {
  it("(matrix #1) empty window: no PRs, no warnings → 'all green' action=post, no state PR", () => {
    const result = buildDigest({ prs: [], prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.action).toBe("post");
    expect(result.slackMessage).toContain("No release-gate warnings this week");
    expect(result.openStatePr).toBe(false);
  });

  it("(matrix #2) all blockers, zero warnings → 'no warnings this week', no state PR", () => {
    const prs = [
      makePr({
        number: 100,
        title: "release: X",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Backend - Unit Tests", "blocker", "backend-dev")],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.action).toBe("post");
    expect(result.slackMessage).toContain("No release-gate warnings this week");
    expect(result.openStatePr).toBe(false);
  });

  it("filters to warning-tier ONLY (ignores informational + blocker)", () => {
    const prs = [
      makePr({
        number: 101,
        title: "release: Y",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [
          makeFailure("Backend - Unit Tests", "blocker", "backend-dev"),
          makeFailure("Frontend - A11y E2E", "warning", "qa"),
          makeFailure("Lychee Link Check", "informational", "devops"),
        ],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.action).toBe("post");
    expect(result.slackMessage).toContain("Frontend - A11y E2E");
    expect(result.slackMessage).not.toContain("Backend - Unit Tests");
    expect(result.slackMessage).not.toContain("Lychee Link Check");
  });
});

describe("buildDigest — delta tracking (AC-4)", () => {
  it("classifies new warnings (not in prev state) as new_this_week", () => {
    const prs = [
      makePr({
        number: 200,
        title: "release Z",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Lighthouse CI", "warning", "frontend-dev")],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.deltas.new_this_week).toBe(1);
    expect(result.deltas.persisting).toBe(0);
    expect(result.deltas.resolved_this_week).toBe(0);
  });

  it("classifies warnings present in prev state as persisting (increments weeksSeen)", () => {
    const prevState = {
      schemaVersion: 1,
      lastWeekISO: "2026-W21",
      warningsByCheck: {
        "Frontend - A11y E2E": {
          owner: "qa",
          firstSeenWeek: "2026-W20",
          weeksSeen: 2,
          lastSeenWeek: "2026-W21",
        },
      },
    };
    const prs = [
      makePr({
        number: 300,
        title: "release A",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates: GATES_DEFAULT });
    expect(result.deltas.persisting).toBe(1);
    expect(result.deltas.new_this_week).toBe(0);
    expect(result.newState.warningsByCheck["Frontend - A11y E2E"].weeksSeen).toBe(3);
  });

  it("classifies warnings in prev state but NOT this week as resolved_this_week", () => {
    const prevState = {
      schemaVersion: 1,
      lastWeekISO: "2026-W21",
      warningsByCheck: {
        "Stale Check": {
          owner: "qa",
          firstSeenWeek: "2026-W20",
          weeksSeen: 2,
          lastSeenWeek: "2026-W21",
        },
      },
    };
    const prs = [
      makePr({
        number: 301,
        title: "release B",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates: GATES_DEFAULT });
    expect(result.deltas.resolved_this_week).toBe(1);
    expect(result.newState.warningsByCheck["Stale Check"]).toBeUndefined();
  });
});

describe("buildDigest — escalation flag (matrix #7, AC-5)", () => {
  it("flags warnings persisting >= escalation_threshold_weeks (default 4)", () => {
    const prevState = {
      schemaVersion: 1,
      lastWeekISO: "2026-W21",
      warningsByCheck: {
        "Frontend - A11y E2E": {
          owner: "qa",
          firstSeenWeek: "2026-W18",
          weeksSeen: 4,
          lastSeenWeek: "2026-W21",
        },
      },
    };
    const prs = [
      makePr({
        number: 400,
        title: "release C",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates: GATES_DEFAULT });
    expect(result.escalations).toHaveLength(1);
    expect(result.escalations[0].check_name).toBe("Frontend - A11y E2E");
    expect(result.escalations[0].weeks_seen).toBe(5);
    expect(result.slackMessage).toContain(":warning:");
    expect(result.slackMessage).toContain("suggest reclassify to blocker");
  });

  it("honors a custom escalation_threshold_weeks override from gates", () => {
    const gates = {
      bot: { phase2c: { enabled: true, escalation_threshold_weeks: 2 } },
    };
    const prevState = {
      schemaVersion: 1,
      lastWeekISO: "2026-W21",
      warningsByCheck: {
        "Lighthouse CI": {
          owner: "frontend-dev",
          firstSeenWeek: "2026-W20",
          weeksSeen: 2,
          lastSeenWeek: "2026-W21",
        },
      },
    };
    const prs = [
      makePr({
        number: 401,
        title: "release D",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Lighthouse CI", "warning", "frontend-dev")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates });
    expect(result.escalations).toHaveLength(1);
  });
});

describe("buildDigest — idempotency (matrix #6)", () => {
  it("skips when prevState.lastWeekISO == currentWeek (action='skip')", () => {
    const currentWeek = isoWeek(NOW);
    const prevState = {
      schemaVersion: 1,
      lastWeekISO: currentWeek,
      warningsByCheck: {},
    };
    const prs = [
      makePr({
        number: 500,
        title: "release E",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates: GATES_DEFAULT });
    expect(result.action).toBe("skip");
    expect(result.reason).toBe("already-processed-this-week");
  });
});

describe("buildDigest — kill switch (matrix #11, AC-6)", () => {
  it("skips when gates.bot.phase2c.enabled === false", () => {
    const gates = { bot: { phase2c: { enabled: false } } };
    const prs = [
      makePr({
        number: 600,
        title: "release F",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates });
    expect(result.action).toBe("skip");
    expect(result.reason).toBe("bot.phase2c.enabled=false");
  });
});

describe("buildDigest — schema drift tolerance (matrix #10)", () => {
  it("treats prevState with unknown schemaVersion as empty (graceful degrade)", () => {
    const prevState = {
      schemaVersion: 999,
      lastWeekISO: "2026-W21",
      warningsByCheck: { foo: { owner: "x", weeksSeen: 99 } },
    };
    const prs = [
      makePr({
        number: 700,
        title: "release G",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState, now: NOW, gates: GATES_DEFAULT });
    // Falls back to treating prevState as empty → warning is "new" not persisting.
    expect(result.deltas.new_this_week).toBe(1);
    expect(result.deltas.persisting).toBe(0);
  });
});

describe("buildDigest — output structure", () => {
  it("populates newState.lastWeekISO with the current ISO week", () => {
    const result = buildDigest({ prs: [], prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.newState.lastWeekISO).toBe(isoWeek(NOW));
  });

  it("aggregates a per-check entry with frequency and owner", () => {
    const prs = [
      makePr({
        number: 800,
        title: "release H",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
      makePr({
        number: 801,
        title: "release I",
        mergedAt: "2026-05-24T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.aggregates).toContainEqual(
      expect.objectContaining({
        check_name: "Frontend - A11y E2E",
        owner: "qa",
        frequency: 2,
      })
    );
  });

  it("opens a state PR when there is at least 1 warning to record", () => {
    const prs = [
      makePr({
        number: 900,
        title: "release J",
        mergedAt: "2026-05-23T10:00:00Z",
        botFailures: [makeFailure("Frontend - A11y E2E", "warning", "qa")],
      }),
    ];
    const result = buildDigest({ prs, prevState: EMPTY_STATE, now: NOW, gates: GATES_DEFAULT });
    expect(result.openStatePr).toBe(true);
    expect(result.statePrBranchName).toMatch(/^chore\/release-gate-digest-state-2026-W22$/);
  });
});

describe("isoWeek — utility", () => {
  it("formats Date → 'YYYY-Www' using ISO 8601 week numbering", () => {
    // Monday 2026-05-25 is in ISO week 22 of 2026.
    expect(isoWeek(new Date("2026-05-25T08:00:00Z"))).toBe("2026-W22");
    // 2026-01-01 (Thursday) is in ISO week 1 of 2026.
    expect(isoWeek(new Date("2026-01-01T12:00:00Z"))).toBe("2026-W01");
    // 2025-12-29 (Monday) is in ISO week 1 of 2026 by ISO 8601 rules.
    expect(isoWeek(new Date("2025-12-29T12:00:00Z"))).toBe("2026-W01");
  });
});
