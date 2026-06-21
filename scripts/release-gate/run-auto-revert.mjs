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
import { parseEventLog, serializeEvent, buildEventId } from "./lib/auto-revert-events.mjs";

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

const SLACK_WEBHOOK = process.env.SLACK_RELEASE_WEBHOOK_URL || "";

async function postSlack(text, dryRun) {
  if (!SLACK_WEBHOOK) {
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "slack_sent", note: "webhook_not_configured", latency_ms: 0 });
    return;
  }
  try {
    const res = await fetch(SLACK_WEBHOOK, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ text }),
    });
    if (!res.ok) throw new Error(`Slack POST ${res.status}`);
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "slack_sent", latency_ms: 0 });
  } catch (err) {
    logJson({ level: "warn", ts: new Date().toISOString(), event_type: "slack_failed", error: err.message, latency_ms: 0 });
    // soft-fail per AC-8 spec
  }
}

function renderSlackLive(originalPr, revertPr, blockerName, conclusion, runUrl) {
  return `:warning: Auto-revert fired — PR #${originalPr} reverted via #${revertPr}\nOriginal blocker: \`${blockerName}\` (${conclusion})\nRationale: 15-min cooldown elapsed · no fix-forward · SHA pinned · new (not pre-existing)\nAudit: ${runUrl}`;
}

function renderSlackDryRun(originalPr, revertPr, blockerName, conclusion, runUrl, revertPrUrl) {
  return `[DRY-RUN] :test_tube: Auto-revert would fire — PR #${originalPr} would be reverted (DRAFT #${revertPr})\nOriginal blocker: \`${blockerName}\` (${conclusion})\nAudit: ${runUrl}\nOperator review: ${revertPrUrl}`;
}

function renderSlackRaceAbort(originalPr, revertPr, raceWindowMs, fixForwardPr, runUrl) {
  return `:no_entry: Auto-revert aborted at merge — PR #${originalPr} revert (#${revertPr}) closed\nReason: fix-forward race detected at T+${raceWindowMs}ms\nFix-forward PR: #${fixForwardPr}\nAudit: ${runUrl}`;
}

function renderRevertPrBody({ originalPr, runUrl, blockerCheck, rationale, dryRunMode }) {
  const banner = dryRunMode ? "## [DRY-RUN] No merge will happen — operator review only\n\n" : "";
  return `${banner}## Auto-revert PR (Phase 2b #1445)

**Workflow run**: ${runUrl}
**Original PR**: #${originalPr}
**Classification snapshot**:
\`\`\`json
${JSON.stringify(blockerCheck, null, 2)}
\`\`\`
**Blocker check**: [${blockerCheck.name}](${blockerCheck.checkRunUrl}) (${blockerCheck.conclusion})
**Decision rationale**:
- Cooldown elapsed: ${rationale.cooldownElapsedMs}ms (threshold ${COOLDOWN_MS_DEFAULT}ms)
- No fix-forward detected pre-create
- SHA pinned at decision time: \`${rationale.shaPinned}\`
- AC-12 isNewBlocker: ${rationale.isNewBlocker} (NOT pre-existing override-accepted)
- Cascade check: ${rationale.cascadeCheck}
- All NEW blockers (${rationale.newBlockerCount}): ${rationale.allNewBlockers.join(", ")}

---
🤖 Generated by [Release-gate auto-revert Phase 2b](${runUrl})`;
}

async function main() {
  const startTs = Date.now();
  const runUrl = `${process.env.GITHUB_SERVER_URL}/${process.env.GITHUB_REPOSITORY}/actions/runs/${process.env.GITHUB_RUN_ID}`;
  const now = getNow();

  logJson({ level: "info", ts: now.toISOString(), event_type: "tick_start", workflow_run_id: process.env.GITHUB_RUN_ID, latency_ms: 0 });

  const gates = loadGates();
  const phase2b = gates.bot?.phase2b;
  if (!phase2b || phase2b.enabled !== true) {
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "kill_switch_active", latency_ms: Date.now() - startTs });
    return;
  }
  const dryRunMode = phase2b.dry_run_mode !== false;

  const octokit = new Octokit({ auth: envOrThrow("GITHUB_TOKEN") });
  const { owner, repo } = parseRepo();

  // ── Hydrate DecisionInput ──
  const latestMergedRelease = await fetchLatestMergedRelease(octokit, owner, repo);
  if (!latestMergedRelease) {
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "noop_no_recent_merge", latency_ms: Date.now() - startTs });
    return;
  }

  const currentHeadSha = fetchCurrentHeadSha();
  const classifyGates = loadClassifyGates(RELEASE_GATES_YAML);
  const blockers = await fetchClassifiedBlockers(octokit, owner, repo, currentHeadSha, classifyGates);
  const preMergeBotComment = await fetchPreMergeBotComment(octokit, owner, repo, latestMergedRelease.prNumber);
  const fixForwards = await fetchFixForwards(octokit, owner, repo, latestMergedRelease.mergeTime.toISOString());

  // Switch to state branch to read JSONL
  const originalBranch = gitExec("git rev-parse --abbrev-ref HEAD");
  ensureStateBranchCheckout();
  const jsonlEvents = readEventLog();

  const decision = decideRevertAction({
    killSwitchEnabled: true,
    dryRunMode,
    latestMergedRelease,
    currentHeadSha,
    cooldownMs: COOLDOWN_MS_DEFAULT,
    now,
    blockers,
    preMergeBotComment,
    fixForwards,
    jsonlEvents,
  });

  logJson({ level: "info", ts: new Date().toISOString(), event_type: "decision_made", decision: decision.action, outcome: decision.reason || decision.action, merge_sha: latestMergedRelease.mergeSha, latency_ms: Date.now() - startTs });

  if (decision.action === "noop_no_blockers" || decision.action === "noop_idempotent" || decision.action === "noop_no_recent_merge") {
    restoreOriginalBranch(originalBranch);
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: decision.action, latency_ms: Date.now() - startTs });
    return;
  }

  if (decision.action === "abort") {
    // Emit revert_aborted event
    const ts = new Date().toISOString();
    const abortEvent = {
      schemaVersion: 1,
      eventId: buildEventId("revert_aborted", latestMergedRelease.mergeSha, ts),
      eventType: "revert_aborted",
      timestamp: ts,
      runUrl,
      mode: dryRunMode ? "dry_run" : "live",
      originalPr: latestMergedRelease.prNumber,
      mergeSha: latestMergedRelease.mergeSha,
      blockerCheck: blockers[0] ? { name: blockers[0].name, conclusion: blockers[0].conclusion } : null,
      abortReason: decision.reason,
      outcome: decision.reason,
    };
    await appendEventsWithRetry([abortEvent], originalBranch);
    restoreOriginalBranch(originalBranch);
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: decision.reason, latency_ms: Date.now() - startTs });
    return;
  }

  // decision.action === "open_revert" — proceed with imperative shell
  const targetBlocker = decision.blockerCheck;

  // Switch back to original branch for git revert
  restoreOriginalBranch(originalBranch);

  const revertBranch = `revert/auto-${latestMergedRelease.mergeSha.slice(0, 8)}`;
  gitExec(`git checkout -B ${revertBranch} origin/main-staging`);
  gitExec(`git revert -m 1 --no-edit ${latestMergedRelease.mergeSha}`);
  gitExec(`git push -u origin ${revertBranch}`);

  const prTitle = `${dryRunMode ? "[DRY-RUN] " : ""}revert: auto-revert #${latestMergedRelease.prNumber} — blocker ${targetBlocker.name}`;
  const prBody = renderRevertPrBody({
    originalPr: latestMergedRelease.prNumber,
    runUrl,
    blockerCheck: targetBlocker,
    rationale: decision.rationale,
    dryRunMode,
  });
  const labelsToAdd = dryRunMode ? ["auto-revert", "phase2b", "dry-run"] : ["auto-revert", "phase2b"];

  const { data: createdPr } = await octokit.pulls.create({
    owner,
    repo,
    title: prTitle,
    head: revertBranch,
    base: "main-staging",
    body: prBody,
    draft: dryRunMode,
  });
  await octokit.issues.addLabels({ owner, repo, issue_number: createdPr.number, labels: labelsToAdd });

  logJson({ level: "info", ts: new Date().toISOString(), event_type: "pr_created", pr_number: createdPr.number, latency_ms: Date.now() - startTs });

  // Append revert_opened event
  const openedTs = new Date().toISOString();
  const openedEvent = {
    schemaVersion: 1,
    eventId: buildEventId("revert_opened", latestMergedRelease.mergeSha, openedTs),
    eventType: "revert_opened",
    timestamp: openedTs,
    runUrl,
    mode: dryRunMode ? "dry_run" : "live",
    originalPr: latestMergedRelease.prNumber,
    revertPr: createdPr.number,
    mergeSha: latestMergedRelease.mergeSha,
    blockerCheck: targetBlocker,
    decisionRationale: decision.rationale,
    outcome: "true_positive_pending",
  };

  ensureStateBranchCheckout();
  await appendEventsWithRetry([openedEvent], originalBranch);
  restoreOriginalBranch(originalBranch);

  // C3b — re-check fix-forward right before merge
  const fixForwardsAtMerge = await fetchFixForwards(octokit, owner, repo, latestMergedRelease.mergeTime.toISOString());
  if (fixForwardsAtMerge.length > 0) {
    const raceWindowMs = Date.now() - startTs;
    const raceFf = fixForwardsAtMerge[0];

    await octokit.issues.createComment({
      owner, repo, issue_number: createdPr.number,
      body: `Aborted by fix-forward race detected at T+${raceWindowMs}ms. Fix-forward PR: #${raceFf.number} (matched via ${raceFf.matchedVia}). Closing this auto-revert PR.`,
    });
    await octokit.pulls.update({ owner, repo, pull_number: createdPr.number, state: "closed" });

    logJson({ level: "info", ts: new Date().toISOString(), event_type: "pr_closed_race", pr_number: createdPr.number, latency_ms: Date.now() - startTs });

    const ts = new Date().toISOString();
    const raceEvent = {
      schemaVersion: 1,
      eventId: buildEventId("revert_aborted_at_merge", latestMergedRelease.mergeSha, ts),
      eventType: "revert_aborted_at_merge",
      timestamp: ts,
      runUrl,
      mode: dryRunMode ? "dry_run" : "live",
      originalPr: latestMergedRelease.prNumber,
      revertPr: createdPr.number,
      mergeSha: latestMergedRelease.mergeSha,
      raceWindowMs,
      detectedFixForward: { number: raceFf.number, titleOrLabel: raceFf.matchedVia },
      outcome: "aborted_fix_forward_race",
    };
    ensureStateBranchCheckout();
    await appendEventsWithRetry([raceEvent], originalBranch);
    restoreOriginalBranch(originalBranch);

    await postSlack(renderSlackRaceAbort(latestMergedRelease.prNumber, createdPr.number, raceWindowMs, raceFf.number, runUrl), dryRunMode);
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "aborted_fix_forward_race", latency_ms: Date.now() - startTs });
    return;
  }

  // Dry-run mode → SKIP merge
  if (dryRunMode) {
    await postSlack(renderSlackDryRun(latestMergedRelease.prNumber, createdPr.number, targetBlocker.name, targetBlocker.conclusion, runUrl, createdPr.html_url), true);
    logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "dry_run_pr_opened", latency_ms: Date.now() - startTs });
    return;
  }

  // Live mode — merge --admin --squash
  await octokit.pulls.merge({
    owner, repo,
    pull_number: createdPr.number,
    merge_method: "squash",
  });
  logJson({ level: "info", ts: new Date().toISOString(), event_type: "pr_merged", pr_number: createdPr.number, latency_ms: Date.now() - startTs });

  await postSlack(renderSlackLive(latestMergedRelease.prNumber, createdPr.number, targetBlocker.name, targetBlocker.conclusion, runUrl), false);

  logJson({ level: "info", ts: new Date().toISOString(), event_type: "tick_end", outcome: "true_positive_pending", latency_ms: Date.now() - startTs });
}

main().catch(err => {
  logJson({ level: "error", ts: new Date().toISOString(), event_type: "tick_end", error: err.message, stack: err.stack });
  process.exit(1);
});
