#!/usr/bin/env node
/**
 * CLI glue del report builder (#2999): legge captures.json + i PNG,
 * li converte in data-URI, chiama buildReportHtml, scrive gallery.html.
 */
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { buildReportHtml } from './build-report.mjs';

const OUTPUT_DIR = path.resolve(process.cwd(), 'mockup-compare-output');
const CAPTURES = path.join(OUTPUT_DIR, 'captures.json');

function pngToDataUri(relPath) {
  if (!relPath) return null;
  const abs = path.join(OUTPUT_DIR, relPath);
  if (!existsSync(abs)) return null;
  return `data:image/png;base64,${readFileSync(abs).toString('base64')}`;
}

function readDesignIntent(_id) {
  // fidelity companion opzionale (design_intent) — estensione futura.
  return undefined;
}

function main() {
  if (!existsSync(CAPTURES)) {
    console.error(
      `[compare] captures.json non trovato in ${OUTPUT_DIR}. Esegui prima la capture spec.`
    );
    process.exit(1);
  }
  const captures = JSON.parse(readFileSync(CAPTURES, 'utf8'));
  const entries = captures.map((c) => ({
    id: c.id,
    label: c.label,
    route: c.route,
    viewport: c.viewport,
    mockupDataUri: pngToDataUri(c.mockupPng),
    mockupError: c.mockupError,
    liveDataUri: pngToDataUri(c.livePng),
    liveError: c.liveError,
    designIntent: readDesignIntent(c.id),
  }));
  const html = buildReportHtml(entries);
  const out = path.join(OUTPUT_DIR, 'gallery.html');
  writeFileSync(out, html, 'utf8');
  console.log(`[compare] gallery generata: ${out}`);
  const failed = entries.filter((e) => e.liveError || e.mockupError).length;
  console.log(`[compare] ${entries.length} coppie (${failed} con cattura fallita)`);

  if (process.argv.includes('--open')) {
    const opener =
      process.platform === 'win32' ? 'start' : process.platform === 'darwin' ? 'open' : 'xdg-open';
    spawn(opener, [out], { shell: true, stdio: 'ignore', detached: true }).unref();
  }
}

main();
