import { describe, it, expect, beforeAll, vi } from "vitest";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { loadGates } from "../lib/classify.mjs";
import { SIGNATURE_HEADER } from "../lib/format.mjs";
import { runBot } from "../comment.mjs";
import { VERDICT_CHECK_NAME, VERDICT_EXTERNAL_ID } from "../lib/conclusion-override.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixturePath = path.join(__dirname, "fixtures", "release-gates-valid.yml");

let gates;
beforeAll(() => {
  gates = loadGates(fixturePath);
});

function makeOctokit({
  checkRuns = [],
  existingComments = [],
  failListComments = false,
  failCheckRuns = false,
  // Phase 2a (#1444) — verdict mock hooks
  checksCreateImpl = null,
  existingVerdictRun = null,
  checksUpdateImpl = null,
  commentHtmlUrl = "https://github.com/x/y/pull/1#issuecomment-12345",
} = {}) {
  const defaultCreateImpl = async (args) => ({
    data: { id: 7777, name: args.name, external_id: args.external_id },
  });
  const defaultUpdateImpl = async (args) => ({
    data: { id: args.check_run_id, name: args.name, external_id: args.external_id },
  });

  return {
    checks: {
      listForRef: vi.fn(async (args) => {
        if (failCheckRuns) {
          const err = new Error("503 Service Unavailable");
          err.status = 503;
          throw err;
        }
        // Phase 2a dedup lookup: when called with check_name=VERDICT_CHECK_NAME
        // return the seeded existingVerdictRun (if any).
        if (args?.check_name === VERDICT_CHECK_NAME) {
          return { data: { check_runs: existingVerdictRun ? [existingVerdictRun] : [] } };
        }
        return { data: { check_runs: checkRuns } };
      }),
      create: vi.fn(checksCreateImpl ?? defaultCreateImpl),
      update: vi.fn(checksUpdateImpl ?? defaultUpdateImpl),
    },
    pulls: {
      get: vi.fn(async () => ({ data: { head: { sha: "head1234" } } })),
    },
    issues: {
      listComments: vi.fn(async () => {
        if (failListComments) {
          const err = new Error("rate limited");
          throw err;
        }
        return { data: existingComments };
      }),
      createComment: vi.fn(async (args) => ({
        data: { id: 9001, body: args.body, html_url: commentHtmlUrl },
      })),
      updateComment: vi.fn(async (args) => ({
        data: { id: args.comment_id, body: args.body, html_url: commentHtmlUrl },
      })),
    },
  };
}

const BASE_ARGS = {
  owner: "meepleAi-app",
  repo: "meepleai-monorepo",
  prNumber: 1016,
  commitSha: "abcdef1234567890",
  appendSummary: null,
  now: () => new Date("2026-05-22T10:00:00Z"),
};

describe("runBot — first-run (AC-2, AC-3)", () => {
  it("creates a new comment when none exists", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "failure" },
        { name: "Frontend - A11y E2E", conclusion: "failure" },
      ],
      existingComments: [],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(true);
    expect(result.edited).toBe(false);
    expect(result.existing_comment_id).toBeNull();
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
    expect(octokit.issues.updateComment).not.toHaveBeenCalled();

    const postedBody = octokit.issues.createComment.mock.calls[0][0].body;
    expect(postedBody).toContain(SIGNATURE_HEADER);
    expect(postedBody).toContain("Backend - Unit Tests");
    expect(postedBody).toContain("Frontend - A11y E2E");
    expect(postedBody).toMatch(/BLOCKER/);
  });

  it("classifies count of failures correctly", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "failure" },
        { name: "Lychee Link Check", conclusion: "failure" },
      ],
    });
    const result = await runBot({ ...BASE_ARGS, octokit, gates });
    expect(result.failures).toHaveLength(2);
    expect(result.total_checks).toBe(2);
  });
});

describe("runBot — idempotency (AC-4)", () => {
  it("edits existing comment when signature header matches", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      existingComments: [
        { id: 42, body: `${SIGNATURE_HEADER}\n\nOld content` },
        { id: 43, body: "Unrelated comment" },
      ],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(true);
    expect(result.edited).toBe(true);
    expect(result.existing_comment_id).toBe(42);
    expect(octokit.issues.updateComment).toHaveBeenCalledTimes(1);
    expect(octokit.issues.updateComment.mock.calls[0][0].comment_id).toBe(42);
    expect(octokit.issues.createComment).not.toHaveBeenCalled();
  });

  it("two consecutive runs produce 0 extra create calls", async () => {
    // Simulate the second run: existing comment present from first run
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      existingComments: [{ id: 42, body: `${SIGNATURE_HEADER}\n\nOld` }],
    });

    await runBot({ ...BASE_ARGS, octokit, gates });
    await runBot({ ...BASE_ARGS, octokit, gates });

    expect(octokit.issues.createComment).not.toHaveBeenCalled();
    expect(octokit.issues.updateComment).toHaveBeenCalledTimes(2);
  });

  it("ignores comments without the signature header (treated as foreign)", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      existingComments: [
        { id: 100, body: "<!-- some-other-bot:v1 -->\n\nNot us" },
        { id: 101, body: "Just a human comment" },
      ],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.edited).toBe(false);
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
    expect(octokit.issues.updateComment).not.toHaveBeenCalled();
  });
});

describe("runBot — failure-mode (AC-5)", () => {
  it("posts fallback comment when check-runs fetch fails", async () => {
    const octokit = makeOctokit({ failCheckRuns: true });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(false);
    expect(result.error.phase).toBe("fetch-check-runs");
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
    const body = octokit.issues.createComment.mock.calls[0][0].body;
    expect(body).toContain("release-gate bot failed");
    expect(body).toContain(SIGNATURE_HEADER);
    expect(body).toContain("503");
  });

  it("falls back to create when listComments also fails", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      failListComments: true,
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    // listComments failure is non-fatal: bot falls through to createComment
    expect(result.ok).toBe(true);
    expect(result.edited).toBe(false);
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
  });
});

describe("runBot — unknown-check handling (AC-6)", () => {
  it("flags unknown check in comment + footer", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "failure" },
        { name: "Brand New Workflow Z", conclusion: "failure" },
      ],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.failures).toHaveLength(2);
    expect(result.failures.find((f) => f.name === "Brand New Workflow Z").is_unknown).toBe(true);

    const body = octokit.issues.createComment.mock.calls[0][0].body;
    expect(body).toMatch(/🆕|new check/i);
    expect(body).toContain(".github/release-gates.yml");
  });
});

describe("runBot — observability (AC-10)", () => {
  it("appends summary when appendSummary callback is provided", async () => {
    const summarySink = [];
    const append = (s) => summarySink.push(s);

    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "failure" },
        { name: "Frontend - A11y E2E", conclusion: "failure" },
        { name: "Backend - Unit Tests Passed", conclusion: "success" }, // not a failure
      ],
    });

    await runBot({ ...BASE_ARGS, octokit, gates, appendSummary: append });

    // Two summary entries: Phase 1 classification + Phase 2a verdict.
    expect(summarySink).toHaveLength(2);
    const summary = summarySink[0];
    expect(summary).toContain("Total checks evaluated");
    expect(summary).toContain("3"); // total
    expect(summary).toMatch(/Blocker.*1/);
    expect(summary).toMatch(/Warning.*1/);

    // Phase 2a verdict appended after Phase 1 summary
    const verdictSummary = summarySink[1];
    expect(verdictSummary).toContain("Release-Gate Verdict (Phase 2a)");
    expect(verdictSummary).toContain("failure"); // 1 blocker dominates → failure
  });
});

describe("runBot — conclusion filtering", () => {
  it("treats cancelled as failure-of-interest", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "cancelled" },
        { name: "Frontend - A11y E2E", conclusion: "success" },
      ],
    });
    const result = await runBot({ ...BASE_ARGS, octokit, gates });
    expect(result.failures).toHaveLength(1);
    expect(result.failures[0].name).toBe("Backend - Unit Tests");
  });

  it("ignores success / neutral / skipped conclusions", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "success" },
        { name: "Frontend - A11y E2E", conclusion: "neutral" },
        { name: "Lychee Link Check", conclusion: "skipped" },
      ],
    });
    const result = await runBot({ ...BASE_ARGS, octokit, gates });
    expect(result.failures).toHaveLength(0);
  });

  it("treats timed_out and action_required as failures", async () => {
    const octokit = makeOctokit({
      checkRuns: [
        { name: "Backend - Unit Tests", conclusion: "timed_out" },
        { name: "Frontend - A11y E2E", conclusion: "action_required" },
      ],
    });
    const result = await runBot({ ...BASE_ARGS, octokit, gates });
    expect(result.failures).toHaveLength(2);
  });
});

// ─── Phase 2a (#1444) — publishVerdict integration ──────────────────────────
// Spec edge-case matrix rows 6, 7, 9, 10.

describe("publishVerdict — Phase 2a integration (AC-1, AC-4, AC-7, AC-8)", () => {
  it("row 6 — idempotency: re-run on same SHA updates via external_id lookup", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      existingVerdictRun: { id: 4242, external_id: VERDICT_EXTERNAL_ID },
      checksCreateImpl: async () => {
        const err = new Error("a check_run with this external_id already exists");
        err.status = 422;
        throw err;
      },
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(true);
    expect(result.verdict.published).toBe(true);
    expect(result.verdict.edited).toBe(true);
    expect(result.verdict.check_run_id).toBe(4242);
    expect(octokit.checks.create).toHaveBeenCalledTimes(1);
    expect(octokit.checks.update).toHaveBeenCalledTimes(1);
    // Update call carried the verdict payload
    const updateCallArgs = octokit.checks.update.mock.calls[0][0];
    expect(updateCallArgs.check_run_id).toBe(4242);
    expect(updateCallArgs.name).toBe(VERDICT_CHECK_NAME);
    expect(updateCallArgs.conclusion).toBe("failure");
  });

  it("row 7 — kill switch: bot.phase2a.enabled=false skips checks.create entirely", async () => {
    const gatesKilled = JSON.parse(JSON.stringify(gates));
    gatesKilled.bot.phase2a = { enabled: false };

    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates: gatesKilled });

    expect(result.ok).toBe(true);
    expect(result.verdict.published).toBe(false);
    expect(result.verdict.reason).toBe("kill-switch-disabled");
    // No checks.create or checks.update calls when kill switch is active
    expect(octokit.checks.create).not.toHaveBeenCalled();
    expect(octokit.checks.update).not.toHaveBeenCalled();
    // Phase 1 comment still posted
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
  });

  it("row 9 — rate-limited (429): runBot returns success, verdict soft-fails", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      checksCreateImpl: async () => {
        const err = new Error("API rate limit exceeded");
        err.status = 429;
        throw err;
      },
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(true); // AC-8 soft-fail — comment IS the signal
    expect(result.verdict.published).toBe(false);
    expect(result.verdict.reason).toBe("publish-error");
    expect(result.verdict.error).toContain("rate limit");
    // Phase 1 comment still posted
    expect(octokit.issues.createComment).toHaveBeenCalledTimes(1);
  });

  it("row 10 — 5xx server error: same soft-fail as 429", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Frontend - A11y E2E", conclusion: "failure" }],
      checksCreateImpl: async () => {
        const err = new Error("503 Service Unavailable");
        err.status = 503;
        throw err;
      },
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.ok).toBe(true);
    expect(result.verdict.published).toBe(false);
    expect(result.verdict.reason).toBe("publish-error");
    expect(result.verdict.error).toContain("503");
  });

  it("AC-2: published check carries the exact name 'Release-Gate Verdict'", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.verdict.published).toBe(true);
    expect(octokit.checks.create).toHaveBeenCalledTimes(1);
    const createArgs = octokit.checks.create.mock.calls[0][0];
    expect(createArgs.name).toBe(VERDICT_CHECK_NAME);
    expect(createArgs.external_id).toBe(VERDICT_EXTERNAL_ID);
    expect(createArgs.head_sha).toBe(BASE_ARGS.commitSha);
    expect(createArgs.status).toBe("completed");
  });

  it("AC-5: warning-only failure produces conclusion=neutral", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Frontend - A11y E2E", conclusion: "failure" }],
    });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.verdict.published).toBe(true);
    expect(result.verdict.conclusion).toBe("neutral");
    expect(octokit.checks.create.mock.calls[0][0].conclusion).toBe("neutral");
  });

  it("AC-5: clean run (no failures) produces conclusion=success", async () => {
    const octokit = makeOctokit({ checkRuns: [] });

    const result = await runBot({ ...BASE_ARGS, octokit, gates });

    expect(result.verdict.published).toBe(true);
    expect(result.verdict.conclusion).toBe("success");
  });

  it("output.summary links to the bot comment URL when available", async () => {
    const octokit = makeOctokit({
      checkRuns: [{ name: "Backend - Unit Tests", conclusion: "failure" }],
      commentHtmlUrl: "https://github.com/test/repo/pull/1#issuecomment-99",
    });

    await runBot({ ...BASE_ARGS, octokit, gates });

    const createArgs = octokit.checks.create.mock.calls[0][0];
    expect(createArgs.output.summary).toContain(
      "https://github.com/test/repo/pull/1#issuecomment-99"
    );
  });
});
