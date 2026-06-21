#!/usr/bin/env node
// scripts/release-gate/reconcile-revert-outcomes.mjs
// Phase 2b (#1445) — weekly reconciler for auto-revert outcomes.
//
// 3 modes:
//   (default)        Reconcile + write outcome_updated events into JSONL
//   --report-only    Print Phase 2b maturity report (AC-5), no writes
//   --metrics-only   Print AC-7 false-revert rate (JSON one-line), no writes

import { execSync } from "node:child_process";
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { Octokit } from "@octokit/rest";

import { parseEventLog, reconcileOutcomes, serializeEvent } from "./lib/auto-revert-events.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");
const STATE_BRANCH = process.env.STATE_BRANCH || "release-gate-state/auto-revert-events";
const STATE_FILE_REL = "state/auto-revert-events.jsonl";
const BOT_EMAIL = "41898282+github-actions[bot]@users.noreply.github.com";
const BOT_NAME = "github-actions[bot]";

function gitExec(cmd) {
  return execSync(cmd, { encoding: "utf8" }).trim();
}

function envOrThrow(name) {
  const v = process.env[name];
  if (!v) throw new Error(`Missing env var: ${name}`);
  return v;
}

function parseRepo() {
  const slug = envOrThrow("GITHUB_REPOSITORY");
  const [owner, repo] = slug.split("/");
  return { owner, repo };
}

async function fetchRevertPRs(octokit, owner, repo) {
  const sinceISO = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
  const { data } = await octokit.search.issuesAndPullRequests({
    q: `repo:${owner}/${repo} is:pr label:auto-revert label:phase2b is:closed merged:>${sinceISO}`,
    per_page: 100,
  });
  const out = [];
  for (const item of data.items) {
    const { data: prDetail } = await octokit.pulls.get({ owner, repo, pull_number: item.number });
    out.push({
      number: prDetail.number,
      state: prDetail.merged_at ? "merged" : prDetail.state,
      mergedAt: prDetail.merged_at ? new Date(prDetail.merged_at) : null,
      labels: prDetail.labels.map(l => l.name),
      createdAt: new Date(prDetail.created_at),
    });
  }
  return out;
}

function readEventLog() {
  if (!existsSync(STATE_FILE_REL)) return [];
  return parseEventLog(readFileSync(STATE_FILE_REL, "utf8"));
}

function computeMetrics(events, now) {
  const windowMs = 30 * 24 * 60 * 60 * 1000;
  const cutoff = now.getTime() - windowMs;
  const inWindow = events.filter(e => new Date(e.timestamp).getTime() >= cutoff && e.mode === "live");

  const opened = inWindow.filter(e => e.eventType === "revert_opened").length;
  const updates = inWindow.filter(e => e.eventType === "outcome_updated");
  const trueConfirmed = updates.filter(e => e.newOutcome === "true_positive_confirmed").length;
  const falsePositive = updates.filter(e => e.newOutcome === "false_positive").length;
  const pending = opened - trueConfirmed - falsePositive;

  const rate = opened > 0 ? falsePositive / opened : 0;
  const threshold = 0.02;
  return {
    ts: now.toISOString(),
    window_days: 30,
    mode: "live",
    total_reverts: opened,
    true_positive_confirmed: trueConfirmed,
    false_positive: falsePositive,
    pending,
    false_revert_rate: rate,
    threshold,
    breach: rate > threshold && opened > 0,
  };
}

async function main() {
  const args = process.argv.slice(2);
  const reportOnly = args.includes("--report-only");
  const metricsOnly = args.includes("--metrics-only");
  const now = process.env.AUTO_REVERT_TEST_NOW ? new Date(process.env.AUTO_REVERT_TEST_NOW) : new Date();

  // Switch to state branch
  const originalBranch = gitExec("git rev-parse --abbrev-ref HEAD");
  try {
    gitExec(`git fetch origin ${STATE_BRANCH}`);
    gitExec(`git checkout -B ${STATE_BRANCH} origin/${STATE_BRANCH}`);
  } catch {
    console.error("State branch not found — has Phase A bootstrap run?");
    process.exit(2);
  }

  const events = readEventLog();

  if (metricsOnly) {
    const metrics = computeMetrics(events, now);
    console.log(JSON.stringify(metrics));
    try { gitExec(`git checkout ${originalBranch}`); } catch {}
    return;
  }

  if (reportOnly) {
    const metrics = computeMetrics(events, now);
    const dryRunEvents = events.filter(e => e.mode === "dry_run");
    const drOpened = dryRunEvents.filter(e => e.eventType === "revert_opened").length;
    const drConfirmed = dryRunEvents.filter(e => e.eventType === "outcome_updated" && e.newOutcome === "true_positive_confirmed").length;
    const drFalse = dryRunEvents.filter(e => e.eventType === "outcome_updated" && e.newOutcome === "false_positive").length;
    const drAborted = dryRunEvents.filter(e => e.eventType === "revert_aborted" || e.eventType === "revert_aborted_at_merge").length;
    const drPending = drOpened - drConfirmed - drFalse;
    const exitReady = drFalse === 0 && drConfirmed >= 1;
    console.log(`=== Phase 2b Dry-Run Maturity Report ===\n`);
    console.log(`Counts (mode=dry_run only):`);
    console.log(`  revert_opened:                 ${drOpened}`);
    console.log(`  true_positive_confirmed:       ${drConfirmed}`);
    console.log(`  false_positive:                ${drFalse}  ${drFalse > 0 ? "← BLOCKS exit" : ""}`);
    console.log(`  pending (< 7gg):               ${drPending}`);
    console.log(`  aborted (any reason):          ${drAborted}\n`);
    console.log(`Exit gate (AC-5):`);
    console.log(`  ${drFalse === 0 ? "✓" : "✗"} 0 false-reverts                              (need: 0)`);
    console.log(`  ${drConfirmed >= 1 ? "✓" : "✗"} 1+ true-positive validated                   (need: ≥1)\n`);
    console.log(`DECISION: ${exitReady ? "ready to flip phase2b.dry_run_mode=false" : "NOT READY"}`);
    try { gitExec(`git checkout ${originalBranch}`); } catch {}
    return;
  }

  // Default: reconcile + write
  const octokit = new Octokit({ auth: envOrThrow("GITHUB_TOKEN") });
  const { owner, repo } = parseRepo();
  const revertPRs = await fetchRevertPRs(octokit, owner, repo);
  const newEvents = reconcileOutcomes(revertPRs, events, now);

  if (newEvents.length === 0) {
    console.log("No new outcome events to emit");
    try { gitExec(`git checkout ${originalBranch}`); } catch {}
    return;
  }

  let text = "";
  try { text = readFileSync(STATE_FILE_REL, "utf8"); } catch {}
  for (const ev of newEvents) text += serializeEvent(ev);
  if (!existsSync(path.dirname(STATE_FILE_REL))) mkdirSync(path.dirname(STATE_FILE_REL), { recursive: true });
  writeFileSync(STATE_FILE_REL, text);

  gitExec(`git add ${STATE_FILE_REL}`);
  gitExec(`git -c user.email="${BOT_EMAIL}" -c user.name="${BOT_NAME}" commit -m "chore(events): reconcile ${newEvents.length} outcome event(s)"`);
  gitExec(`git push origin ${STATE_BRANCH}`);

  console.log(`Reconciled ${newEvents.length} outcome events`);
  try { gitExec(`git checkout ${originalBranch}`); } catch {}
}

export { main, computeMetrics };

if (import.meta.url === `file://${process.argv[1]}` || import.meta.url.endsWith(process.argv[1])) {
  main().catch(err => { console.error(err); process.exit(1); });
}
