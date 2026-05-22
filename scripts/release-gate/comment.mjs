#!/usr/bin/env node
// CLI entry — fetches PR check-runs, classifies, posts/edits a single release-gate comment.
// Tested via __tests__/integration.test.mjs (nock mocks octokit).
//
// Env:
//   GITHUB_TOKEN       (required, unless DRY_RUN=1)
//   GITHUB_REPOSITORY  (e.g. "meepleAi-app/meepleai-monorepo")
//   PR_NUMBER          (release PR number; required)
//   RELEASE_GATES_YAML (optional, override default .github/release-gates.yml)
//   GITHUB_SHA         (optional, commit SHA for header)
//   DRY_RUN            (if set, prints to stdout instead of posting/editing)
//   GITHUB_STEP_SUMMARY (if set, append observability summary)

import path from "node:path";
import { fileURLToPath } from "node:url";
import { appendFileSync } from "node:fs";

import { Octokit } from "@octokit/rest";

import { loadGates, classify } from "./lib/classify.mjs";
import { formatComment, formatActionsSummary, SIGNATURE_HEADER } from "./lib/format.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");

function envOrThrow(name) {
  const v = process.env[name];
  if (!v) throw new Error(`Missing env var: ${name}`);
  return v;
}

function parseRepo() {
  const slug = envOrThrow("GITHUB_REPOSITORY");
  const [owner, repo] = slug.split("/");
  if (!owner || !repo) throw new Error(`Invalid GITHUB_REPOSITORY: ${slug}`);
  return { owner, repo };
}

// Exported for tests; the bot logic with an injected octokit + appendSummary.
export async function runBot({
  octokit,
  owner,
  repo,
  prNumber,
  gates,
  commitSha,
  appendSummary = null,
  now = () => new Date(),
}) {
  const started = Date.now();
  const generatedAt = now().toISOString();
  const shortSha = commitSha ? commitSha.slice(0, 7) : "unknown";

  // 1. Fetch check_runs for the PR's head SHA
  let checkRuns = [];
  try {
    if (commitSha) {
      const res = await octokit.checks.listForRef({
        owner,
        repo,
        ref: commitSha,
        per_page: 100,
      });
      checkRuns = res.data?.check_runs ?? [];
    } else {
      // Fallback: list check-runs for PR's head ref via pulls API
      const pr = await octokit.pulls.get({ owner, repo, pull_number: prNumber });
      const headSha = pr.data.head.sha;
      const res = await octokit.checks.listForRef({
        owner,
        repo,
        ref: headSha,
        per_page: 100,
      });
      checkRuns = res.data?.check_runs ?? [];
    }
  } catch (err) {
    return postFallback({
      octokit,
      owner,
      repo,
      prNumber,
      gates,
      shortSha,
      generatedAt,
      appendSummary,
      duration_ms: Date.now() - started,
      error: { message: err.message, phase: "fetch-check-runs" },
    });
  }

  // 2. Filter failures (failure + cancelled treated as failures-of-interest)
  const FAILURE_CONCLUSIONS = new Set(["failure", "cancelled", "timed_out", "action_required"]);
  const failures = checkRuns
    .filter((c) => FAILURE_CONCLUSIONS.has(c.conclusion))
    .map((c) => ({
      name: c.name,
      conclusion: c.conclusion,
      ...classify(c.name, gates),
    }));

  // 3. Format comment
  const meta = {
    commit_sha: commitSha ?? "unknown",
    commit_short: shortSha,
    pr_number: prNumber,
    generated_at: generatedAt,
    duration_ms: Date.now() - started,
  };
  const body = formatComment({ failures, meta, gates });

  // 4. Idempotency: find existing bot comment by signature header
  let existingId = null;
  try {
    const comments = await octokit.issues.listComments({
      owner,
      repo,
      issue_number: prNumber,
      per_page: 100,
    });
    const existing = (comments.data ?? []).find((c) => (c.body ?? "").startsWith(SIGNATURE_HEADER));
    if (existing) existingId = existing.id;
  } catch (err) {
    // Non-fatal: fall through to create-only branch
    console.error(`[release-gate:comment] warning — listComments failed: ${err.message}`);
  }

  // 5. Post or edit
  if (existingId) {
    await octokit.issues.updateComment({
      owner,
      repo,
      comment_id: existingId,
      body,
    });
  } else {
    await octokit.issues.createComment({
      owner,
      repo,
      issue_number: prNumber,
      body,
    });
  }

  // 6. Observability — GH Actions summary
  if (appendSummary) {
    appendSummary(formatActionsSummary({
      failures,
      total_checks: checkRuns.length,
      meta,
    }));
  }

  return {
    ok: true,
    failures,
    total_checks: checkRuns.length,
    existing_comment_id: existingId,
    edited: existingId !== null,
  };
}

async function postFallback({
  octokit,
  owner,
  repo,
  prNumber,
  gates,
  shortSha,
  generatedAt,
  appendSummary,
  duration_ms,
  error,
}) {
  const meta = {
    commit_sha: "unknown",
    commit_short: shortSha,
    pr_number: prNumber,
    generated_at: generatedAt,
    duration_ms,
  };
  const body = formatComment({ failures: [], meta, gates, error });
  try {
    const comments = await octokit.issues.listComments({
      owner,
      repo,
      issue_number: prNumber,
      per_page: 100,
    });
    const existing = (comments.data ?? []).find((c) => (c.body ?? "").startsWith(SIGNATURE_HEADER));
    if (existing) {
      await octokit.issues.updateComment({ owner, repo, comment_id: existing.id, body });
    } else {
      await octokit.issues.createComment({ owner, repo, issue_number: prNumber, body });
    }
  } catch {
    // double-failure: nothing we can do from inside the bot.
  }
  if (appendSummary) {
    appendSummary(`## Release-gate Bot Failure\n\n- Phase: \`${error.phase}\`\n- Error: \`${error.message}\``);
  }
  return { ok: false, error };
}

// ─── CLI ───────────────────────────────────────────────────────────────────
async function main() {
  const dryRun = process.env.DRY_RUN === "1" || process.env.DRY_RUN === "true";
  const yamlPath = process.env.RELEASE_GATES_YAML
    ? path.resolve(process.cwd(), process.env.RELEASE_GATES_YAML)
    : path.join(REPO_ROOT, ".github", "release-gates.yml");

  let gates;
  try {
    gates = loadGates(yamlPath);
  } catch (err) {
    console.error(`[release-gate:comment] FAIL — cannot load gates from ${yamlPath}: ${err.message}`);
    process.exit(2);
  }

  if (dryRun) {
    // Dry-run: classify a synthetic payload if PR data unavailable.
    const sample = (process.env.DRY_RUN_FAILURES ?? "Backend - Unit Tests,Frontend - A11y E2E,Lychee Link Check")
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);
    const failures = sample.map((name) => ({
      name,
      conclusion: "failure",
      ...classify(name, gates),
    }));
    const meta = {
      commit_sha: "dryrunsha",
      commit_short: "dryrun7",
      pr_number: process.env.PR_NUMBER ?? 0,
      generated_at: new Date().toISOString(),
      duration_ms: 0,
    };
    console.log("=== DRY-RUN: comment body ===");
    console.log(formatComment({ failures, meta, gates }));
    console.log("=== DRY-RUN: actions summary ===");
    console.log(formatActionsSummary({ failures, total_checks: failures.length, meta }));
    return;
  }

  const token = envOrThrow("GITHUB_TOKEN");
  const { owner, repo } = parseRepo();
  const prNumber = Number(envOrThrow("PR_NUMBER"));
  const commitSha = process.env.GITHUB_SHA ?? null;

  const octokit = new Octokit({ auth: token });

  const appendSummary = process.env.GITHUB_STEP_SUMMARY
    ? (s) => appendFileSync(process.env.GITHUB_STEP_SUMMARY, s + "\n", "utf8")
    : null;

  const result = await runBot({
    octokit,
    owner,
    repo,
    prNumber,
    gates,
    commitSha,
    appendSummary,
  });

  if (!result.ok) {
    console.error(`[release-gate:comment] FAIL (fallback posted): ${result.error?.message}`);
    process.exit(0); // do not fail the workflow — fallback comment is the signal.
  }

  console.log(
    `[release-gate:comment] OK — ${result.edited ? "edited" : "created"} comment, ${result.failures.length}/${result.total_checks} classified failures.`
  );
}

// Only run if invoked directly (not imported by tests)
if (import.meta.url === `file://${process.argv[1].replace(/\\/g, "/")}` ||
    import.meta.url === `file:///${process.argv[1].replace(/\\/g, "/")}`) {
  main().catch((err) => {
    console.error(`[release-gate:comment] CRASH: ${err.stack ?? err.message}`);
    process.exit(2);
  });
}
