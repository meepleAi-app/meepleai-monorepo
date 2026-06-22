#!/usr/bin/env node
/**
 * DS-16 codemod — CSS variable migration (Phase 2 of token canonicalization).
 *
 * Renames legacy v1 CSS custom property references in inline `style` props
 * and CSS files to the canonical names from `design-tokens-canonical.css`,
 * so the bridge layer can be removed.
 *
 * Bridge file: apps/web/src/styles/token-bridge.css (deleted at end of DS-16).
 *
 * Scope: src/**\/*.{ts,tsx,css}
 */
import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');
const SRC = resolve(ROOT, 'src');

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) {
      if (name === 'node_modules' || name === '.next' || name === '__tests__') continue;
      walk(full, out);
    } else if (
      st.isFile() &&
      (/\.(tsx?|css)$/.test(full)) &&
      !/\.(test|spec)\.(tsx?|jsx?)$/.test(full)
    ) {
      out.push(full);
    }
  }
  return out;
}

// Mapping of legacy v1 token names → canonical names.
// Order: longest names first to avoid prefix overlap.
const TOKEN_RENAMES = [
  // Gaming (legacy "premium gaming" palette) → canonical
  ['--gaming-bg-base', '--bg'],
  ['--gaming-bg-elevated', '--bg-card'],
  ['--gaming-bg-glass', '--glass-bg'],
  ['--gaming-bg-glass-hover', '--glass-bg'],
  ['--gaming-border-glass', '--glass-border'],
  ['--gaming-border-glass-hover', '--glass-border'],
  ['--gaming-border-glass-strong', '--glass-border'],
  ['--gaming-text-primary', '--text'],
  ['--gaming-text-secondary', '--text-sec'],
  ['--gaming-text-tertiary', '--text-muted'],
  ['--gaming-text-accent', '--brand-fg'],
  // nh-* warm-modern palette → canonical
  ['--nh-bg-base', '--bg'],
  ['--nh-bg-surface-end', '--bg-muted'],
  ['--nh-bg-surface', '--bg-card'],
  ['--nh-bg-elevated', '--bg-card'],
  ['--nh-text-primary', '--text'],
  ['--nh-text-secondary', '--text-sec'],
  ['--nh-text-muted', '--text-muted'],
  ['--nh-border-default', '--border'],
  // Bridge aliases (declared in token-bridge.css) → canonical
  ['--bg-elevated', '--bg-card'],
  ['--bg-surface', '--bg-card'],
  ['--bg-sunken-runtime', '--bg-sunken'],
  ['--bg-base', '--bg'],
  ['--text-primary', '--text'],
  ['--text-secondary', '--text-sec'],
  ['--text-tertiary', '--text-muted'],
  ['--border-primary', '--border'],
  ['--border-secondary', '--border-strong'],
  ['--bg-glass', '--glass-bg'],
  ['--border-glass', '--glass-border'],
  // Entity colors --e-* → --c-* (canonical mockup names)
  ['--e-game', '--c-game'],
  ['--e-player', '--c-player'],
  ['--e-session', '--c-session'],
  ['--e-agent', '--c-agent'],
  ['--e-document', '--c-kb'],
  ['--e-chat', '--c-chat'],
  ['--e-event', '--c-event'],
  ['--e-toolkit', '--c-toolkit'],
  ['--e-tool', '--c-tool'],
];

const files = walk(SRC);
let totalFiles = 0;
let totalReplacements = 0;

for (const abs of files) {
  let content;
  try { content = readFileSync(abs, 'utf8'); }
  catch { continue; }
  const original = content;
  let fileCount = 0;

  for (const [from, to] of TOKEN_RENAMES) {
    // Match in:
    //   var(--name)        — CSS / inline style
    //   "--name"           — string literal (uncommon)
    //   Tailwind bg-[var(--name)] — already matched by var(--…) above
    // Use a regex that matches the token name in any of these contexts.
    // Word boundary at start to avoid matching `--gaming-bg-base-overlay` etc.
    // (note: `-` is treated as word boundary by JS regex \b — works for CSS idents)
    const pattern = new RegExp(`(?<![\\w-])${from.replace(/[-]/g, '\\-')}(?![\\w-])`, 'g');
    const matches = content.match(pattern);
    if (matches) {
      content = content.replace(pattern, to);
      fileCount += matches.length;
    }
  }

  if (content !== original) {
    writeFileSync(abs, content);
    const rel = abs.replace(ROOT + '\\', '').replace(ROOT + '/', '');
    console.log(`MODIFIED (${fileCount} renames): ${rel}`);
    totalFiles += 1;
    totalReplacements += fileCount;
  }
}

console.log(`\n[ds-16-codemod] Modified ${totalFiles} files, ${totalReplacements} total token renames.`);
