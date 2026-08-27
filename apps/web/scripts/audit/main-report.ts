/**
 * Trasforma la passata del crawler in report e stati del tracker.
 * Si esegue con `pnpm audit:report`.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { applyStatuses, classify, renderMarkdown, type CrawlEntry } from './render-report';

const OUT_DIR = path.resolve('../../docs/for-developers/audits/2026-08-26-full-feature-audit');
const CSV = path.join(OUT_DIR, 'inventory.csv');
const RESULTS_DIR = path.resolve('audit-results');
const EVIDENCE_DIR = path.join(OUT_DIR, 'evidence');
const WAVE = process.env.AUDIT_WAVE ?? 'wave-0-harness';

const entries: CrawlEntry[] = readFileSync(path.join(RESULTS_DIR, 'entries.jsonl'), 'utf8')
  .trim()
  .split('\n')
  .filter(Boolean)
  .map(line => JSON.parse(line) as CrawlEntry);

if (entries.length === 0) {
  // Fallire qui è deliberato: un report vuoto letto come "nessun problema" è il
  // modo più facile di rendere verde un audit che non ha guardato nulla.
  throw new Error('entries.jsonl è vuoto: la passata del crawler non ha prodotto evidenze');
}

// In evidence/ finiscono SOLO gli screenshot dei problemi: committare centinaia
// di immagini fullPage appesantirebbe il repo senza aggiungere informazione.
mkdirSync(EVIDENCE_DIR, { recursive: true });
let copied = 0;
for (const e of entries) {
  if (classify(e) === 'ok' || !e.screenshot) continue;
  const src = path.join(RESULTS_DIR, e.screenshot);
  if (!existsSync(src)) continue;
  copyFileSync(src, path.join(EVIDENCE_DIR, path.basename(e.screenshot)));
  copied += 1;
}

writeFileSync(path.join(OUT_DIR, `${WAVE}.md`), renderMarkdown(entries), 'utf8');
writeFileSync(CSV, applyStatuses(readFileSync(CSV, 'utf8'), entries), 'utf8');

console.log(`entries: ${entries.length} · evidenze copiate: ${copied} · report: ${WAVE}.md`);
