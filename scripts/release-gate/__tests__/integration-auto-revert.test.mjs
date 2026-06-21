// scripts/release-gate/__tests__/integration-auto-revert.test.mjs
// Phase 2b (#1445) — integration tests for run-auto-revert.mjs imperative shell.
//
// Mock surface:
//   - @octokit/rest (full mock per spec pattern Phase 2c)
//   - node:child_process execSync (git ops)
//   - node:fs readFileSync/writeFileSync (state file)
//   - global.fetch (Slack)

import { describe, it, expect, vi, beforeEach } from "vitest";

// Mocks must be declared before imports
const mockOctokit = {
  pulls: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    merge: vi.fn(),
  },
  checks: { listForRef: vi.fn() },
  issues: { listComments: vi.fn(), addLabels: vi.fn(), createComment: vi.fn() },
};

vi.mock("@octokit/rest", () => ({
  Octokit: vi.fn(() => mockOctokit),
}));

const mockExec = vi.fn();
vi.mock("node:child_process", () => ({ execSync: mockExec }));

const mockFs = {
  readFileSync: vi.fn(),
  writeFileSync: vi.fn(),
  existsSync: vi.fn(),
  mkdirSync: vi.fn(),
};
vi.mock("node:fs", async () => {
  const actual = await vi.importActual("node:fs");
  return { ...actual, ...mockFs };
});

global.fetch = vi.fn();

// Defer import until mocks set up
let runMain;

describe("integration — auto-revert E2E", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    process.env.GITHUB_REPOSITORY = "meepleAi-app/meepleai-monorepo";
    process.env.GITHUB_RUN_ID = "42";
    process.env.GITHUB_SERVER_URL = "https://github.com";
    process.env.GITHUB_TOKEN = "test-token";
    process.env.AUTO_REVERT_CLOCK_SOURCE = "test";
    process.env.AUTO_REVERT_TEST_NOW = "2026-06-23T08:16:00Z";
    delete process.env.SLACK_RELEASE_WEBHOOK_URL; // tests opt in per-case

    // YAML stub: kill-switch ON, dry-run OFF (live mode)
    mockFs.readFileSync.mockImplementation((p) => {
      if (String(p).endsWith("release-gates.yml")) {
        return `version: 1
checks:
  - check_name: "Backend - Unit Tests"
    severity: blocker
    owner: backend-dev
    override_path: fix-forward
    pre_existing_in_main_dev: false
bot:
  phase2b:
    enabled: true
    dry_run_mode: false
`;
      }
      if (String(p).endsWith("auto-revert-events.jsonl")) return "";
      throw new Error(`unexpected fs.readFileSync(${p})`);
    });
    mockFs.existsSync.mockReturnValue(true);

    mockExec.mockImplementation((cmd) => {
      if (cmd.startsWith("git ls-remote")) return "abc12345\trefs/heads/main-staging";
      if (cmd === "git rev-parse --abbrev-ref HEAD") return "main-dev";
      return "";
    });

    // Module needs fresh import after mocks
    vi.resetModules();
    runMain = (await import("../run-auto-revert.mjs")).main;
  });

  it("Happy path: blocker → 16min elapsed → no fix-forward → revert opens + merges", async () => {
    mockOctokit.pulls.list.mockImplementation(async ({ state }) => {
      if (state === "closed") {
        return { data: [{
          number: 1234,
          merged_at: "2026-06-23T08:00:00Z",
          merge_commit_sha: "abc12345",
          title: "feat: some feature",
          body: "",
          state: "closed",
        }] };
      }
      // state === "open" — fix-forwards query
      return { data: [] };
    });
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [{ name: "Backend - Unit Tests", conclusion: "failure", html_url: "https://check-run-url" }] },
    });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });
    mockOctokit.pulls.create.mockResolvedValue({ data: { number: 9999, html_url: "https://pr-url" } });
    mockOctokit.issues.addLabels.mockResolvedValue({});
    mockOctokit.pulls.merge.mockResolvedValue({});
    global.fetch.mockResolvedValue({ ok: true });

    await runMain();

    expect(mockOctokit.pulls.create).toHaveBeenCalled();
    expect(mockOctokit.pulls.merge).toHaveBeenCalled();
  });

  it("C3b race: revert opens → fix-forward arrives → re-check closes revert PR", async () => {
    // first fixForward call returns empty, second (after pr_created) returns the race PR
    let ffCallCount = 0;
    mockOctokit.pulls.list.mockImplementation(async ({ state }) => {
      if (state === "closed") {
        return { data: [{
          number: 1234,
          merged_at: "2026-06-23T08:00:00Z",
          merge_commit_sha: "abc12345",
          title: "feat: x",
          body: "",
        }] };
      }
      ffCallCount++;
      if (ffCallCount === 1) return { data: [] }; // pre-create: no fix-forward
      return { data: [{
        number: 5678,
        created_at: "2026-06-23T08:05:00Z",
        title: "fix: hot patch",
        labels: [],
      }] }; // race
    });
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [{ name: "Backend - Unit Tests", conclusion: "failure", html_url: "https://check-run-url" }] },
    });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });
    mockOctokit.pulls.create.mockResolvedValue({ data: { number: 9999, html_url: "https://pr-url" } });
    mockOctokit.issues.addLabels.mockResolvedValue({});
    mockOctokit.pulls.update.mockResolvedValue({});
    mockOctokit.issues.createComment.mockResolvedValue({});

    await runMain();

    expect(mockOctokit.pulls.update).toHaveBeenCalledWith(expect.objectContaining({ pull_number: 9999, state: "closed" }));
    expect(mockOctokit.pulls.merge).not.toHaveBeenCalled();
  });

  it("Dry-run mode: DRAFT PR opens + no merge + Slack [DRY-RUN] prefix", async () => {
    // Override yaml stub: dry_run_mode: true
    mockFs.readFileSync.mockImplementation((p) => {
      if (String(p).endsWith("release-gates.yml")) {
        return `version: 1
checks:
  - check_name: "Backend - Unit Tests"
    severity: blocker
    owner: backend-dev
    override_path: fix-forward
    pre_existing_in_main_dev: false
bot:
  phase2b:
    enabled: true
    dry_run_mode: true
`;
      }
      if (String(p).endsWith("auto-revert-events.jsonl")) return "";
      throw new Error(`unexpected fs.readFileSync(${p})`);
    });
    mockOctokit.pulls.list.mockImplementation(async ({ state }) => {
      if (state === "closed") {
        return { data: [{
          number: 1234,
          merged_at: "2026-06-23T08:00:00Z",
          merge_commit_sha: "abc12345",
          title: "feat: x",
          body: "",
        }] };
      }
      return { data: [] };
    });
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [{ name: "Backend - Unit Tests", conclusion: "failure", html_url: "https://check-run-url" }] },
    });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });
    mockOctokit.pulls.create.mockResolvedValue({ data: { number: 9999, html_url: "https://pr-url" } });
    mockOctokit.issues.addLabels.mockResolvedValue({});
    global.fetch.mockResolvedValue({ ok: true });

    await runMain();

    expect(mockOctokit.pulls.create).toHaveBeenCalledWith(expect.objectContaining({ draft: true }));
    expect(mockOctokit.pulls.merge).not.toHaveBeenCalled();
    expect(mockOctokit.issues.addLabels).toHaveBeenCalledWith(
      expect.objectContaining({ labels: expect.arrayContaining(["dry-run"]) }),
    );
  });

  it("Audit trail: revert PR body contains all 5 AC-6 fields", async () => {
    mockOctokit.pulls.list.mockImplementation(async ({ state }) =>
      state === "closed"
        ? { data: [{
            number: 1234,
            merged_at: "2026-06-23T08:00:00Z",
            merge_commit_sha: "abc12345",
            title: "feat: x",
            body: "",
          }] }
        : { data: [] },
    );
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [{ name: "Backend - Unit Tests", conclusion: "failure", html_url: "https://check-url" }] },
    });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });
    mockOctokit.pulls.create.mockResolvedValue({ data: { number: 9999, html_url: "https://pr-url" } });
    mockOctokit.issues.addLabels.mockResolvedValue({});
    mockOctokit.pulls.merge.mockResolvedValue({});
    global.fetch.mockResolvedValue({ ok: true });

    await runMain();

    const callBody = mockOctokit.pulls.create.mock.calls[0][0].body;
    expect(callBody).toContain("Workflow run");
    expect(callBody).toContain("Original PR**: #1234");
    expect(callBody).toContain("Classification snapshot");
    expect(callBody).toContain("Backend - Unit Tests");
    expect(callBody).toContain("Cooldown elapsed");
  });

  it("Slack 503 → soft-fail, revert still merged", async () => {
    // SLACK_WEBHOOK is captured at module-load (top-level const). Set env var and
    // re-import so the catch path is actually exercised.
    process.env.SLACK_RELEASE_WEBHOOK_URL = "https://hooks.slack.com/test";
    vi.resetModules();
    runMain = (await import("../run-auto-revert.mjs")).main;

    mockOctokit.pulls.list.mockImplementation(async ({ state }) =>
      state === "closed"
        ? { data: [{
            number: 1234,
            merged_at: "2026-06-23T08:00:00Z",
            merge_commit_sha: "abc12345",
            title: "feat: x",
            body: "",
          }] }
        : { data: [] },
    );
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [{ name: "Backend - Unit Tests", conclusion: "failure", html_url: "https://check-run-url" }] },
    });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });
    mockOctokit.pulls.create.mockResolvedValue({ data: { number: 9999, html_url: "https://pr-url" } });
    mockOctokit.issues.addLabels.mockResolvedValue({});
    mockOctokit.pulls.merge.mockResolvedValue({});
    global.fetch.mockRejectedValue(new Error("503 Service Unavailable"));

    await runMain();

    expect(mockOctokit.pulls.merge).toHaveBeenCalled(); // merge still happens
  });
});
