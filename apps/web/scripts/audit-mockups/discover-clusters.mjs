#!/usr/bin/env node
/**
 * discover-clusters.mjs — partition mockup files into 6 clusters (DS-17 Phase B).
 *
 * Walks admin-mockups/design_files/ and emits a deterministic manifest.json
 * with files grouped into:
 *   - dev-fixtures: design system playground, datasets, tokens
 *   - auth: login/register/onboarding/notifications/settings
 *   - sp3: public hub, join, library, KB
 *   - sp4-core: dashboard, players, sessions, game-night, library, game-detail
 *   - sp4-sessions: live, toolkit, scores, recap, gamebook
 *   - sp6-7-nano: admin, RAG, observability, generators (also: fallback)
 *
 * Manifest is the deterministic input contract — same files in, same clusters out.
 * Master orchestrator consumes manifest in order, dispatching one Agent per cluster.
 *
 * Usage:
 *   node discover-clusters.mjs --out audits/2026-06-10-mockup-design-intent-manifest.json
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 *   Sub-issue: #2127
 */

import { writeFileSync } from 'node:fs';
import { dirname, resolve, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');
const DESIGN_FILES_DIR = resolve(REPO_ROOT, 'admin-mockups', 'design_files');

/** @typedef {'dev-fixtures' | 'auth' | 'sp3' | 'sp4-core' | 'sp4-sessions' | 'sp6-7-nano'} ClusterId */

const DEV_FIXTURE_NAMES = new Set([
  '00-hub.html',
  '01-screens.html',
  '02-desktop-patterns.html',
  '03-drawer-variants.html',
  '04-design-system.html',
  '05-dark-mode.html',
  'state-matrix.html',
  'components.css',
  'data.js',
  'mobile-app.jsx',
  'tokens.css',
  'sp4-play-records-data.js',
  'scaffold.css',
]);

const AUTH_PREFIXES = [
  'auth-',
  'onboarding',
  'notifications',
  'public',
  'settings',
  'verify-',
  'reset-',
  'sp5-',
];
const SP3_PREFIXES = ['sp3-', 'hub-', 'library-public', 'join-'];
const SP4_SESSIONS_KEYWORDS = ['live', 'toolkit', 'scores', 'recap', 'gamebook'];
const SP4_CORE_KEYWORDS = [
  'dashboard',
  'player',
  'session',
  'game-night',
  'library',
  'game-detail',
];
const SP6_7_NANO_PREFIXES = [
  'sp6-',
  'sp7-',
  'admin-',
  'nano-',
  'rag-',
  'observability',
  'generator',
];

// Code-reviewer Finding 3: explicit prefix coverage for unrecognized file families.
// librogame-*: game-specific session storyboards/runthroughs → sp4-sessions
// chat-*: chat UI mockups → sp4-sessions
// nanolith-nav-*: navigation infrastructure → dev-fixtures
// pr-form-*: PR/form-core mockups → sp4-core
// index.html standalone → dev-fixtures
const LIBROGAME_PREFIXES = ['librogame-', 'chat-'];
const NANOLITH_DEV_FIXTURE_PREFIXES = ['nanolith-'];
const SP4_CORE_FALLBACK_PREFIXES = ['pr-form-'];
const DEV_FIXTURE_STANDALONE = new Set(['index.html']);

/**
 * @param {string} filename
 * @param {(msg: string) => void} [onWarn]
 * @returns {ClusterId}
 */
export function classifyFile(filename, onWarn) {
  if (DEV_FIXTURE_NAMES.has(filename)) return 'dev-fixtures';
  if (DEV_FIXTURE_STANDALONE.has(filename)) return 'dev-fixtures';
  if (NANOLITH_DEV_FIXTURE_PREFIXES.some((p) => filename.startsWith(p))) return 'dev-fixtures';

  // Any sp4-* filename: sessions keywords → sp4-sessions; everything else → sp4-core
  // (sp4 = scenario plan 4 = main app surface; default to core when not a session view)
  if (filename.startsWith('sp4-')) {
    if (SP4_SESSIONS_KEYWORDS.some((k) => filename.includes(k))) return 'sp4-sessions';
    return 'sp4-core';
  }

  // Explicit routing for unrecognized families (Code-reviewer Finding 3)
  if (LIBROGAME_PREFIXES.some((p) => filename.startsWith(p))) return 'sp4-sessions';
  if (SP4_CORE_FALLBACK_PREFIXES.some((p) => filename.startsWith(p))) return 'sp4-core';

  if (AUTH_PREFIXES.some((p) => filename.startsWith(p))) return 'auth';
  if (SP3_PREFIXES.some((p) => filename.startsWith(p))) return 'sp3';
  if (SP6_7_NANO_PREFIXES.some((p) => filename.startsWith(p))) return 'sp6-7-nano';

  onWarn?.(`Unknown filename "${filename}" — falling back to sp6-7-nano`);
  return 'sp6-7-nano';
}

/**
 * @param {Array<{path: string, type: 'html' | 'jsx'}>} files
 * @returns {Array<{path: string, type: 'html' | 'jsx', pairKey?: string}>}
 */
export function groupByPair(files) {
  const stems = new Map();
  for (const file of files) {
    const stem = basename(file.path, extname(file.path));
    stems.set(stem, (stems.get(stem) ?? 0) + 1);
  }
  return files.map((file) => {
    const stem = basename(file.path, extname(file.path));
    if (stems.get(stem) > 1) {
      return { ...file, pairKey: stem };
    }
    return file;
  });
}

function main() {
  const argv = process.argv.slice(2);
  const outIdx = argv.indexOf('--out');
  if (outIdx === -1) {
    console.error('Usage: discover-clusters.mjs --out <path>');
    process.exit(2);
  }
  const outPath = argv[outIdx + 1];

  const htmlFiles = globSync('*.html', { cwd: DESIGN_FILES_DIR });
  const jsxFiles = globSync('*.jsx', { cwd: DESIGN_FILES_DIR });
  const cssFiles = globSync('*.css', { cwd: DESIGN_FILES_DIR });
  const jsFiles = globSync('*.js', { cwd: DESIGN_FILES_DIR });

  /** @type {Array<{path: string, type: 'html' | 'jsx'}>} */
  const allFiles = [
    ...htmlFiles.map((f) => ({
      path: `admin-mockups/design_files/${f}`,
      type: /** @type {const} */ ('html'),
    })),
    ...jsxFiles.map((f) => ({
      path: `admin-mockups/design_files/${f}`,
      type: /** @type {const} */ ('jsx'),
    })),
    ...cssFiles.map((f) => ({
      path: `admin-mockups/design_files/${f}`,
      type: /** @type {const} */ ('html'),
    })),
    ...jsFiles.map((f) => ({
      path: `admin-mockups/design_files/${f}`,
      type: /** @type {const} */ ('html'),
    })),
  ];

  const paired = groupByPair(allFiles);

  /** @type {Map<ClusterId, Array<{path: string, type: string, pairKey?: string}>>} */
  const clusters = new Map([
    ['dev-fixtures', []],
    ['auth', []],
    ['sp3', []],
    ['sp4-core', []],
    ['sp4-sessions', []],
    ['sp6-7-nano', []],
  ]);

  const warnings = [];
  for (const file of paired) {
    const clusterId = classifyFile(basename(file.path), (msg) => warnings.push(msg));
    clusters.get(clusterId).push(file);
  }

  const orderedClusters = ['dev-fixtures', 'auth', 'sp3', 'sp4-core', 'sp4-sessions', 'sp6-7-nano'];

  const manifest = {
    generatedAt: new Date().toISOString().split('T')[0],
    totalFiles: paired.length,
    warnings,
    clusters: orderedClusters.map((clusterId, idx) => ({
      clusterId,
      files: clusters.get(clusterId),
      dependencies: orderedClusters.slice(0, idx),
    })),
  };

  const absoluteOut = resolve(REPO_ROOT, outPath);
  writeFileSync(absoluteOut, JSON.stringify(manifest, null, 2) + '\n');
  console.log(`Manifest written: ${outPath} (${paired.length} files in 6 clusters)`);
  for (const c of manifest.clusters) {
    console.log(`  - ${c.clusterId}: ${c.files.length} files`);
  }
  if (warnings.length) {
    console.warn(`Warnings (${warnings.length}):`);
    for (const w of warnings) console.warn(`  ${w}`);
  }
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
