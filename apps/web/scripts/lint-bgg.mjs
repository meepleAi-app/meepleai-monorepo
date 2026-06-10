#!/usr/bin/env node
/**
 * lint-bgg.mjs — issue #2123 BGG ToS compliance grep gate.
 *
 * Defense-in-depth complement to the `local/no-bgg-host` ESLint rule. ESLint
 * only sees the FE source tree; this script ALSO covers:
 *
 *   - apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/*.yml
 *   - apps/web/next.config.js
 *   - apps/web/src/** (re-check, in case ESLint misconfig hides it)
 *   - apps/api/src/Api/Infrastructure/Seeders/**.cs
 *
 * Allowlisted occurrences (not flagged): the BGG host blocklist in
 * apps/web/src/lib/games/cover-utils.ts is a data definition, not a URL to
 * render — handled with inline `// eslint-disable-next-line local/no-bgg-host`
 * comments in that file. The grep script approximates the same allowance by
 * skipping the cover-utils file path entirely.
 *
 * Usage:
 *   pnpm lint:bgg            # exit 0 if clean, exit 1 on any violation
 *   pnpm lint:bgg --verbose  # print each violation file:line:match
 *
 * Refs:
 *   Issue : https://github.com/meepleAi-app/meepleai-monorepo/issues/2123
 *   Spec  : docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md
 *   ADR   : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = resolve(__dirname, '..');
const REPO_ROOT = resolve(WEB_ROOT, '..', '..');

const BGG_PATTERN = /(cf\.geekdo-images\.com|geekdo-images\.com|images\.geekdo\.com|boardgamegeek\.com)/i;

const args = new Set(process.argv.slice(2));
const VERBOSE = args.has('--verbose') || args.has('-v');

/** @type {Array<{label: string; root: string; include: RegExp; excludePaths?: RegExp[]}>} */
const SCAN_TARGETS = [
  {
    label: 'API catalog seed manifests (apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests)',
    root: resolve(REPO_ROOT, 'apps', 'api', 'src', 'Api', 'Infrastructure', 'Seeders', 'Catalog', 'Manifests'),
    include: /\.ya?ml$/i,
  },
  {
    label: 'Web Next.js config (apps/web/next.config.js)',
    root: resolve(WEB_ROOT, 'next.config.js'),
    include: /next\.config\.js$/i,
    // The file documents the BGG ban in inline comments — the grep allowlist
    // skips lines starting with comment markers (// or *), see scanTarget().
  },
  {
    label: 'Web FE source (apps/web/src)',
    root: resolve(WEB_ROOT, 'src'),
    include: /\.(ts|tsx|js|jsx)$/i,
    excludePaths: [
      // Allowlisted: the BGG host blocklist itself (data, not URLs to render).
      /[\\/]src[\\/]lib[\\/]games[\\/]cover-utils\.ts$/i,
      // Admin server-to-server BGG paths — legitimate per ADR-059 §2.
      /[\\/]src[\\/]app[\\/]admin[\\/]/i,
      /[\\/]src[\\/]components[\\/]admin[\\/]/i,
      // Storybook fixtures — tracked in F2 follow-up, not runtime.
      /\.stories\.(ts|tsx|js|jsx)$/i,
      /\.story\.(ts|tsx|js|jsx)$/i,
      // The BGG types file documents legitimate BGG API DTOs.
      /[\\/]src[\\/]types[\\/]bgg\.ts$/i,
      // The BGG API client legitimately fetches from BGG server-side.
      /[\\/]src[\\/]lib[\\/]api[\\/]clients[\\/]bggClient\.ts$/i,
      // Test files reference BGG hosts to assert the blocking guards behave
      // correctly — verifying the rule itself.
      /[\\/]__tests__[\\/]/i,
      /\.test\.(ts|tsx|js|jsx)$/i,
      /\.spec\.(ts|tsx|js|jsx)$/i,
    ],
  },
  {
    label: 'API seeders source (apps/api/src/Api/Infrastructure/Seeders)',
    root: resolve(REPO_ROOT, 'apps', 'api', 'src', 'Api', 'Infrastructure', 'Seeders'),
    include: /\.cs$/i,
    excludePaths: [
      // Empty list — Phase A migration already nullified the legacy
      // BggCoverDownloader path from the seeder; if any future legitimate
      // server-side fetch needs to land here, it gets a comment + override.
    ],
  },
];

function walk(root) {
  const stats = statSync(root, { throwIfNoEntry: false });
  if (!stats) return [];
  if (stats.isFile()) return [root];
  const out = [];
  const entries = readdirSync(root, { withFileTypes: true });
  for (const entry of entries) {
    const full = resolve(root, entry.name);
    if (entry.isDirectory()) {
      out.push(...walk(full));
    } else if (entry.isFile()) {
      out.push(full);
    }
  }
  return out;
}

function scanTarget(target) {
  const files = walk(target.root).filter(f => target.include.test(f));
  const filtered = target.excludePaths
    ? files.filter(f => !target.excludePaths.some(rx => rx.test(f)))
    : files;
  /** @type {Array<{file: string; line: number; match: string}>} */
  const hits = [];
  for (const file of filtered) {
    let text;
    try {
      text = readFileSync(file, 'utf8');
    } catch (e) {
      console.error(`lint:bgg: cannot read ${file}: ${e.message}`);
      continue;
    }
    const lines = text.split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      // Skip lines whose first non-whitespace content is a comment marker:
      //   //   single-line JS/TS/CS comment
      //   *    JSDoc block-comment continuation or YAML/MD bullet-comment
      //   #    YAML / Python / shell comment
      const stripped = line.trimStart();
      if (stripped.startsWith('//') || stripped.startsWith('*') || stripped.startsWith('#')) continue;
      const m = line.match(BGG_PATTERN);
      if (m) {
        hits.push({ file, line: i + 1, match: m[0] });
      }
    }
  }
  return { label: target.label, count: hits.length, hits };
}

let totalFailures = 0;
for (const target of SCAN_TARGETS) {
  const result = scanTarget(target);
  if (result.count === 0) {
    console.log(`✅ ${result.label}: clean`);
  } else {
    totalFailures += result.count;
    console.log(`❌ ${result.label}: ${result.count} BGG host match${result.count === 1 ? '' : 'es'}`);
    if (VERBOSE) {
      for (const hit of result.hits) {
        const rel = relative(REPO_ROOT, hit.file).replace(/\\/g, '/');
        console.log(`     ${rel}:${hit.line}: ${hit.match}`);
      }
    } else {
      const unique = Array.from(new Set(result.hits.map(h => relative(REPO_ROOT, h.file).replace(/\\/g, '/'))));
      const head = unique.slice(0, 5);
      for (const f of head) console.log(`     ${f}`);
      if (unique.length > 5) console.log(`     … and ${unique.length - 5} more file(s); run with --verbose for full list`);
    }
  }
}

if (totalFailures === 0) {
  console.log('\n✅ BGG ToS compliance gate clean.');
  process.exit(0);
}
console.error(`\n❌ BGG ToS compliance gate failed: ${totalFailures} violation(s).`);
console.error('   See docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md');
process.exit(1);
