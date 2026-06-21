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

  // Additional scenarios — Task 21 will flesh these out:
  it.todo("C3b race: revert opens → fix-forward arrives → re-check closes revert PR");
  it.todo("Dry-run mode: DRAFT PR opens + no merge + Slack [DRY-RUN]");
  it.todo("Audit trail: revert PR body contains all 5 AC-6 fields (snapshot)");
  it.todo("Slack 503 → soft-fail, revert still merged");
});
