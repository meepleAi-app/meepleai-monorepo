#!/usr/bin/env node
/**
 * lint-tokens.mjs — token vocabulary violation reporter (DS-2)
 *
 * Runs ESLint with the project's flat config and extracts every violation
 * of `local/no-hardcoded-color-utility`. Output:
 *   - JSON: audits/2026-05-12-token-violations.json
 *   - Markdown summary: audits/2026-05-12-token-violations.md (cluster-aggregated)
 *
 * Usage:
 *   pnpm lint:tokens                         # full inventory
 *   pnpm lint:tokens -- --max-warnings 0     # CI gate (DS-12 only)
 *
 * Refs:
 *   Spec: docs/for-developers/specs/2026-05-12-token-canonicalization.md
 */

import { writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ESLint } from 'eslint';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');
const WEB_ROOT = resolve(__dirname, '..');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-05-12-token-violations.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-05-12-token-violations.md');

const TARGET_RULE = 'local/no-hardcoded-color-utility';

const args = process.argv.slice(2);
const maxWarnings = Number.parseInt(
  args.find(a => a.startsWith('--max-warnings'))?.split('=')[1] ?? '-1',
  10
);

function clusterFor(filePath) {
  const rel = relative(WEB_ROOT, filePath).replace(/\\/g, '/');
  // Heuristics: first 3 segments under src/ define the cluster.
  // e.g. src/components/features/sessions/SessionsHero.tsx -> features/sessions
  const m = rel.match(/^src\/(.+)$/);
  if (!m) return 'unknown';
  const parts = m[1].split('/');
  if (parts[0] === 'app') {
    // App router route group => use up to the route segment
    return `app/${parts.slice(1, 3).join('/')}`;
  }
  if (parts[0] === 'components') {
    return parts.slice(1, 3).join('/');
  }
  return parts.slice(0, 2).join('/');
}

async function main() {
  const eslint = new ESLint({ cwd: WEB_ROOT });
  const results = await eslint.lintFiles(['src/**/*.{ts,tsx}']);

  let total = 0;
  const violations = [];
  for (const r of results) {
    for (const msg of r.messages) {
      if (msg.ruleId !== TARGET_RULE) continue;
      total += 1;
      violations.push({
        file: relative(WEB_ROOT, r.filePath).replace(/\\/g, '/'),
        cluster: clusterFor(r.filePath),
        line: msg.line,
        column: msg.column,
        message: msg.message,
        severity: msg.severity,
      });
    }
  }

  const byCluster = violations.reduce((acc, v) => {
    acc[v.cluster] = (acc[v.cluster] ?? 0) + 1;
    return acc;
  }, {});
  const clusterOrder = Object.entries(byCluster).sort((a, b) => b[1] - a[1]);

  if (!existsSync(AUDIT_DIR)) mkdirSync(AUDIT_DIR, { recursive: true });

  const jsonReport = {
    generatedAt: new Date().toISOString(),
    rule: TARGET_RULE,
    totalViolations: total,
    fileCount: new Set(violations.map(v => v.file)).size,
    clusterCount: Object.keys(byCluster).length,
    byCluster,
    violations,
  };
  writeFileSync(JSON_OUT, JSON.stringify(jsonReport, null, 2));

  const mdLines = [
    '# Token Vocabulary Violations — Inventory',
    '',
    `| Field | Value |`,
    `|---|---|`,
    `| **Date** | ${new Date().toISOString().slice(0, 10)} |`,
    `| **Generator** | \`pnpm lint:tokens\` (DS-2) |`,
    `| **Spec** | [\`2026-05-12-token-canonicalization.md\`](../docs/for-developers/specs/2026-05-12-token-canonicalization.md) |`,
    `| **Rule** | \`${TARGET_RULE}\` |`,
    `| **Total violations** | ${total} |`,
    `| **Files affected** | ${jsonReport.fileCount} |`,
    `| **Clusters affected** | ${jsonReport.clusterCount} |`,
    '',
    '## Violations by cluster',
    '',
    '| Cluster | Violations | Suggested stage |',
    '|---|---|---|',
  ];

  // Stage assignment heuristic (matches the spec §3 DS-4..DS-11 order)
  const stageFor = c => {
    if (c.startsWith('app/(authenticated)/dashboard')) return 'DS-4';
    if (c.startsWith('features/sessions')) return 'DS-5';
    if (c.startsWith('features/session-live') || c.startsWith('features/session-summary')) return 'DS-6';
    if (c.startsWith('features/games') || c.startsWith('features/game-detail')) return 'DS-7';
    if (c.startsWith('features/agents') || c.startsWith('features/agent-detail')) return 'DS-8';
    if (c.startsWith('features/players') || c.startsWith('features/player-detail')) return 'DS-9';
    if (c.startsWith('features/gamebook') || c.startsWith('features/game-chat')) return 'DS-10';
    if (c.startsWith('features/library') || c.startsWith('app/(authenticated)/library')) return 'DS-11';
    return 'manual';
  };

  for (const [cluster, count] of clusterOrder) {
    mdLines.push(`| \`${cluster}\` | ${count} | ${stageFor(cluster)} |`);
  }

  mdLines.push(
    '',
    '## Top 20 files',
    '',
    '| File | Violations |',
    '|---|---|'
  );
  const byFile = violations.reduce((acc, v) => {
    acc[v.file] = (acc[v.file] ?? 0) + 1;
    return acc;
  }, {});
  const topFiles = Object.entries(byFile)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 20);
  for (const [file, count] of topFiles) {
    mdLines.push(`| \`${file}\` | ${count} |`);
  }

  mdLines.push(
    '',
    '## Notes',
    '',
    '- Rule is in `warn` mode during DS-3 inventory + DS-4..DS-11 cluster migrations.',
    '- Switched to `error` in DS-12 once `pnpm lint:tokens --max-warnings 0` is green.',
    '- Companion JSON: [`2026-05-12-token-violations.json`](./2026-05-12-token-violations.json).',
    ''
  );
  writeFileSync(MD_OUT, mdLines.join('\n'));

  // Pretty terminal output
  process.stdout.write(
    `\n[lint:tokens] ${total} violations across ${jsonReport.fileCount} files in ${jsonReport.clusterCount} clusters.\n` +
      `  JSON: ${relative(REPO_ROOT, JSON_OUT)}\n` +
      `  MD:   ${relative(REPO_ROOT, MD_OUT)}\n\n`
  );

  if (maxWarnings >= 0 && total > maxWarnings) {
    process.stderr.write(
      `[lint:tokens] FAIL: ${total} violations exceed --max-warnings ${maxWarnings}\n`
    );
    process.exit(1);
  }
}

main().catch(err => {
  process.stderr.write(`[lint:tokens] ERROR: ${err.stack || err.message}\n`);
  process.exit(2);
});
