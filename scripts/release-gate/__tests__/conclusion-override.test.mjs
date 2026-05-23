// Phase 2a (#1444) — unit tests for the pure verdict synthesizer.
// Covers edge case matrix rows 1-5, 8, 11-12 from the spec (integration
// rows 6, 7, 9, 10 live in integration.test.mjs).

import { describe, it, expect } from "vitest";

import {
  computeVerdict,
  VERDICT_CHECK_NAME,
  VERDICT_EXTERNAL_ID,
  OUTPUT_TEXT_MAX,
} from "../lib/conclusion-override.mjs";

function failure({ name, severity, is_unknown = false, owner = "backend-dev", override_path = "fix-forward", notes = null }) {
  return { name, severity, is_unknown, owner, override_path, notes };
}

const ENABLED_GATES = { bot: { phase2a: { enabled: true } } };
const DISABLED_GATES = { bot: { phase2a: { enabled: false } } };
const DEFAULT_GATES = { bot: {} }; // no phase2a sub-key → default true

describe("constants — AC-2 stable contract", () => {
  it("VERDICT_CHECK_NAME is the exact public-contract string", () => {
    expect(VERDICT_CHECK_NAME).toBe("Release-Gate Verdict");
  });

  it("VERDICT_EXTERNAL_ID is the idempotency key (AC-4)", () => {
    expect(VERDICT_EXTERNAL_ID).toBe("release-gate-verdict-v1");
  });

  it("OUTPUT_TEXT_MAX stays under GitHub Checks API hard limit (AC-9)", () => {
    expect(OUTPUT_TEXT_MAX).toBeLessThanOrEqual(65_535);
    // Sanity: leave at least 5_000 chars of headroom for the footer + safety.
    expect(65_535 - OUTPUT_TEXT_MAX).toBeGreaterThanOrEqual(5_000);
  });
});

describe("computeVerdict — kill switch (AC-3)", () => {
  it("returns { enabled: false } when bot.phase2a.enabled === false", () => {
    const result = computeVerdict({ failures: [], gates: DISABLED_GATES });
    expect(result).toEqual({ enabled: false });
  });

  it("defaults to enabled when bot.phase2a sub-key is missing", () => {
    const result = computeVerdict({ failures: [], gates: DEFAULT_GATES });
    expect(result.enabled).toBe(true);
  });

  it("defaults to enabled when gates is empty object", () => {
    const result = computeVerdict({ failures: [], gates: {} });
    expect(result.enabled).toBe(true);
  });

  it("defaults to enabled when gates is null/undefined", () => {
    expect(computeVerdict({ failures: [], gates: null }).enabled).toBe(true);
    expect(computeVerdict({ failures: [], gates: undefined }).enabled).toBe(true);
  });
});

describe("computeVerdict — conclusion mapping (AC-5)", () => {
  it("row 1: no failures → conclusion=success, title 0/0/0", () => {
    const result = computeVerdict({ failures: [], gates: ENABLED_GATES });
    expect(result.conclusion).toBe("success");
    expect(result.title).toBe("0 blocker · 0 warning · 0 informational");
    expect(result.counts).toEqual({ blocker: 0, warning: 0, informational: 0 });
  });

  it("row 2: informational-only → conclusion=success, title includes (auto-bypassed)", () => {
    const result = computeVerdict({
      failures: [failure({ name: "Lychee Link Check", severity: "informational" })],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("success");
    expect(result.title).toBe("0 blocker · 0 warning · 1 informational (auto-bypassed)");
  });

  it("row 3: warning-only → conclusion=neutral", () => {
    const result = computeVerdict({
      failures: [failure({ name: "Frontend - A11y E2E", severity: "warning" })],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("neutral");
    expect(result.title).toBe("0 blocker · 1 warning · 0 informational");
  });

  it("row 4: blocker-only → conclusion=failure", () => {
    const result = computeVerdict({
      failures: [failure({ name: "Backend - Unit Tests", severity: "blocker" })],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("failure");
    expect(result.title).toBe("1 blocker · 0 warning · 0 informational");
  });

  it("row 5: mixed blocker + warning + informational → conclusion=failure (blocker dominates)", () => {
    const result = computeVerdict({
      failures: [
        failure({ name: "Backend - Unit Tests", severity: "blocker" }),
        failure({ name: "Frontend - A11y E2E", severity: "warning" }),
        failure({ name: "Lychee Link Check", severity: "informational" }),
      ],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("failure");
    expect(result.title).toBe("1 blocker · 1 warning · 1 informational (auto-bypassed)");
  });

  it("row 12: multiple blockers (count > 1) → title shows blocker count", () => {
    const result = computeVerdict({
      failures: [
        failure({ name: "Backend - Unit Tests", severity: "blocker" }),
        failure({ name: "Backend - Integration (Core)", severity: "blocker" }),
        failure({ name: "CodeQL", severity: "blocker" }),
      ],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("failure");
    expect(result.title).toBe("3 blocker · 0 warning · 0 informational");
  });
});

describe("computeVerdict — unknown-tier fallback (AC-11, row 11)", () => {
  it("counts is_unknown=true as warning (least-surprise default)", () => {
    const result = computeVerdict({
      failures: [failure({ name: "New Job", severity: "blocker", is_unknown: true })],
      gates: ENABLED_GATES,
    });
    // is_unknown overrides severity → counts as warning, not blocker
    expect(result.conclusion).toBe("neutral");
    expect(result.counts).toEqual({ blocker: 0, warning: 1, informational: 0 });
  });

  it("rolls unrecognized severity string into warning bucket", () => {
    const result = computeVerdict({
      failures: [{ name: "Weird Job", severity: "critical", is_unknown: false, owner: "?", override_path: "?", notes: null }],
      gates: ENABLED_GATES,
    });
    expect(result.conclusion).toBe("neutral");
    expect(result.counts.warning).toBe(1);
  });
});

describe("computeVerdict — output.text formatting + truncation (AC-9, row 8)", () => {
  it("renders an empty-failures message when failures.length === 0", () => {
    const result = computeVerdict({ failures: [], gates: ENABLED_GATES });
    expect(result.text).toContain("No failed checks classified");
  });

  it("renders one markdown row per failure with escaped pipes/newlines", () => {
    const result = computeVerdict({
      failures: [
        failure({
          name: "Backend - Unit Tests",
          severity: "blocker",
          notes: "weird | note with\nnewline",
        }),
      ],
      gates: ENABLED_GATES,
    });
    expect(result.text).toContain("| `Backend - Unit Tests` | blocker | backend-dev | fix-forward |");
    // pipe inside notes must be escaped
    expect(result.text).toMatch(/weird \\\| note with newline/);
  });

  it("truncates output.text past 60_000 chars and appends truncation footer", () => {
    // synthesize 200+ failures to overflow the budget
    const big = Array.from({ length: 600 }, (_, i) =>
      failure({
        name: `Synthetic Suite ${i.toString().padStart(4, "0")}`,
        severity: i % 3 === 0 ? "blocker" : i % 3 === 1 ? "warning" : "informational",
        notes: "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor",
      })
    );
    const result = computeVerdict({ failures: big, gates: ENABLED_GATES });
    expect(result.text.length).toBeLessThanOrEqual(OUTPUT_TEXT_MAX);
    expect(result.text).toContain("table truncated");
  });
});

describe("computeVerdict — output.summary includes optional comment URL", () => {
  it("omits the comment URL when not provided", () => {
    const result = computeVerdict({
      failures: [failure({ name: "Backend - Unit Tests", severity: "blocker" })],
      gates: ENABLED_GATES,
    });
    expect(result.summary).not.toContain("release-gate bot comment");
  });

  it("includes a markdown link to the comment URL when provided", () => {
    const url = "https://github.com/meepleAi-app/meepleai-monorepo/pull/1234#issuecomment-9999";
    const result = computeVerdict({
      failures: [failure({ name: "Backend - Unit Tests", severity: "blocker" })],
      gates: ENABLED_GATES,
      commentUrl: url,
    });
    expect(result.summary).toContain(`[release-gate bot comment](${url})`);
  });
});

describe("computeVerdict — return shape stability (AC-10)", () => {
  it("always returns the same keys when enabled=true", () => {
    const result = computeVerdict({ failures: [], gates: ENABLED_GATES });
    expect(Object.keys(result).sort()).toEqual(
      ["conclusion", "counts", "enabled", "summary", "text", "title"].sort()
    );
  });

  it("only returns { enabled: false } when killed", () => {
    const result = computeVerdict({ failures: [], gates: DISABLED_GATES });
    expect(Object.keys(result)).toEqual(["enabled"]);
  });
});
