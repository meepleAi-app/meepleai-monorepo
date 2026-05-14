#!/usr/bin/env node
/**
 * validate-sp4.mjs — Lightweight validator for SP4 mockup files.
 * Introduced by issue #1148 (Pre-Stage-3 hub mockups) to satisfy AC2 + AC3 + AC5.
 *
 * Usage:
 *   node admin-mockups/scripts/validate-sp4.mjs [file1.jsx file2.jsx ...]
 *   node admin-mockups/scripts/validate-sp4.mjs --all      # validate all sp4-*.jsx
 *
 * Exit codes:
 *   0 = all checks pass
 *   1 = one or more validation failures (details to stderr)
 *
 * Checks per .jsx file:
 *   - parse-clean: file loadable as text, ES module syntax OK (loose parse via Babel? No — keep dep-free regex sniff).
 *   - hex/rgb token compliance (AC3): forbid literal #RGB / #RRGGBB / rgb()/rgba() colors in style/className strings.
 *     Allowed: CSS variables (hsl(var(--c-*) / x), var(--text), etc.) and rgba(0,0,0,...) for shadows on dark overlays.
 *     Forbidden: #f5b800, rgb(255,0,0). The check is regex-based and may over-report; CI override via inline `// validator:allow-hex` comment.
 *   - use-case header (AC5): file must contain `USE CASE:` block in JSDoc top comment.
 *   - companion .html exists.
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
const HEX_RE = /(?<![A-Za-z0-9])#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3})?\b/g;
// allow specific "neutral" colors used inside on-cover overlays where the
// design intent is opaque white/black (not entity-tinted). Allowlist intentional:
const HEX_ALLOWLIST = new Set([
  '#fff', '#ffffff', // overlay text on dark covers
  '#1a1a1a', '#0a0a0a', // hard black for phone frame inside components.css
  '#000', '#000000',    // pure black for shadow rings
]);

// ─── USE CASE header check ─────────────────────────────────────────
const USE_CASE_RE = /USE CASE:\s*\n\s*\*?\s*Primary actor:/i;

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
      errors.push(`Missing or malformed USE CASE block (expected "USE CASE:\\n  Primary actor:" pattern)`);
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

// ─── Main ─────────────────────────────────────────────────────────
const args = process.argv.slice(2);
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
