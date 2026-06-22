import { describe, it, expect, beforeAll } from "vitest";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { loadGates, classify } from "../lib/classify.mjs";
import { formatComment, formatActionsSummary, SIGNATURE_HEADER } from "../lib/format.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturePath = path.join(__dirname, "fixtures", "release-gates-valid.yml");

let gates;
beforeAll(() => {
  gates = loadGates(fixturePath);
});

function makeClassifiedFailures(checkNames) {
  return checkNames.map((name) => ({
    name,
    conclusion: "failure",
    ...classify(name, gates),
  }));
}

const FIXED_META = {
  commit_sha: "abc1234def5678",
  commit_short: "abc1234",
  pr_number: 999,
  generated_at: "2026-05-22T10:00:00Z",
  duration_ms: 142,
};

describe("formatComment — first-run output (AC-2)", () => {
  it("includes signature header for idempotent edit detection", () => {
    const failures = makeClassifiedFailures(["Backend - Unit Tests"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain(SIGNATURE_HEADER);
    expect(md.startsWith(SIGNATURE_HEADER)).toBe(true);
  });

  it("includes commit ref + timestamp in header", () => {
    const failures = makeClassifiedFailures(["Backend - Unit Tests"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain("abc1234");
    expect(md).toContain("2026-05-22T10:00:00Z");
  });

  it("renders a classification table row per failure", () => {
    const failures = makeClassifiedFailures([
      "Backend - Unit Tests",
      "Frontend - A11y E2E",
      "Lychee Link Check",
    ]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain("Backend - Unit Tests");
    expect(md).toContain("Frontend - A11y E2E");
    expect(md).toContain("Lychee Link Check");
    expect(md).toContain("blocker");
    expect(md).toContain("warning");
    expect(md).toContain("informational");
    expect(md).toContain("backend-dev");
    expect(md).toContain("qa");
    expect(md).toContain("devops");
  });

  it("renders verdict summary with per-severity counts", () => {
    const failures = makeClassifiedFailures([
      "Backend - Unit Tests", // blocker
      "Frontend - A11y E2E",  // warning
      "Lychee Link Check",    // informational
    ]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/1.*blocker/i);
    expect(md).toMatch(/1.*warning/i);
    expect(md).toMatch(/1.*informational/i);
  });

  it("renders BLOCKER verdict when any blocker present", () => {
    const failures = makeClassifiedFailures(["Backend - Unit Tests"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/BLOCKER/);
  });

  it("renders GREEN verdict when only informational failures present", () => {
    const failures = makeClassifiedFailures(["Lychee Link Check"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/GREEN/);
  });

  it("renders WARNING verdict when only warnings present", () => {
    const failures = makeClassifiedFailures(["Frontend - A11y E2E"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/WARNING/);
  });
});

describe("formatComment — unknown-check footer (AC-6)", () => {
  it("appends 🆕 footer when at least one unknown check present", () => {
    const failures = makeClassifiedFailures(["Brand New Check"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/🆕|new check/i);
    expect(md).toContain(".github/release-gates.yml");
  });

  it("omits footer when no unknown checks", () => {
    const failures = makeClassifiedFailures(["Backend - Unit Tests"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).not.toMatch(/🆕/);
  });

  it("includes unknown-count in footer", () => {
    const failures = makeClassifiedFailures(["Brand A", "Brand B", "Brand C"]);
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toMatch(/3.*new/i);
  });
});

describe("formatComment — failure-mode fallback (AC-5)", () => {
  it("formats fallback comment when bot script crashes", () => {
    const md = formatComment({
      failures: [],
      meta: FIXED_META,
      gates,
      error: { message: "yaml: bad indentation at line 17", phase: "yaml-parse" },
    });
    expect(md).toContain(SIGNATURE_HEADER);
    expect(md).toContain("⚠️");
    expect(md).toContain("release-gate bot failed");
    expect(md).toContain("yaml: bad indentation at line 17");
    expect(md).toContain("manual triage required");
  });
});

describe("formatActionsSummary — observability (AC-10)", () => {
  it("renders summary table with all counts", () => {
    const failures = makeClassifiedFailures([
      "Backend - Unit Tests",
      "Backend - Integration (KnowledgeBase)",
      "Frontend - A11y E2E",
      "Lychee Link Check",
      "Brand New Check",
    ]);
    const summary = formatActionsSummary({
      failures,
      total_checks: 24,
      meta: FIXED_META,
    });
    expect(summary).toContain("Total checks evaluated");
    expect(summary).toContain("24");
    expect(summary).toMatch(/Blocker.*2/);
    expect(summary).toMatch(/Warning.*1/);
    expect(summary).toMatch(/Informational.*1/);
    expect(summary).toMatch(/(Unknown|new).*1/);
    expect(summary).toContain("142"); // duration_ms
  });

  it("renders verdict line", () => {
    const failures = makeClassifiedFailures(["Backend - Unit Tests"]);
    const summary = formatActionsSummary({ failures, total_checks: 10, meta: FIXED_META });
    expect(summary).toMatch(/Verdict.*BLOCKER/i);
  });
});

describe("formatComment — markdown injection defense (AC-5 security)", () => {
  it("escapes backticks in check_name to prevent code-span breakout", () => {
    // Adversarial check name: backtick → injected markdown after escape
    const failures = [
      {
        name: "evil`name`with`backticks",
        conclusion: "failure",
        severity: "warning",
        owner: "unknown",
        override_path: "exception-comment",
        notes: null,
        source: "unknown",
        matched_by: null,
        is_unknown: true,
        pre_existing_in_main_dev: false,
      },
    ];
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain("evil\\`name\\`with\\`backticks");
    expect(md).not.toContain("`evil`name`"); // unescaped form must not appear
  });

  it("escapes pipes in check_name to prevent table-row breakout", () => {
    const failures = [
      {
        name: "evil|cell|injection",
        conclusion: "failure",
        severity: "warning",
        owner: "unknown",
        override_path: "exception-comment",
        notes: null,
        source: "unknown",
        matched_by: null,
        is_unknown: true,
        pre_existing_in_main_dev: false,
      },
    ];
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain("evil\\|cell\\|injection");
  });

  it("collapses newlines in check_name to prevent paragraph breakout", () => {
    const failures = [
      {
        name: "evil\nname\nbreak",
        conclusion: "failure",
        severity: "warning",
        owner: "unknown",
        override_path: "exception-comment",
        notes: null,
        source: "unknown",
        matched_by: null,
        is_unknown: true,
        pre_existing_in_main_dev: false,
      },
    ];
    const md = formatComment({ failures, meta: FIXED_META, gates });
    // Newlines collapsed to spaces, no row breakout
    expect(md).not.toMatch(/\| `evil$/m);
    expect(md).toContain("evil name break");
  });

  it("escapes pipes in notes (regression for existing behavior)", () => {
    const failures = [
      {
        name: "safe",
        conclusion: "failure",
        severity: "warning",
        owner: "devops",
        override_path: "exception-comment",
        notes: "Has | pipe | in | notes",
        source: "exact",
        matched_by: "safe",
        is_unknown: false,
        pre_existing_in_main_dev: false,
      },
    ];
    const md = formatComment({ failures, meta: FIXED_META, gates });
    expect(md).toContain("Has \\| pipe \\| in \\| notes");
  });
});

describe("formatComment — idempotency anchor (AC-4)", () => {
  it("same input produces stable signature header for matching/editing existing comment", () => {
    const failures1 = makeClassifiedFailures(["Backend - Unit Tests"]);
    const failures2 = makeClassifiedFailures(["Frontend - A11y E2E"]); // different content
    const md1 = formatComment({ failures: failures1, meta: FIXED_META, gates });
    const md2 = formatComment({ failures: failures2, meta: FIXED_META, gates });
    // Different content but SAME signature → bot can find & edit
    expect(md1.startsWith(SIGNATURE_HEADER)).toBe(true);
    expect(md2.startsWith(SIGNATURE_HEADER)).toBe(true);
  });
});
