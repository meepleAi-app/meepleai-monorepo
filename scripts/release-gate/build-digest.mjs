#!/usr/bin/env node
// CLI entry — fetches release PRs from the past 7 days, parses bot comments,
// builds a Slack digest, posts it, and writes a state PR with the updated
// state/release-gate-digest.json.
//
// Env (all required unless noted):
//   GITHUB_TOKEN                  Provided automatically in GH Actions
//   GITHUB_REPOSITORY             e.g. "meepleAi-app/meepleai-monorepo"
//   SLACK_GITNOTIFY_WEBHOOK_URL   Slack incoming webhook (required unless DRY_RUN)
//   DRY_RUN                       If "1", print Slack payload to stdout + skip state PR
//   RELEASE_GATES_YAML            Optional override path
//   STATE_FILE                    Optional override path (default state/release-gate-digest.json)
//   GITHUB_STEP_SUMMARY           Auto-set in GH Actions runner
//
// Tested via lib/digest-builder.test.mjs + lib/parse-bot-comment.test.mjs.
// The CLI itself is a thin orchestrator (≤ 80 LOC of actual logic).

import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync, writeFileSync, appendFileSync, existsSync, mkdirSync } from "node:fs";

import { Octokit } from "@octokit/rest";

import { loadGates } from "./lib/classify.mjs";
import { parseBotComment, pickLatestBotComment } from "./lib/parse-bot-comment.mjs";
import { buildDigest, EMPTY_STATE } from "./lib/digest-builder.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..", "..");
const DEFAULT_STATE_FILE = path.join(REPO_ROOT, "state", "release-gate-digest.json");
const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;

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

function loadState(filePath) {
  if (!existsSync(filePath)) return EMPTY_STATE;
  try {
    return JSON.parse(readFileSync(filePath, "utf8"));
  } catch (err) {
    console.warn(`[release-gate:digest] warning — state file unreadable: ${err.message}`);
    return EMPTY_STATE;
  }
}

function writeState(filePath, state) {
  const dir = path.dirname(filePath);
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  writeFileSync(filePath, JSON.stringify(state, null, 2) + "\n", "utf8");
}

async function fetchReleasePrs({ octokit, owner, repo, since }) {
  // List recent PRs targeting main-staging or main (open or merged).
  // Use search API to filter by base branch + updated date.
  const sinceISO = since.toISOString().slice(0, 10);
  const q = `repo:${owner}/${repo} is:pr updated:>=${sinceISO} (base:main-staging OR base:main)`;
  const res = await octokit.search.issuesAndPullRequests({ q, per_page: 50 });
  return (res.data?.items ?? []).map((it) => ({
    number: it.number,
    title: it.title,
    mergedAt: it.pull_request?.merged_at ?? null,
    baseRef: null, // search result does not include base ref; we filter via query.
  }));
}

async function fetchPrFailures({ octokit, owner, repo, prNumber }) {
  const res = await octokit.issues.listComments({
    owner,
    repo,
    issue_number: prNumber,
    per_page: 100,
  });
  const latest = pickLatestBotComment(res.data ?? []);
  if (!latest) return null;
  return parseBotComment(latest.body) ?? null;
}

async function postSlack({ webhookUrl, payload, fetcher = fetch }) {
  // 3 attempts with 1s/2s backoff between retries (no sleep after the final
  // attempt — that wall-clock is wasted because the loop is about to exit).
  const delays = [1000, 2000];
  let lastErr = null;
  for (let attempt = 0; attempt < 3; attempt++) {
    const isLast = attempt === 2;
    try {
      const res = await fetcher(webhookUrl, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (res.ok) return { ok: true, attempt };
      if (res.status === 429 || res.status >= 500) {
        lastErr = new Error(`Slack ${res.status}`);
        if (!isLast) await new Promise((r) => setTimeout(r, delays[attempt]));
        continue;
      }
      const body = await res.text().catch(() => "");
      return { ok: false, error: `Slack ${res.status}: ${body.slice(0, 200)}` };
    } catch (err) {
      lastErr = err;
      if (!isLast) await new Promise((r) => setTimeout(r, delays[attempt]));
    }
  }
  return { ok: false, error: lastErr?.message ?? "Slack POST failed after 3 retries" };
}

async function openStatePr({ octokit, owner, repo, branchName, stateJson, weekISO, slackMessage }) {
  // Create a branch from main-dev, write state JSON, open PR. Audit-only —
  // does not auto-merge.
  const baseRef = await octokit.git.getRef({ owner, repo, ref: "heads/main-dev" });
  const baseSha = baseRef.data.object.sha;

  // Create branch (or reset existing one to current main-dev tip).
  // Re-running the same week before the previous state PR was merged would
  // otherwise leave the branch pointing at a stale base; updateRef with
  // `force: true` ensures we always write from a fresh main-dev tip.
  try {
    await octokit.git.createRef({
      owner,
      repo,
      ref: `refs/heads/${branchName}`,
      sha: baseSha,
    });
  } catch (err) {
    if (err.status !== 422) throw err;
    // 422 = ref exists; reset it to baseSha so the file commit lands on a
    // fresh base. force=true is required because the existing branch tip is
    // unrelated to baseSha (not an ancestor).
    await octokit.git.updateRef({
      owner,
      repo,
      ref: `heads/${branchName}`,
      sha: baseSha,
      force: true,
    });
  }

  // Get current state file SHA on the branch (if present) so we can update it
  let existingSha = null;
  try {
    const cur = await octokit.repos.getContent({
      owner,
      repo,
      path: "state/release-gate-digest.json",
      ref: branchName,
    });
    existingSha = cur.data?.sha ?? null;
  } catch (err) {
    if (err.status !== 404) throw err;
  }

  const content = Buffer.from(JSON.stringify(stateJson, null, 2) + "\n", "utf8").toString("base64");
  await octokit.repos.createOrUpdateFileContents({
    owner,
    repo,
    path: "state/release-gate-digest.json",
    message: `chore(release-gate): digest state ${weekISO}`,
    content,
    branch: branchName,
    sha: existingSha ?? undefined,
  });

  // Open PR (idempotent: search existing)
  const search = await octokit.pulls.list({
    owner,
    repo,
    head: `${owner}:${branchName}`,
    state: "open",
  });
  if (search.data && search.data.length > 0) {
    return search.data[0].html_url;
  }

  const prBody = [
    "Auto-generated by `.github/workflows/release-gate-digest.yml`.",
    "",
    `Week: \`${weekISO}\``,
    "",
    "## Slack digest posted",
    "",
    "```",
    slackMessage,
    "```",
    "",
    "**Action**: review the state diff and merge when ready. State PRs are NOT auto-merged.",
    "",
    "Closes (none — recurring chore).",
  ].join("\n");

  const created = await octokit.pulls.create({
    owner,
    repo,
    title: `chore(release-gate): digest state ${weekISO}`,
    head: branchName,
    base: "main-dev",
    body: prBody,
  });
  return created.data.html_url;
}

async function openFailureIssue({ octokit, owner, repo, weekISO, errorMessage }) {
  await octokit.issues.create({
    owner,
    repo,
    title: `[release-gate-digest][${weekISO}] FAILED`,
    body: [
      "Auto-generated by `.github/workflows/release-gate-digest.yml`.",
      "",
      `Week: \`${weekISO}\``,
      `Failure: \`${errorMessage}\``,
      "",
      "Workflow exited soft-fail. No retry scheduled (cron is weekly).",
    ].join("\n"),
    labels: ["release-gate-digest", "P2"],
  });
}

async function main() {
  const dryRun = process.env.DRY_RUN === "1" || process.env.DRY_RUN === "true";
  const yamlPath = process.env.RELEASE_GATES_YAML
    ? path.resolve(process.cwd(), process.env.RELEASE_GATES_YAML)
    : path.join(REPO_ROOT, ".github", "release-gates.yml");
  const stateFilePath = process.env.STATE_FILE
    ? path.resolve(process.cwd(), process.env.STATE_FILE)
    : DEFAULT_STATE_FILE;

  const gates = loadGates(yamlPath);
  const prevState = loadState(stateFilePath);
  const now = new Date();

  let prs = [];
  let octokit = null;

  if (!dryRun) {
    const token = envOrThrow("GITHUB_TOKEN");
    const { owner, repo } = parseRepo();
    octokit = new Octokit({ auth: token });
    const since = new Date(now.getTime() - SEVEN_DAYS_MS);
    const prSummaries = await fetchReleasePrs({ octokit, owner, repo, since });
    prs = [];
    for (const summary of prSummaries) {
      const failures = await fetchPrFailures({
        octokit,
        owner,
        repo,
        prNumber: summary.number,
      });
      if (failures === null) continue;
      prs.push({ ...summary, botFailures: failures });
    }
  } else {
    // Dry-run: use empty PR list unless DRY_RUN_PRS is provided as JSON
    if (process.env.DRY_RUN_PRS) {
      try {
        prs = JSON.parse(process.env.DRY_RUN_PRS);
      } catch (err) {
        console.warn(`[release-gate:digest] DRY_RUN_PRS not valid JSON: ${err.message}`);
      }
    }
  }

  const digest = buildDigest({ prs, prevState, now, gates });

  const stepSummary = process.env.GITHUB_STEP_SUMMARY;
  const appendSummary = stepSummary
    ? (s) => appendFileSync(stepSummary, s + "\n", "utf8")
    : null;

  if (digest.action === "skip") {
    const msg = `[release-gate:digest] SKIP (${digest.reason}) week=${digest.weekISO}`;
    console.log(msg);
    if (appendSummary) appendSummary(`## Release-gate Digest\n\nSkipped: \`${digest.reason}\``);
    return;
  }

  console.log(`[release-gate:digest] week=${digest.weekISO} action=post`);
  console.log(`  aggregates=${digest.aggregates.length} escalations=${digest.escalations.length}`);
  console.log(`  deltas: ${JSON.stringify(digest.deltas)}`);

  if (dryRun) {
    console.log("=== DRY-RUN: slack payload ===");
    console.log(digest.slackMessage);
    console.log("=== DRY-RUN: new state ===");
    console.log(JSON.stringify(digest.newState, null, 2));
    if (appendSummary) {
      appendSummary(`## Release-gate Digest (DRY-RUN)\n\nWeek: \`${digest.weekISO}\`\n\n\`\`\`\n${digest.slackMessage}\n\`\`\``);
    }
    return;
  }

  // Honor `bot.phase2c.slack_webhook_env` if set; falls back to
  // SLACK_GITNOTIFY_WEBHOOK_URL otherwise. Indirection lets operators rebind
  // the channel without code change.
  const webhookEnvName =
    gates?.bot?.phase2c?.slack_webhook_env ?? "SLACK_GITNOTIFY_WEBHOOK_URL";
  const webhookUrl = envOrThrow(webhookEnvName);
  const slackResult = await postSlack({
    webhookUrl,
    payload: { text: digest.slackMessage },
  });

  if (!slackResult.ok) {
    const { owner, repo } = parseRepo();
    await openFailureIssue({
      octokit,
      owner,
      repo,
      weekISO: digest.weekISO,
      errorMessage: slackResult.error,
    });
    console.error(`[release-gate:digest] Slack POST failed: ${slackResult.error}`);
    process.exit(1);
  }

  if (digest.openStatePr) {
    const { owner, repo } = parseRepo();
    const prUrl = await openStatePr({
      octokit,
      owner,
      repo,
      branchName: digest.statePrBranchName,
      stateJson: digest.newState,
      weekISO: digest.weekISO,
      slackMessage: digest.slackMessage,
    });
    console.log(`[release-gate:digest] state PR: ${prUrl}`);
    if (appendSummary) {
      appendSummary(
        `## Release-gate Digest\n\nWeek: \`${digest.weekISO}\`\n\nSlack: posted (attempt ${slackResult.attempt + 1})\n\nState PR: ${prUrl}`
      );
    }
  } else {
    // No warnings → still write state locally for tracking (but no PR)
    writeState(stateFilePath, digest.newState);
    if (appendSummary) {
      appendSummary(
        `## Release-gate Digest\n\nWeek: \`${digest.weekISO}\`\n\nAll green — no state PR opened.`
      );
    }
  }
}

// Only run if invoked directly (not imported)
if (import.meta.url === `file://${process.argv[1].replace(/\\/g, "/")}` ||
    import.meta.url === `file:///${process.argv[1].replace(/\\/g, "/")}`) {
  main().catch((err) => {
    console.error(`[release-gate:digest] CRASH: ${err.stack ?? err.message}`);
    process.exit(2);
  });
}

export { main, fetchReleasePrs, fetchPrFailures, postSlack, openStatePr };
