#!/usr/bin/env tsx
/**
 * WS-F.1 — Mockup ownership header validator (AC-F.2 progressive enforcement).
 *
 * Walks `admin-mockups/design_files/*.html` and validates the `@route /...`
 * structured header block per AC-F.5 enum normalization.
 *
 * Enforcement strategy (AC-F.1b):
 *   - Files in `ALLOWLIST` → header missing/invalid is an ERROR (exit 1).
 *   - Other files → warnings printed but exit code stays 0.
 *   - Files completely without an `@route` comment AND not in allowlist are
 *     skipped silently (legacy mockups not yet adopted; will be flagged in
 *     later F phases as the allowlist expands).
 *
 * The allowlist grows by explicit edit to this file (PR review gate). It is
 * intentionally a constant, not derived from `mockup-ownership.json`, so the
 * adoption pace is visible in git history.
 *
 * Refs: #1072 (WS-F), #1066 (umbrella), AC-F.1b + AC-F.2 + AC-F.5.
 */
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

import { parseMockupHeader } from './parse-mockup-header';

const HERE = dirname(fileURLToPath(import.meta.url));
const DEFAULT_MOCKUPS_DIR = resolve(HERE, '../../../admin-mockups/design_files');

/**
 * 5 mockup canonical core (AC-F.1b). Headers REQUIRED on these.
 *
 * Expansion process: open a PR adding a file here; the file MUST have a valid
 * header at the time the PR is opened (otherwise this script fails).
 */
export const ALLOWLIST: readonly string[] = [
  'sp4-library-desktop.html',
  'nanolith-runthrough-game-detail.html',
  'sp4-game-detail.html',
  'sp4-agents-index.html',
  'sp4-sessions-index.html',
];

export interface CheckReport {
  file: string;
  level: 'error' | 'warning' | 'ok';
  message: string;
}

export function checkMockup(file: string, html: string, allowlist: readonly string[]): CheckReport {
  const name = basename(file);
  const isAllowlisted = allowlist.includes(name);
  const result = parseMockupHeader(html);

  if (!result.ok) {
    if (isAllowlisted) {
      return {
        file,
        level: 'error',
        message: `Header invalid: ${result.error}. Mockup is in the WS-F.1b allowlist; header MUST conform to AC-F.5.`,
      };
    }
    // Non-allowlist + no header: silently skip (don't even warn — too noisy).
    if (/no html comment containing @route/i.test(result.error)) {
      return { file, level: 'ok', message: 'No header (non-allowlisted)' };
    }
    return {
      file,
      level: 'warning',
      message: `Header malformed (non-allowlisted): ${result.error}`,
    };
  }

  return {
    file,
    level: 'ok',
    message: `route=${result.header.route} status=${result.header.status} verified-by=${result.header.verifiedBy}`,
  };
}

export function runCli(opts: { mockupsDir?: string; allowlist?: readonly string[] } = {}): {
  ok: boolean;
  reports: CheckReport[];
} {
  const dir = opts.mockupsDir ?? DEFAULT_MOCKUPS_DIR;
  const list = opts.allowlist ?? ALLOWLIST;
  const files = readdirSync(dir)
    .filter(f => f.endsWith('.html'))
    .map(f => join(dir, f))
    .sort();

  const reports = files.map(f => checkMockup(f, readFileSync(f, 'utf8'), list));
  const errors = reports.filter(r => r.level === 'error');
  return { ok: errors.length === 0, reports };
}

const invokedAsCli =
  typeof process !== 'undefined' &&
  process.argv[1] &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1]);

if (invokedAsCli) {
  const json = process.argv.includes('--json');
  const { ok, reports } = runCli();
  const errors = reports.filter(r => r.level === 'error');
  const warnings = reports.filter(r => r.level === 'warning');
  const oks = reports.filter(r => r.level === 'ok');

  if (json) {
    console.log(JSON.stringify({ ok, reports }, null, 2));
  } else {
    if (errors.length > 0) {
      console.error('\n❌ Errors (allowlist):');
      for (const r of errors) console.error(`  ${r.file}\n    ${r.message}`);
    }
    if (warnings.length > 0) {
      console.warn('\n⚠️  Warnings (non-allowlist):');
      for (const r of warnings) console.warn(`  ${r.file}\n    ${r.message}`);
    }
    console.log(
      `\n${ok ? '✓' : '✗'} ${reports.length} mockups scanned: ` +
        `${errors.length} error, ${warnings.length} warning, ${oks.length} ok`
    );
  }
  process.exit(ok ? 0 : 1);
}
