/**
 * Genera `e2e/audit/route-params.json` — la mappa che il crawler usa per sostituire i segmenti
 * dinamici delle rotte. Si esegue con `pnpm audit:params`.
 *
 * #4056. Prima di questo script la mappa non si poteva ottenere: `route-params.json` è
 * **gitignored** (`.gitignore:327`) e nessun comando lo produceva — `resolveParams(psqlRunner())`
 * esisteva, era documentato per questo scopo, e non era chiamato da nessuno tranne i suoi test
 * unitari. Quindi su un checkout pulito il file non esiste, `crawl.spec.ts` ricade su una mappa
 * **vuota** (`existsSync(PARAMS) ? … : {}`) e il crawler salta ogni rotta parametrica. Nessuno se
 * ne accorge, perché il crawler non asserisce: l'unico segnale è un conteggio di salti che
 * nessuna baseline guarda.
 *
 * Dove invece il file esiste da una passata precedente, invecchia: dopo un ri-seeding del database
 * i suoi id puntano a entità inesistenti, il crawler visita pagine "not found" e il report
 * attribuisce al prodotto un'anomalia che è della mappa.
 *
 * Ordine dei passi, e non è indifferente:
 *   1. il seeder CREA le condivisioni via API e cattura ciò che il database non conserva;
 *   2. le query SQL leggono le righe, comprese quelle appena create;
 *   3. i valori del seeder vincono sul risultato SQL, perché sa quali entità ha appena fatto.
 *
 * Alla fine incrocia la mappa con l'inventario e stampa le rotte che il crawler salterebbe
 * ancora: un parametro mancante è un pezzo di prodotto che nessuno attraversa, e va visto subito,
 * non scoperto leggendo un conteggio di salti.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { psqlRunner, resolveParams, resolveRouteUrl } from './resolve-params';
import { seedShareParams } from './seed-share-params';

const OUT = path.resolve('e2e/audit/route-params.json');
const INVENTORY = path.resolve(
  '../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv'
);

type Row = { id: string; path: string; ruolo: string };

function readParametricRoutes(): Row[] {
  const [, ...lines] = readFileSync(INVENTORY, 'utf8').trim().split('\n');
  return lines
    .map(line => line.split(','))
    .map(c => ({ id: c[0], tipo: c[1], path: c[2], ruolo: c[5] }))
    .filter(r => r.tipo === 'route' && (r.ruolo === 'user' || r.ruolo === 'admin'))
    .filter(r => r.path.includes('['))
    .map(({ id, path: p, ruolo }) => ({ id, path: p, ruolo }));
}

async function main(): Promise<void> {
  const skipSeed = process.argv.includes('--no-seed');
  // `--check` non semina e non scrive: riferisce solo quali rotte resterebbero saltate con la
  // mappa già sul disco. Esiste perché `--no-seed` **sovrascrive** il file con il solo risultato
  // SQL, e usarlo come diagnostica cancella i token appena seminati — sbagliato una volta.
  const checkOnly = process.argv.includes('--check');

  if (checkOnly) {
    const current = JSON.parse(readFileSync(OUT, 'utf8')) as Record<string, string>;
    reportCoverage(current);
    return;
  }

  let seeded: Record<string, string> = {};
  if (skipSeed) {
    console.log('--no-seed: nessuna entità creata, si usano solo le query SQL');
  } else {
    const result = await seedShareParams();
    seeded = result.params;
    console.log(`seminati: ${Object.keys(seeded).sort().join(', ') || '(nessuno)'}`);
    for (const f of result.failures) console.warn(`⚠️  ${f}`);
  }

  const fromDb = resolveParams(psqlRunner());
  const params = { ...fromDb, ...seeded };

  // Indentazione 2 e newline finale: così il file generato è già conforme a prettier e un
  // `format:check` non lo segnala come da riformattare a ogni rigenerazione.
  writeFileSync(OUT, `${JSON.stringify(params, null, 2)}\n`, 'utf8');
  console.log(
    `\nscritto ${path.relative(process.cwd(), OUT)} con ${Object.keys(params).length} chiavi`
  );

  reportCoverage(params);
}

/** Quante rotte la mappa copre, e quali no — con i nomi dei parametri che mancano. */
function reportCoverage(params: Record<string, string>): void {
  const routes = readParametricRoutes();
  const unresolved = routes.filter(r => resolveRouteUrl(r.path, params) === null);

  console.log(
    `rotte parametriche: ${routes.length} · risolte: ${routes.length - unresolved.length}`
  );

  if (unresolved.length === 0) {
    console.log('nessuna rotta resterà saltata');
    return;
  }

  console.log(`\n🔴 ${unresolved.length} rotte resteranno saltate:`);
  const missingNames = new Set<string>();
  for (const r of unresolved) {
    console.log(`  ${r.ruolo}: ${r.path}`);
    for (const m of r.path.matchAll(/\[(?:\.\.\.)?(\w+)\]/g)) {
      if (!params[m[1]]) missingNames.add(m[1]);
    }
  }
  console.log(
    `\nparametri senza valore: ${
      [...missingNames].join(', ') ||
      '(nessuno — manca la riga ' + 'per prefisso in GENERIC_PARAM_SOURCES)'
    }`
  );

  // Uscita diversa da zero: la mappa è stata scritta (è comunque migliore di prima), ma chi
  // ha invocato il comando deve sapere che la copertura non è completa.
  process.exitCode = 1;
}

// Non `await main()` a livello di modulo: gli script di `scripts/audit/` sono compilati da tsx
// come CJS, dove il top-level await è un errore di transform ("Top-level await is currently not
// supported with the cjs output format") — fallisce prima di eseguire una riga.
main().catch((err: unknown) => {
  console.error(err instanceof Error ? err.message : err);
  process.exitCode = 2;
});
