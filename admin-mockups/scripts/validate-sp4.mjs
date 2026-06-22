#!/usr/bin/env node
/**
 * validate-sp4.mjs — Lightweight validator for SP4 mockup files.
 * Introduced by issue #1148 (Pre-Stage-3 hub mockups) to satisfy AC2 + AC3 + AC5.
 *
 * Usage:
 *   node admin-mockups/scripts/validate-sp4.mjs [file1.jsx file2.jsx ...]
 *   node admin-mockups/scripts/validate-sp4.mjs --all          # validate all sp4-*.jsx
 *   node admin-mockups/scripts/validate-sp4.mjs --self-test    # run inline regex assertions
 *
 * Exit codes:
 *   0 = all checks pass
 *   1 = one or more validation failures (details to stderr)
 *
 * Checks per .jsx file (scope: sp4-(hub-|dashboard) files only — legacy sp4-*
 * files predate the strict tokens-only policy and are intentionally exempt):
 *   - companion .html exists (skipped for *-parts.jsx partial fragments)
 *   - use-case header (AC5): file must contain `USE CASE: ... Primary actor:` block
 *     in JSDoc top comment. Regex is CRLF-tolerant.
 *   - hex token compliance (AC3): forbid literal `#RGB` / `#RRGGBB` colors in
 *     style/className strings. Allowed: CSS variables (`hsl(var(--c-*) / x)`,
 *     `var(--text)` etc.) and the neutral allowlist (`#fff`, `#000`, etc.).
 *     CI override via inline `// validator:allow-hex` comment.
 *
 * Scope clarification (issue #1154):
 *   - This validator ONLY checks `#`-notation hex literals. It does NOT check
 *     `rgb()` / `rgba()` calls. Shadow patterns like `rgba(0, 0, 0, 0.35)` are
 *     legitimate in the existing mockup set (drop-shadow on dark covers) and
 *     wholesale-blocking them would over-report. If `rgb()/rgba()` enforcement
 *     becomes required, add a companion RGBA_RE check with an allowlist for
 *     shadow patterns — see #1154 for rationale.
 *
 * Note: this is a smoke validator. It does NOT execute the JSX. Full execution
 * requires a browser (open the .html in a tab). CI gating is opt-in for follow-up.
 */

import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const DESIGN_FILES = join(__dirname, '..', 'design_files');

// ─── HEX color regex (forbidden literal hex outside comments) ──────
// Match #RRGGBB (6-digit) literals unconditionally. For #RGB (3-digit), require
// at least one a-f letter — this avoids false-positives on PR/issue references
// like `#309` or `#1149` (all-digit, common in mockup comments and JSX text).
//
// tested (kept in sync with --self-test below):
//   match    → #abc, #a1b, #1a2, #12a, #fff, #fff000, #abcdef, #f5b800
//   skip     → #309, #1149, #1234, #542, (PR #309)
const HEX_RE = /(?<![A-Za-z0-9])#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{3}(?<=[a-fA-F][0-9a-fA-F]{0,2}|[0-9a-fA-F][a-fA-F][0-9a-fA-F]?|[0-9a-fA-F]{0,2}[a-fA-F]))\b/g;
// allow specific "neutral" colors used inside on-cover overlays where the
// design intent is opaque white/black (not entity-tinted). Allowlist intentional:
const HEX_ALLOWLIST = new Set([
  '#fff', '#ffffff', // overlay text on dark covers
  '#1a1a1a', '#0a0a0a', // hard black for phone frame inside components.css
  '#000', '#000000',    // pure black for shadow rings
]);

// ─── USE CASE header check (CRLF-tolerant) ─────────────────────────
// tested (kept in sync with --self-test below):
//   match LF   → "USE CASE:\n  Primary actor: foo"
//   match CRLF → "USE CASE:\r\n  Primary actor: foo"
//   skip       → "USE CASE: foo" (no newline before Primary actor)
//   skip       → "Primary actor: foo" (no USE CASE header)
const USE_CASE_RE = /USE CASE:\s*[\r\n]+\s*\*?\s*Primary actor:/i;

// ─── Filename ──────────────────────────────────────────────────────
function companionHtmlFor(jsxPath) {
  return jsxPath.replace(/\.jsx$/, '.html');
}

// ─── Validator ─────────────────────────────────────────────────────
function validateFile(jsxPath) {
  const errors = [];
  const warnings = [];

  if (!existsSync(jsxPath)) {
    errors.push(`File not found: ${jsxPath}`);
    return { errors, warnings };
  }
  const src = readFileSync(jsxPath, 'utf8');

  // Check 1: companion .html (skipped for `*-parts.jsx` partial fragments)
  const isPartial = /-parts\.jsx$/i.test(jsxPath);
  if (!isPartial) {
    const htmlPath = companionHtmlFor(jsxPath);
    if (!existsSync(htmlPath)) {
      errors.push(`Missing companion HTML: ${basename(htmlPath)}`);
    }
  }

  // Check 2: use-case header (only for hub-* and dashboard files — pattern enforcement is partial,
  // legacy sp4-* may not have the expanded format yet).
  const enforceUseCase = /sp4-(hub-|dashboard)/i.test(basename(jsxPath));
  if (enforceUseCase) {
    if (!USE_CASE_RE.test(src)) {
      errors.push(`Missing or malformed USE CASE block (expected "USE CASE:\\n  Primary actor:" pattern, LF or CRLF)`);
    }
  }

  // Check 3: hex color compliance (AC3) — scoped to files introduced
  // by Pre-Stage-3 work (hub-* + dashboard). Legacy sp4-* files predate the
  // strict tokens-only policy and are out of scope for this validator.
  // Skip lines containing `// validator:allow-hex` for explicit overrides.
  const enforceHex = /sp4-(hub-|dashboard)/i.test(basename(jsxPath));
  if (enforceHex) {
    // strip block comments first (multi-line)
    const codeNoBlockComments = src.replace(/\/\*[\s\S]*?\*\//g, '');
    const lines = codeNoBlockComments.split('\n');
    lines.forEach((line, idx) => {
      if (line.includes('validator:allow-hex')) return;
      // strip line comments to reduce false positives
      const codeOnly = line.replace(/\/\/.*$/, '');
      const matches = codeOnly.match(HEX_RE);
      if (!matches) return;
      matches.forEach(hex => {
        const lower = hex.toLowerCase();
        if (HEX_ALLOWLIST.has(lower)) return;
        errors.push(`Line ${idx + 1} (post comment-strip): hardcoded hex color ${hex} — use CSS variables instead`);
      });
    });
  }

  // Check 4: react import / ReactDOM render present (parse-lite)
  if (!/ReactDOM\.createRoot\(/.test(src)) {
    warnings.push(`No ReactDOM.createRoot() call found — mockup may not render`);
  }
  if (!/window\.DS/.test(src)) {
    warnings.push(`window.DS not referenced — mockup may not use shared dataset`);
  }

  return { errors, warnings };
}

// ─── Self-test (inline regex assertions) ───────────────────────────
// Run via `--self-test` flag. Validates that HEX_RE and USE_CASE_RE behave per
// their documented contract above. Any regression in the regex (e.g., a future
// "simplification" that breaks PR-ref detection) trips an exit-1 failure.
function runSelfTest() {
  const cases = [
    // [regex, input, shouldMatch, description]
    [HEX_RE, '#abc',            true,  'HEX_RE: 3-digit with letter (lowercase)'],
    [HEX_RE, '#a1b',            true,  'HEX_RE: 3-digit letter in middle'],
    [HEX_RE, '#1a2',            true,  'HEX_RE: 3-digit letter in middle'],
    [HEX_RE, '#12a',            true,  'HEX_RE: 3-digit letter at end'],
    [HEX_RE, '#fff',            true,  'HEX_RE: 3-digit all-letter (will be allowlisted)'],
    [HEX_RE, '#FFFFFF',         true,  'HEX_RE: 6-digit uppercase'],
    [HEX_RE, '#abcdef',         true,  'HEX_RE: 6-digit all-letter'],
    [HEX_RE, '#f5b800',         true,  'HEX_RE: 6-digit mixed (typical brand color)'],
    [HEX_RE, '#309',            false, 'HEX_RE: 3-digit all-digit = PR ref, must skip'],
    [HEX_RE, '#1149',           false, 'HEX_RE: 4-digit all-digit = issue ref, must skip'],
    [HEX_RE, '#1234',           false, 'HEX_RE: 4-digit (not valid hex length), must skip'],
    [HEX_RE, '#542',            false, 'HEX_RE: 3-digit all-digit = PR ref, must skip'],
    [HEX_RE, 'see (PR #309)',   false, 'HEX_RE: PR ref in prose context'],
    [HEX_RE, 'closes #1148',    false, 'HEX_RE: issue ref in prose'],
    [USE_CASE_RE, 'USE CASE:\n  Primary actor: visitor',   true,  'USE_CASE_RE: LF newline'],
    [USE_CASE_RE, 'USE CASE:\r\n  Primary actor: visitor', true,  'USE_CASE_RE: CRLF newline (Windows compat)'],
    [USE_CASE_RE, 'USE CASE:\n   * Primary actor: visitor', true, 'USE_CASE_RE: JSDoc continuation with leading *'],
    [USE_CASE_RE, 'USE CASE: visitor opens page',          false, 'USE_CASE_RE: no newline before "Primary actor"'],
    [USE_CASE_RE, 'Primary actor: visitor',                false, 'USE_CASE_RE: no USE CASE header'],
  ];

  let failed = 0;
  console.log('\nvalidate-sp4 --self-test: checking inline regex contract...\n');
  for (const [re, input, expectMatch, desc] of cases) {
    // reset global regex state before each test (RegExp with /g flag has lastIndex side-effect)
    re.lastIndex = 0;
    const matched = re.test(input);
    re.lastIndex = 0;
    const ok = matched === expectMatch;
    if (ok) {
      console.log(`  ✓ ${desc}`);
    } else {
      failed++;
      console.error(`  ✗ ${desc}`);
      console.error(`      input: ${JSON.stringify(input)}`);
      console.error(`      expected match=${expectMatch}, got match=${matched}`);
    }
  }
  console.log(`\n${cases.length - failed}/${cases.length} self-test cases passed.`);
  process.exit(failed > 0 ? 1 : 0);
}

// ─── Main ─────────────────────────────────────────────────────────
const args = process.argv.slice(2);

if (args.includes('--self-test')) {
  runSelfTest();
}

let targets = [];

if (args.length === 0 || args.includes('--all')) {
  const all = readdirSync(DESIGN_FILES).filter(f => /^sp4-.*\.jsx$/i.test(f));
  targets = all.map(f => join(DESIGN_FILES, f));
} else {
  targets = args.filter(a => !a.startsWith('--')).map(a =>
    a.includes('/') || a.includes('\\') ? a : join(DESIGN_FILES, a)
  );
}

if (targets.length === 0) {
  console.error('No targets to validate. Pass file paths or --all.');
  process.exit(1);
}

let failed = 0;
let totalWarnings = 0;
console.log(`\nvalidate-sp4: checking ${targets.length} file(s)...\n`);

for (const target of targets) {
  const { errors, warnings } = validateFile(target);
  const name = basename(target);
  if (errors.length === 0 && warnings.length === 0) {
    console.log(`  ✓ ${name}`);
  } else if (errors.length === 0) {
    console.log(`  ⚠ ${name}`);
    for (const w of warnings) console.log(`      WARN: ${w}`);
    totalWarnings += warnings.length;
  } else {
    failed++;
    console.error(`  ✗ ${name}`);
    for (const e of errors) console.error(`      ERROR: ${e}`);
    for (const w of warnings) console.error(`      WARN:  ${w}`);
    totalWarnings += warnings.length;
  }
}

console.log(`\n${targets.length - failed}/${targets.length} passed, ${failed} failed, ${totalWarnings} warning(s).`);
process.exit(failed > 0 ? 1 : 0);
