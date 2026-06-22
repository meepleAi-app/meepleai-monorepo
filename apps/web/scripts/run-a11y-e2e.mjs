#!/usr/bin/env node
/**
 * run-a11y-e2e — wrapper around `playwright test e2e/a11y …` that pipes the
 * structured JSON report through scripts/a11y-summarize.cli.ts for
 * fail-class categorization (axe vs Playwright flake — issue #1698).
 *
 * Cross-platform: spawns Node processes directly instead of using shell
 * piping (`||`, `;`) so Windows + POSIX behave identically.
 *
 * Exit code is propagated from a11y-summarize.cli (0 = all green,
 * 1 = flake only, 2 = any axe AA violation). Playwright's own exit code
 * is intentionally ignored — we run summarize even on Playwright failure
 * to surface the structured output regardless.
 */

import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));
const repoWeb = resolve(__dirname, '..');

const reportDir = resolve(repoWeb, 'playwright-report-a11y');
const reportPath = resolve(reportDir, 'report.json');
mkdirSync(reportDir, { recursive: true });

const pnpmCmd = process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm';

const playwrightArgs = [
  'exec',
  'playwright',
  'test',
  'e2e/accessibility.spec.ts',
  'e2e/a11y',
  '--project=desktop-chrome',
  '--workers=1',
];

const playwrightEnv = {
  ...process.env,
  A11Y_JSON_REPORT_PATH: reportPath,
};

function run(cmd, args, env = process.env) {
  return new Promise((resolveExit) => {
    const child = spawn(cmd, args, { stdio: 'inherit', env, cwd: repoWeb, shell: false });
    child.on('exit', (code, signal) => {
      if (signal) {
        process.stderr.write(`run-a11y-e2e: ${cmd} killed by ${signal}\n`);
        resolveExit(128);
      } else {
        resolveExit(code ?? 0);
      }
    });
    child.on('error', (err) => {
      process.stderr.write(`run-a11y-e2e: failed to spawn ${cmd}: ${err.message}\n`);
      resolveExit(127);
    });
  });
}

const playwrightExit = await run(pnpmCmd, playwrightArgs, playwrightEnv);
process.stderr.write(`run-a11y-e2e: playwright exit=${playwrightExit}\n`);

const summarizeExit = await run(pnpmCmd, [
  'exec',
  'tsx',
  'scripts/a11y-summarize.cli.ts',
  reportPath,
]);

process.exit(summarizeExit);
