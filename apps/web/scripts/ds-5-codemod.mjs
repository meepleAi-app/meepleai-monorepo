#!/usr/bin/env node
/**
 * DS-5 codemod — bulk migration of session/* files to canonical tokens.
 *
 * Applies the cookbook mappings from DS-4 across all 35 files in the cluster.
 * Pattern matches use word boundaries to avoid accidental substring matches.
 *
 * Run once: `node scripts/ds-5-codemod.mjs`
 * Idempotent — re-running has no effect on already-migrated files.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');

// Identify DS-5 files via the audit JSON
const audit = JSON.parse(
  readFileSync(resolve(__dirname, '..', '..', '..', 'audits', '2026-05-12-token-violations.json'), 'utf8')
);
const files = [...new Set(
  audit.violations
    .filter(v =>
      v.file.startsWith('src/components/features/sessions/') ||
      (v.file.startsWith('src/components/session/') && !v.file.includes('/live/'))
    )
    .map(v => v.file)
)];

// Mapping table — ORDER MATTERS: longer/compound patterns must come first.
// Each entry: [regex, replacement, description]
const MAPPINGS = [
  // ────── Compound dark: variants (most specific first) ──────
  [/\btext-slate-(900|950) dark:text-(amber-50|slate-100|slate-50|stone-50|stone-100)\b/g, 'text-foreground'],
  [/\btext-slate-(700|800) dark:text-slate-(100|200|300)\b/g, 'text-foreground'],
  [/\btext-slate-(500|600) dark:text-slate-(300|400|500)\b/g, 'text-muted-foreground'],
  [/\btext-slate-400 dark:text-slate-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-stone-(700|800|900) dark:text-stone-(100|200|300)\b/g, 'text-foreground'],
  [/\btext-stone-(400|500|600) dark:text-stone-(300|400|500)\b/g, 'text-muted-foreground'],
  [/\btext-gray-(700|800|900) dark:text-gray-(100|200|300)\b/g, 'text-foreground'],
  [/\btext-gray-(400|500|600) dark:text-gray-(300|400|500)\b/g, 'text-muted-foreground'],

  [/\bbg-white dark:bg-slate-(800|900)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-white dark:bg-stone-(800|900)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-white dark:bg-gray-(800|900)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-slate-(50|100) dark:bg-slate-(700|800|900)(?:\/\d+)?\b/g, 'bg-muted'],
  [/\bbg-stone-(50|100) dark:bg-stone-(700|800|900)(?:\/\d+)?\b/g, 'bg-muted'],
  [/\bbg-gray-(50|100) dark:bg-gray-(700|800|900)(?:\/\d+)?\b/g, 'bg-muted'],

  [/\bhover:bg-slate-(50|100) dark:hover:bg-slate-(700|800|900)(?:\/\d+)?\b/g, 'hover:bg-muted'],
  [/\bhover:bg-stone-(50|100) dark:hover:bg-stone-(700|800|900)(?:\/\d+)?\b/g, 'hover:bg-muted'],

  [/\bborder-slate-(100|200|300) dark:border-slate-(700|800)\b/g, 'border-border'],
  [/\bborder-stone-(100|200|300) dark:border-stone-(600|700|800)\b/g, 'border-border'],
  [/\bborder-gray-(100|200|300) dark:border-gray-(700|800)\b/g, 'border-border'],

  [/\bdivide-slate-(50|100|200) dark:divide-slate-(700|800)(?:\/\d+)?\b/g, 'divide-border'],
  [/\bdivide-stone-(100|200) dark:divide-stone-(700|800)\b/g, 'divide-border'],

  // ────── Solo neutral classes (no dark: variant) ──────
  [/\btext-slate-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-stone-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-gray-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-zinc-(400|500|600)\b/g, 'text-muted-foreground'],

  [/\btext-slate-(700|800|900|950)\b/g, 'text-foreground'],
  [/\btext-stone-(700|800|900|950)\b/g, 'text-foreground'],
  [/\btext-gray-(700|800|900|950)\b/g, 'text-foreground'],

  [/\bbg-slate-(50|100)\b/g, 'bg-muted'],
  [/\bbg-stone-(50|100)\b/g, 'bg-muted'],
  [/\bbg-gray-(50|100)\b/g, 'bg-muted'],

  [/\bbg-slate-(200|300)\b/g, 'bg-muted'],
  [/\bbg-stone-(200|300)\b/g, 'bg-muted'],
  [/\bbg-gray-(200|300)\b/g, 'bg-muted'],

  [/\bbg-slate-(700|800|900)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-stone-(700|800|900)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-gray-(700|800|900)(?:\/\d+)?\b/g, 'bg-card'],

  [/\bborder-slate-(100|200|300)\b/g, 'border-border'],
  [/\bborder-stone-(100|200|300)\b/g, 'border-border'],
  [/\bborder-gray-(100|200|300)\b/g, 'border-border'],

  [/\bborder-slate-(600|700|800)\b/g, 'border-border'],
  [/\bborder-stone-(600|700|800)\b/g, 'border-border'],
  [/\bborder-gray-(600|700|800)\b/g, 'border-border'],

  // ────── Glass overlays (bg-white/N) ──────
  [/\bbg-white\/70\b/g, 'bg-card/70'],
  [/\bbg-white\/50\b/g, 'bg-card/50'],
  [/\bbg-white\/40\b/g, 'bg-card/40'],
  [/\bbg-white\/(\d+)\b/g, 'bg-card/$1'],

  // ────── White text/border with opacity (no colored bg in same string — handled by rule exemption) ──────
  [/\btext-white\/(40|30|20)\b/g, 'text-muted-foreground'],
  [/\btext-white\/(50|60|70)\b/g, 'text-foreground/80'],
  [/\bborder-white\/(\d+)\b/g, 'border-border'],
  [/\bring-white\/(\d+)\b/g, 'ring-ring/30'],

  // ────── Backdrop ──────
  [/\bbg-black\/(\d+)\b/g, 'bg-foreground/$1'],

  // ────── DS-5 pass 2 residuals ──────
  // Compound `bg-white dark:bg-X` → bg-card (adapts auto via canonical tokens)
  [/\bbg-white dark:bg-(card|background|muted|popover)\b/g, 'bg-$1'],
  [/\bhover:bg-white dark:hover:bg-(card|background|muted|popover)\b/g, 'hover:bg-$1'],
  // Compound `text-white dark:text-foreground` → on light bg this is white text
  // (legacy pattern); migrate to text-primary-foreground (white in light, dark
  // in dark). On colored bg the rule exemption already handles it.
  [/\btext-white dark:text-foreground\b/g, 'text-primary-foreground'],

  [/\bbg-stone-(950)\b/g, 'bg-card'],
  [/\bbg-slate-(950)\b/g, 'bg-card'],
  [/\btext-stone-(300|200|100|50)\b/g, 'text-foreground'],
  [/\btext-gray-(300|200|100|50)\b/g, 'text-foreground'],
  [/\bbg-slate-(400|500)\b/g, 'bg-muted-foreground'],
  [/\bbg-stone-(400|500)\b/g, 'bg-muted-foreground'],
  [/\bbg-gray-(400|500)\b/g, 'bg-muted-foreground'],
  [/\bborder-slate-(400|500)\b/g, 'border-border-strong'],
  [/\bborder-stone-(400|500)\b/g, 'border-border-strong'],
  [/\bring-slate-(100|200|300)\b/g, 'ring-border'],
  [/\bring-stone-(100|200|300)\b/g, 'ring-border'],
  [/\bring-gray-(100|200|300)\b/g, 'ring-border'],
  [/\bring-black(?:\/\d+)?\b/g, 'ring-border'],
  [/\bbg-black\b(?!\/)/g, 'bg-foreground'],
  // bg-white standalone (no opacity/dark variant) → assume card surface
  [/\bbg-white\b(?!\/|\s+dark:)/g, 'bg-card'],
];

let totalChanges = 0;
const changedFiles = [];

for (const relFile of files) {
  const absFile = resolve(ROOT, relFile);
  let content;
  try {
    content = readFileSync(absFile, 'utf8');
  } catch (err) {
    console.error(`Skip ${relFile}: ${err.message}`);
    continue;
  }
  const original = content;
  for (const [regex, replacement] of MAPPINGS) {
    content = content.replace(regex, replacement);
  }
  if (content !== original) {
    writeFileSync(absFile, content);
    const diff = original.split('\n').length - content.split('\n').length;
    console.log(`MODIFIED: ${relFile}`);
    changedFiles.push(relFile);
    totalChanges += 1;
  }
}

console.log(`\n[ds-5-codemod] Modified ${totalChanges}/${files.length} files.`);
