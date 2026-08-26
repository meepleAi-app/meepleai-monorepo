/**
 * Riporta nel tracker le verifiche eseguite a mano.
 *
 * Il crawler aggiorna solo le rotte che percorre; le verifiche di livello L2/L3
 * — mutazioni, casi negativi, effetti su DB e log — si eseguono a mano e senza
 * questo passaggio resterebbero fuori dal conteggio di copertura, facendo
 * sembrare scoperto ciò che è stato verificato meglio del resto.
 *
 * Uso: `pnpm tsx scripts/audit/mark-verified.ts <file.jsonl>`
 * Ogni riga: {"metodo":"POST","path":"/api/v1/auth/login","livello":"L3","esito":"atteso","evidenza":"..."}
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

export type ManualCheck = {
  metodo: string;
  path: string;
  livello: 'L1' | 'L2' | 'L3';
  esito: 'atteso' | 'difforme';
  evidenza: string;
};

const STATO = { atteso: '✅ verificato', difforme: '⚠️ finding da aprire' } as const;

/** Chiave di confronto: metodo + path, indipendente dall'id hash del tracker. */
const key = (metodo: string, p: string): string => `${metodo.toUpperCase()} ${p}`;

/**
 * Applica le verifiche manuali al CSV. Ritorna il csv aggiornato e quante
 * verifiche non hanno trovato riscontro: una verifica senza riga corrispondente
 * di solito significa che il path è stato scritto a mano e non combacia con
 * l'inventario — va corretta, non ignorata.
 */
export function applyManualChecks(
  csv: string,
  checks: ManualCheck[]
): { csv: string; orfane: ManualCheck[] } {
  const byKey = new Map(checks.map(c => [key(c.metodo, c.path), c]));
  const trovate = new Set<string>();
  const [header, ...lines] = csv.trim().split('\n');

  const updated = lines.map(line => {
    const cells = line.split(',');
    const k = key(cells[3], cells[2]);
    const check = byKey.get(k);
    if (!check) return line;

    trovate.add(k);
    cells[6] = check.livello;
    cells[7] = STATO[check.esito];
    cells[8] = check.evidenza.replace(/[",\n]/g, ' ').slice(0, 120);
    return cells.join(',');
  });

  return {
    csv: [header, ...updated].join('\n') + '\n',
    orfane: checks.filter(c => !trovate.has(key(c.metodo, c.path))),
  };
}

if (require.main === module) {
  const input = process.argv[2];
  if (!input) throw new Error('serve il percorso del file .jsonl con le verifiche');

  const CSV = path.resolve(
    '../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv'
  );
  const checks = readFileSync(input, 'utf8')
    .trim()
    .split('\n')
    .filter(Boolean)
    .map(l => JSON.parse(l) as ManualCheck);

  const { csv, orfane } = applyManualChecks(readFileSync(CSV, 'utf8'), checks);
  writeFileSync(CSV, csv, 'utf8');

  console.log(`verifiche applicate: ${checks.length - orfane.length} su ${checks.length}`);
  if (orfane.length) {
    console.log('SENZA RISCONTRO nel tracker (path da correggere):');
    orfane.forEach(o => console.log(`  ${o.metodo} ${o.path}`));
  }
}
