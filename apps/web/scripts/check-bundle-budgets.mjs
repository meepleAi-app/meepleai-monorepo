#!/usr/bin/env node
/**
 * check-bundle-budgets.mjs — Per-route First Load JS budget enforcement.
 *
 * Issue #629 (Wave A closeout Step 4). Reads Next.js 16 Turbopack diagnostic
 * output `.next/diagnostics/route-bundle-stats.json` (canonical per-route
 * `firstLoadChunkPaths` manifest), computes gzipped sum, validates against
 * `apps/web/.bundle-budgets.json`. Exits non-zero when any budgeted route
 * exceeds `budget * (1 + tolerance)`.
 *
 * Inputs:
 *   - `.next/diagnostics/route-bundle-stats.json` (Next.js build artifact)
 *   - `apps/web/.bundle-budgets.json` (committed budgets)
 *
 * Outputs:
 *   - stdout: human-readable per-route table (✅ / ⚠️ / ❌)
 *   - file: `.next/diagnostics/bundle-budget-report.json` (machine-readable
 *     summary; CI uploads as artifact)
 *   - exit code: 0 = all routes within budget; 1 = at least one exceeds
 *     `budget × (1 + tolerance)`; 2 = configuration error (missing file,
 *     unknown route, etc.)
 *
 * Usage:
 *   pnpm build && node scripts/check-bundle-budgets.mjs
 *   # or via package.json: pnpm bundle:check
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { gzipSync } from 'node:zlib';

const STATS_PATH = '.next/diagnostics/route-bundle-stats.json';
const BUDGETS_PATH = '.bundle-budgets.json';
const REPORT_PATH = '.next/diagnostics/bundle-budget-report.json';

function fail(msg, code = 2) {
  console.error(`❌ ${msg}`);
  process.exit(code);
}

function fmtKb(bytes) {
  return `${(bytes / 1024).toFixed(1)} KB`;
}

function fmtPct(ratio) {
  const sign = ratio >= 0 ? '+' : '';
  return `${sign}${(ratio * 100).toFixed(2)}%`;
}

if (!existsSync(STATS_PATH)) {
  fail(`Missing ${STATS_PATH}. Run \`pnpm build\` before \`bundle:check\`.`);
}
if (!existsSync(BUDGETS_PATH)) {
  fail(`Missing ${BUDGETS_PATH} (committed budgets file).`);
}

const stats = JSON.parse(readFileSync(STATS_PATH, 'utf8'));
const config = JSON.parse(readFileSync(BUDGETS_PATH, 'utf8'));

const tolerance = typeof config.tolerance === 'number' ? config.tolerance : 0.05;
const budgets = config.routes ?? {};

if (Object.keys(budgets).length === 0) {
  fail(`No routes in ${BUDGETS_PATH}.`);
}

const results = [];
let hasFailure = false;
let hasConfigError = false;

for (const [route, entry] of Object.entries(budgets)) {
  const budget = entry.gzippedBytes;
  if (typeof budget !== 'number' || budget <= 0) {
    console.error(`⚠️  ${route}: invalid budget value (${budget}).`);
    hasConfigError = true;
    continue;
  }

  const match = stats.find(s => s.route === route);
  if (!match) {
    console.error(
      `⚠️  ${route}: not found in ${STATS_PATH}. Was the route renamed or removed?`,
    );
    hasConfigError = true;
    results.push({ route, budget, status: 'missing' });
    continue;
  }

  let measuredGzipped = 0;
  for (const chunkPath of match.firstLoadChunkPaths) {
    const normalized = chunkPath.replace(/\\/g, '/');
    if (!existsSync(normalized)) {
      console.error(`⚠️  ${route}: chunk missing on disk: ${normalized}`);
      hasConfigError = true;
      continue;
    }
    const buf = readFileSync(normalized);
    measuredGzipped += gzipSync(buf, { level: 9 }).length;
  }

  const maxAllowed = Math.floor(budget * (1 + tolerance));
  const overage = measuredGzipped - budget;
  const ratio = overage / budget;
  const exceeds = measuredGzipped > maxAllowed;

  let icon;
  if (exceeds) {
    icon = '❌';
    hasFailure = true;
  } else if (overage > 0) {
    icon = '⚠️';
  } else {
    icon = '✅';
  }

  results.push({
    route,
    budget,
    measured: measuredGzipped,
    maxAllowed,
    overageBytes: overage,
    overageRatio: ratio,
    exceeds,
    chunkCount: match.firstLoadChunkPaths.length,
  });

  console.log(
    `${icon} ${route.padEnd(28)} ${fmtKb(measuredGzipped).padStart(11)} (budget ${fmtKb(budget)}, Δ ${fmtPct(ratio)}, max ${fmtKb(maxAllowed)})`,
  );
}

const summary = {
  generatedAt: new Date().toISOString(),
  tolerance,
  totalRoutes: results.length,
  failingRoutes: results.filter(r => r.exceeds).length,
  warningRoutes: results.filter(r => !r.exceeds && (r.overageBytes ?? 0) > 0).length,
  results,
};

mkdirSync(dirname(REPORT_PATH), { recursive: true });
writeFileSync(REPORT_PATH, JSON.stringify(summary, null, 2));
console.log(`\nReport written to ${REPORT_PATH}.`);

if (hasFailure) {
  console.error(
    `\n❌ Bundle budget violation: ${summary.failingRoutes} route(s) exceed budget × (1 + ${tolerance}). Update budgets in ${BUDGETS_PATH} or reduce bundle size.`,
  );
  process.exit(1);
}

if (hasConfigError) {
  console.error(`\n⚠️  Configuration error in ${BUDGETS_PATH} or build output.`);
  process.exit(2);
}

console.log(`\n✅ All ${results.length} routes within budget (tolerance ${tolerance * 100}%).`);
