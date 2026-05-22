import { describe, it, expect, beforeAll, vi } from "vitest";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { loadGates } from "../lib/classify.mjs";
import { SIGNATURE_HEADER } from "../lib/format.mjs";
import { runBot } from "../comment.mjs";

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
} = {}) {
  return {
    checks: {
      listForRef: vi.fn(async () => {
        if (failCheckRuns) {
          const err = new Error("503 Service Unavailable");
          err.status = 503;
          throw err;
        }
        return { data: { check_runs: checkRuns } };
      }),
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
      createComment: vi.fn(async (args) => ({ data: { id: 9001, body: args.body } })),
      updateComment: vi.fn(async (args) => ({ data: { id: args.comment_id, body: args.body } })),
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

    expect(summarySink).toHaveLength(1);
    const summary = summarySink[0];
    expect(summary).toContain("Total checks evaluated");
    expect(summary).toContain("3"); // total
    expect(summary).toMatch(/Blocker.*1/);
    expect(summary).toMatch(/Warning.*1/);
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
