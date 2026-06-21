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
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { execSync } from "node:child_process";

import { Octokit } from "@octokit/rest";
import yaml from "js-yaml";

import { decideRevertAction, COOLDOWN_MS_DEFAULT } from "./lib/auto-revert.mjs";
import { loadGates as loadClassifyGates, classify as classifyCheck } from "./lib/classify.mjs";
import { pickLatestBotComment, parseBotComment } from "./lib/parse-bot-comment.mjs";
import { parseEventLog, serializeEvent } from "./lib/auto-revert-events.mjs";

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

const FIX_FORWARD_TITLE_REGEX = /^(revert|fix|hotfix)(\(\S+\))?:/i;
const FIX_FORWARD_LABEL = "release-fix-forward";
const REVERT_TITLE_PREFIX_REGEX = /^revert: /i;
const REVERT_BODY_LINK_REGEX = /Reverts #\d+|This reverts commit/;

async function fetchLatestMergedRelease(octokit, owner, repo) {
  const { data } = await octokit.pulls.list({
    owner,
    repo,
    state: "closed",
    base: "main-staging",
    sort: "updated",
    direction: "desc",
    per_page: 10,
  });
  const merged = data.find(p => p.merged_at != null);
  if (!merged) return null;

  const isAutoRevertPr = REVERT_TITLE_PREFIX_REGEX.test(merged.title) && REVERT_BODY_LINK_REGEX.test(merged.body || "");

  return {
    prNumber: merged.number,
    mergeSha: merged.merge_commit_sha,
    mergeTime: new Date(merged.merged_at),
    isAutoRevertPr,
    rawPr: merged,
  };
}

function fetchCurrentHeadSha() {
  // git ls-remote returns "<sha>\trefs/heads/main-staging"
  const out = execSync("git ls-remote origin refs/heads/main-staging", { encoding: "utf8" });
  return out.split("\t")[0].trim();
}

async function fetchClassifiedBlockers(octokit, owner, repo, headSha, gates) {
  const { data } = await octokit.checks.listForRef({
    owner,
    repo,
    ref: headSha,
    per_page: 100,
  });
  const failing = data.check_runs.filter(r => ["failure", "cancelled", "timed_out"].includes(r.conclusion));
  const blockers = [];
  for (const r of failing) {
    const cls = classifyCheck(r.name, gates);
    if (cls.severity === "blocker") {
      blockers.push({
        name: r.name,
        conclusion: r.conclusion,
        checkRunUrl: r.html_url,
        classifiedAt: new Date().toISOString(),
      });
    }
  }
  return blockers;
}

async function fetchPreMergeBotComment(octokit, owner, repo, prNumber) {
  const { data } = await octokit.issues.listComments({ owner, repo, issue_number: prNumber, per_page: 100 });
  const latest = pickLatestBotComment(data);
  if (!latest) return null;
  return parseBotComment(latest.body);
}

async function fetchFixForwards(octokit, owner, repo, mergeTimeIso) {
  const { data } = await octokit.pulls.list({
    owner,
    repo,
    state: "open",
    base: "main-staging",
    sort: "created",
    direction: "desc",
    per_page: 50,
  });
  // Filter to PRs created AFTER mergeTime + C1d match rule
  const fixForwards = [];
  const mergeTimeMs = new Date(mergeTimeIso).getTime();
  for (const pr of data) {
    if (new Date(pr.created_at).getTime() <= mergeTimeMs) continue;
    const hasLabel = pr.labels.some(l => l.name === FIX_FORWARD_LABEL);
    const titleMatch = FIX_FORWARD_TITLE_REGEX.test(pr.title);
    if (hasLabel) {
      fixForwards.push({ number: pr.number, matchedVia: "label", createdAt: new Date(pr.created_at) });
    } else if (titleMatch) {
      fixForwards.push({ number: pr.number, matchedVia: "title_prefix", createdAt: new Date(pr.created_at) });
    }
  }
  return fixForwards;
}

const STATE_BRANCH = process.env.STATE_BRANCH || "release-gate-state/auto-revert-events";
const STATE_FILE_REL = "state/auto-revert-events.jsonl";

const BOT_EMAIL = "41898282+github-actions[bot]@users.noreply.github.com";
const BOT_NAME = "github-actions[bot]";

function gitExec(cmd, opts = {}) {
  return execSync(cmd, { encoding: "utf8", stdio: ["pipe", "pipe", "pipe"], ...opts }).trim();
}

function ensureStateBranchCheckout() {
  // Try fetch first; if branch doesn't exist on remote, init empty
  try {
    gitExec(`git fetch origin ${STATE_BRANCH}`);
    gitExec(`git checkout -B ${STATE_BRANCH} origin/${STATE_BRANCH}`);
  } catch (err) {
    // First-time bootstrap — create orphan branch
    gitExec(`git checkout --orphan ${STATE_BRANCH}`);
    gitExec("git rm -rf . || true");
    mkdirSync(path.dirname(STATE_FILE_REL), { recursive: true });
    writeFileSync(STATE_FILE_REL, "");
    gitExec(`git add ${STATE_FILE_REL}`);
    gitExec(`git -c user.email="${BOT_EMAIL}" -c user.name="${BOT_NAME}" commit -m "chore: bootstrap auto-revert state branch"`);
    gitExec(`git push -u origin ${STATE_BRANCH}`);
  }
}

function readEventLog() {
  if (!existsSync(STATE_FILE_REL)) return [];
  const text = readFileSync(STATE_FILE_REL, "utf8");
  return parseEventLog(text);
}

async function appendEventsWithRetry(newEvents, originalBranch) {
  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      gitExec(`git fetch origin ${STATE_BRANCH}`);
      gitExec(`git reset --hard origin/${STATE_BRANCH}`);

      // Re-read latest events post-reset
      const fresh = readEventLog();
      // Idempotency: drop any newEvents whose eventId already exists upstream
      const existingIds = new Set(fresh.map(e => e.eventId));
      const toAppend = newEvents.filter(e => !existingIds.has(e.eventId));
      if (toAppend.length === 0) {
        logJson({ level: "info", ts: new Date().toISOString(), event_type: "jsonl_appended", note: "no_new_events_after_dedup", latency_ms: 0 });
        return;
      }

      let text = "";
      try { text = readFileSync(STATE_FILE_REL, "utf8"); } catch {}
      for (const ev of toAppend) text += serializeEvent(ev);
      writeFileSync(STATE_FILE_REL, text);

      gitExec(`git add ${STATE_FILE_REL}`);
      gitExec(`git -c user.email="${BOT_EMAIL}" -c user.name="${BOT_NAME}" commit -m "chore(events): append ${toAppend.length} auto-revert event(s)"`);
      gitExec(`git push origin ${STATE_BRANCH}`);

      logJson({ level: "info", ts: new Date().toISOString(), event_type: "jsonl_appended", count: toAppend.length, latency_ms: 0 });
      return;
    } catch (err) {
      logJson({ level: "warn", ts: new Date().toISOString(), event_type: "jsonl_push_retry", attempt, error: err.message, latency_ms: 0 });
      if (attempt === 3) throw err;
      await new Promise(r => setTimeout(r, attempt * 5000));
    }
  }
}

function restoreOriginalBranch(originalBranch) {
  try { gitExec(`git checkout ${originalBranch}`); } catch {}
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
