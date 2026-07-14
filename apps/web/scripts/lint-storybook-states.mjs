/**
 * lint-storybook-states.mjs — canonical-state coverage gate (DEC-A5, umbrella #2342)
 *
 * Walks MOCKUPS_INDEX page-mock entries → fidelity.json (by mockup.source) →
 * story_path → states implemented, and classifies each entry:
 *   - covered            : story implements every declared canonical state
 *   - coverage-gap       : no fidelity, or fidelity without story_path (whitelist-incremental)
 *   - contract-violation : story omits a declared canonical state (always blocking)
 *   - skipped-obsolete   : fidelity design_intent === 'forward-refactor-obsolete'
 *
 * Modes: inventory (default, exit 0) | strict (--strict --max-baseline N).
 * Refs: docs/superpowers/specs/2026-07-14-lint-storybook-states-design.md
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';
import { parseMockupsIndex } from './mockup-annotations/inject-annotations.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');
const MOCKUPS_INDEX = resolve(REPO_ROOT, 'admin-mockups', 'MOCKUPS_INDEX.md');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-07-14-storybook-states-coverage.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-07-14-storybook-states-coverage.md');

export const CANONICAL_STATES = Object.freeze(['default', 'empty', 'loading', 'error', 'sse']);

/** Canonicalize one raw state token. empty-* → empty. Non-canonical → null. */
export function normalizeState(raw) {
  if (typeof raw !== 'string') return null;
  const s = raw.trim().toLowerCase();
  if (s === 'default' || s === 'loading' || s === 'error' || s === 'sse') return s;
  if (s === 'empty' || s.startsWith('empty-')) return 'empty';
  return null;
}

const OVERRIDE_RE = /canonicalStates\s*:\s*\[([^\]]*)\]/;
const STATE_LITERAL_RE = /['"`](default|empty[\w-]*|loading|error|sse|offline|quota-(?:soft|hard))['"`]/g;

/** Hybrid state detection: explicit override wins, else heuristic scan. */
export function detectStates(storySource) {
  const set = new Set();
  const override = OVERRIDE_RE.exec(storySource);
  if (override) {
    const quoted = override[1].match(/['"`]([^'"`]+)['"`]/g) || [];
    for (const q of quoted) {
      const norm = normalizeState(q.slice(1, -1));
      if (norm) set.add(norm);
    }
    return set;
  }
  STATE_LITERAL_RE.lastIndex = 0;
  let m;
  while ((m = STATE_LITERAL_RE.exec(storySource)) !== null) {
    const norm = normalizeState(m[1]);
    if (norm) set.add(norm);
  }
  return set;
}

/** Build Map<mockupSource, {fidelityPath, fidelity}>. Malformed JSON skipped. */
export function buildFidelityIndex(fidelityRelPaths, readFile) {
  const bySource = new Map();
  for (const rel of fidelityRelPaths) {
    let obj;
    try {
      obj = JSON.parse(readFile(rel));
    } catch {
      continue;
    }
    const source = obj && obj.mockup && obj.mockup.source;
    if (typeof source === 'string' && source.length > 0) {
      bySource.set(source, { fidelityPath: rel, fidelity: obj });
    }
  }
  return bySource;
}

/** Classify one page-mock entry against its fidelity + story. */
export function classifyMockupEntry(entry, fidelityIndex, io) {
  const mockupSource = `admin-mockups/design_files/${entry.mockup}`;
  const base = { mockup: entry.mockup, routes: entry.routes, mockupSource };

  const hit = fidelityIndex.get(mockupSource);
  if (!hit) return { ...base, verdict: 'coverage-gap', reason: 'no-fidelity' };

  const acceptance = hit.fidelity.acceptance || {};
  if (acceptance.design_intent === 'forward-refactor-obsolete') {
    return { ...base, verdict: 'skipped-obsolete' };
  }

  const storyPath = acceptance.story_path;
  if (!storyPath) return { ...base, verdict: 'coverage-gap', reason: 'no-story-path' };
  if (!io.exists(storyPath)) return { ...base, verdict: 'coverage-gap', reason: 'story-missing' };

  const declared = [
    ...new Set((acceptance.states_covered || []).map(normalizeState).filter(Boolean)),
  ].filter((s) => CANONICAL_STATES.includes(s));

  const detected = detectStates(io.readFile(storyPath));
  const missing = declared.filter((s) => !detected.has(s));

  if (missing.length > 0) {
    return {
      ...base,
      verdict: 'contract-violation',
      storyPath,
      declared,
      detected: [...detected],
      missing,
    };
  }
  return { ...base, verdict: 'covered', storyPath, declared, detected: [...detected] };
}
