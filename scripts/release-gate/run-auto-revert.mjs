#!/usr/bin/env node
// scripts/release-gate/run-auto-revert.mjs
// Phase 2b (#1445) — imperative CLI shell for auto-revert decision tick.
//
// Hydrates DecisionInput from GitHub APIs + JSONL state + .github/release-gates.yml,
// dispatches a decideRevertAction(), then executes the imperative side effects:
//   - git revert + push branch
//   - gh pr create
//   - C3b re-check fix-forward
//   - gh pr merge --admin --squash (skipped in dry-run)
//   - Slack POST (soft-fail)
//   - JSONL event append on side branch (retry pattern)
//
// Env (all required unless noted):
//   GITHUB_TOKEN              Provided automatically in GH Actions
//   GITHUB_REPOSITORY         e.g. "meepleAi-app/meepleai-monorepo"
//   GITHUB_RUN_ID             Auto-set by GH Actions
//   GITHUB_SERVER_URL         Auto-set by GH Actions
//   SLACK_RELEASE_WEBHOOK_URL Slack incoming webhook (soft-fail if missing)
//   STATE_BRANCH              Optional override (default "release-gate-state/auto-revert-events")
//   DRY_RUN                   If "1", print intent + skip ALL writes
//   AUTO_REVERT_CLOCK_SOURCE  If "test", reads AUTO_REVERT_TEST_NOW for `now`

import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

import { Octokit } from "@octokit/rest";
import yaml from "js-yaml";

import { decideRevertAction, COOLDOWN_MS_DEFAULT } from "./lib/auto-revert.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");
const RELEASE_GATES_YAML = path.join(REPO_ROOT, ".github", "release-gates.yml");

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

function getNow() {
  if (process.env.AUTO_REVERT_CLOCK_SOURCE === "test" && process.env.AUTO_REVERT_TEST_NOW) {
    return new Date(process.env.AUTO_REVERT_TEST_NOW);
  }
  return new Date();
}

function loadGates() {
  const text = readFileSync(RELEASE_GATES_YAML, "utf8");
  return yaml.load(text);
}

function logJson(line) {
  console.log(JSON.stringify(line));
}

async function main() {
  const startTs = Date.now();
  const runUrl = `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`;
  const now = getNow();

  logJson({ level: "info", ts: now.toISOString(), event_type: "tick_start", workflow_run_id: process.env.GITHUB_RUN_ID, latency_ms: 0 });

  const gates = loadGates();
  const phase2b = gates.bot?.phase2b;

  // Quick kill-switch short-circuit — DON'T even build Octokit
  if (!phase2b || phase2b.enabled !== true) {
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "kill_switch_active", latency_ms: Date.now() - startTs });
    return;
  }

  const dryRunMode = phase2b.dry_run_mode !== false; // default true if missing

  // Skeleton scope: ONLY kill-switch short-circuit + tick_start/tick_end logs.
  // Hydration + decision dispatch + execution shell added in Tasks 17-19.
  logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "skeleton_only", latency_ms: Date.now() - startTs });
}

main().catch(err => {
  logJson({ level: "error", ts: new Date().toISOString(), event_type: "tick_end", error: err.message, stack: err.stack });
  process.exit(1);
});
