/**
 * a11y-summarize CLI — invoked by `test:a11y:e2e` after Playwright finishes.
 *
 * Reads a Playwright JSON report from argv[2], classifies failures via the
 * `summarize` library, and:
 *   - appends the markdown summary to `$GITHUB_STEP_SUMMARY` when set
 *     (or prints it to stdout otherwise — useful for local runs)
 *   - prints workflow annotations (`::error::` / `::warning::`) on stdout
 *     so GitHub Actions picks them up and surfaces them on the Files tab
 *   - exits with the categorized exit code (0/1/2 — see scripts/a11y-summarize.ts)
 *
 * Spec: issue #1698.
 */

import { appendFileSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { summarize } from './a11y-summarize';

function fail(msg: string, code = 64): never {
  process.stderr.write(`a11y-summarize.cli: ${msg}\n`);
  process.exit(code);
}

function main(): void {
  const reportPathArg = process.argv[2];
  if (!reportPathArg) {
    fail('usage: a11y-summarize.cli <playwright-report.json>');
  }

  const reportPath = resolve(reportPathArg);
  let raw: string;
  try {
    raw = readFileSync(reportPath, 'utf8');
  } catch (e) {
    const reason = e instanceof Error ? e.message : String(e);
    fail(`unable to read report at ${reportPath}: ${reason}`, 65);
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (e) {
    const reason = e instanceof Error ? e.message : String(e);
    fail(`report at ${reportPath} is not valid JSON: ${reason}`, 65);
  }

  let result;
  try {
    result = summarize(parsed);
  } catch (e) {
    const reason = e instanceof Error ? e.message : String(e);
    fail(`summarize failed: ${reason}`, 65);
  }

  // Summary → $GITHUB_STEP_SUMMARY (GitHub Actions) or stdout (local).
  const stepSummaryFile = process.env.GITHUB_STEP_SUMMARY;
  if (stepSummaryFile) {
    appendFileSync(stepSummaryFile, result.summary, 'utf8');
  } else {
    process.stdout.write(result.summary);
  }

  // Annotations → stdout so GitHub Actions interprets them.
  for (const line of result.annotations) {
    process.stdout.write(`${line}\n`);
  }

  process.exit(result.exitCode);
}

main();
