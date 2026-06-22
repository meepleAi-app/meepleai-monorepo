#!/usr/bin/env node
/**
 * DS-13a codemod — app/admin/(dashboard)/* migration (Tier 3, sub-a).
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');

const audit = JSON.parse(
  readFileSync(resolve(__dirname, '..', '..', '..', 'audits', '2026-05-12-token-violations.json'), 'utf8')
);
const files = [...new Set(
  audit.violations
    .filter(v => v.file.startsWith('src/app/admin/(dashboard)/'))
    .map(v => v.file)
)];

const MAPPINGS = [
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

  [/\btext-slate-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-stone-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-gray-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-zinc-(400|500|600)\b/g, 'text-muted-foreground'],
  [/\btext-slate-(700|800|900|950)\b/g, 'text-foreground'],
  [/\btext-stone-(700|800|900|950)\b/g, 'text-foreground'],
  [/\btext-gray-(700|800|900|950)\b/g, 'text-foreground'],
  [/\btext-stone-(300|200|100|50)\b/g, 'text-foreground'],
  [/\btext-gray-(300|200|100|50)\b/g, 'text-foreground'],

  [/\bbg-slate-(50|100|200|300)\b/g, 'bg-muted'],
  [/\bbg-stone-(50|100|200|300)\b/g, 'bg-muted'],
  [/\bbg-gray-(50|100|200|300)\b/g, 'bg-muted'],
  [/\bbg-slate-(700|800|900|950)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-stone-(700|800|900|950)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-gray-(700|800|900|950)(?:\/\d+)?\b/g, 'bg-card'],
  [/\bbg-slate-(400|500)\b/g, 'bg-muted-foreground'],
  [/\bbg-stone-(400|500)\b/g, 'bg-muted-foreground'],
  [/\bbg-gray-(400|500)\b/g, 'bg-muted-foreground'],

  [/\bborder-slate-(100|200|300|400|500|600|700|800)\b/g, 'border-border'],
  [/\bborder-stone-(100|200|300|400|500|600|700|800)\b/g, 'border-border'],
  [/\bborder-gray-(100|200|300|400|500|600|700|800)\b/g, 'border-border'],

  [/\bbg-white\/(\d+)\b/g, 'bg-card/$1'],
  [/\btext-white\/(40|30|20)\b/g, 'text-muted-foreground'],
  [/\btext-white\/(50|60|70)\b/g, 'text-foreground/80'],
  [/\bborder-white\/(\d+)\b/g, 'border-border'],
  [/\bring-white\/(\d+)\b/g, 'ring-ring/30'],
  [/\bbg-black\/(\d+)\b/g, 'bg-foreground/$1'],

  [/\bring-slate-(100|200|300)\b/g, 'ring-border'],
  [/\bring-stone-(100|200|300)\b/g, 'ring-border'],
  [/\bring-black(?:\/\d+)?\b/g, 'ring-border'],
  [/\bbg-black\b(?!\/)/g, 'bg-foreground'],
  [/\bbg-white\b(?!\/|\s+dark:)/g, 'bg-card'],

  [/\bbg-white dark:bg-(card|background|muted|popover)\b/g, 'bg-$1'],
  [/\bhover:bg-white dark:hover:bg-(card|background|muted|popover)\b/g, 'hover:bg-$1'],
  [/\btext-white dark:text-foreground\b/g, 'text-primary-foreground'],
];

let total = 0;
for (const rel of files) {
  const abs = resolve(ROOT, rel);
  let content;
  try { content = readFileSync(abs, 'utf8'); }
  catch (err) { console.error(`Skip ${rel}: ${err.message}`); continue; }
  const original = content;
  for (const [re, repl] of MAPPINGS) content = content.replace(re, repl);
  if (content !== original) {
    writeFileSync(abs, content);
    console.log(`MODIFIED: ${rel}`);
    total += 1;
  }
}
console.log(`\n[ds-13a-codemod] Modified ${total}/${files.length} files.`);
