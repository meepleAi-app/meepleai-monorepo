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
    // shell:true on Windows so `pnpm.cmd` (a batch file) can be spawned — Node
    // 20.12+/22 hardened spawn (CVE-2024-27980) throws EINVAL on .cmd/.bat with
    // shell:false. POSIX keeps shell:false. Enables local `pnpm test:a11y:e2e`
    // iteration on Windows instead of ~20min CI rounds (issue #3289).
    const isWin = process.platform === 'win32';
    // cmd.exe word-splits array args on spaces, so quote any arg containing
    // whitespace (e.g. the absolute reportPath under "C:\\Users\\John Doe\\...")
    // — otherwise the summarize step receives a truncated path and exits 65.
    const spawnArgs = isWin ? args.map(a => (/\s/.test(a) ? `"${a}"` : a)) : args;
    const child = spawn(cmd, spawnArgs, { stdio: 'inherit', env, cwd: repoWeb, shell: isWin });
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
