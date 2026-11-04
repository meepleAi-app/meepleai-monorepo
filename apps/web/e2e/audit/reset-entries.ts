/**
 * Azzera `audit-results/entries.jsonl` all'inizio di ogni passata.
 *
 * #4056. `crawl.spec.ts` scrive con `appendFileSync`, e nessuno troncava il file: due passate
 * finivano sovrapposte nello stesso JSONL, e `main-report.ts` le classificava **insieme**.
 * Misurato sul file dello stack locale: 658 righe, 329 chiavi `rotta|ruolo` distinte, **tutte**
 * con due righe — una passata di agosto e una di ottobre fuse, con sei URL che portavano ancora
 * `dateFrom=2026-08-27` nelle query string.
 *
 * Perché è dannoso e non solo ridondante: il report ricava il verdetto per rotta, e con due
 * osservazioni della stessa rotta vince quella che arriva per ultima nell'iterazione. Una rotta
 * riparata resta «rotta» se la riga vecchia la segue, e una rotta che si è appena rotta passa per
 * sana. L'audit riporta lo stato di un prodotto che non esiste in nessun istante.
 *
 * Perché un globalSetup e non il primo test: con `workers: 1` l'append è sicuro, ma il
 * troncamento deve avvenire **una volta per passata**. Dentro uno spec girerebbe per ogni file
 * della suite, cancellando le righe dei file eseguiti prima.
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const RESULTS_DIR = path.join(__dirname, '../../audit-results');

/**
 * I JSONL che **questa passata** scrive, e solo quelli.
 *
 * Non `probe-*.jsonl` né `user-mutations.jsonl`/`config-mutations.jsonl`: li producono gli script
 * di `scripts/audit/`, che girano separatamente. Azzerarli qui cancellerebbe evidenze di un'altra
 * fase, e l'errore sarebbe silenzioso come quello che questo file corregge.
 *
 * L'elenco si ricava con:
 *   grep -rhoE "'[a-z0-9-]+\.jsonl'" apps/web/e2e/audit/*.ts | sort -u
 */
const PASSATA = ['entries.jsonl', 'ui-interazioni.jsonl', 'ui-flussi.jsonl', 'wave1a.jsonl'];

export default function resetEntries(): void {
  mkdirSync(RESULTS_DIR, { recursive: true });
  for (const nome of PASSATA) {
    writeFileSync(path.join(RESULTS_DIR, nome), '', 'utf8');
  }
  // Su stdout e non in un file: se questa riga non compare nel log della passata, i JSONL che il
  // report leggerà contengono anche osservazioni precedenti. `ui-interazioni.jsonl` ne aveva
  // accumulate quattro — 24 righe per 6 casi — e gli esiti `difforme` di una passata vecchia
  // sopravvivevano a una correzione, perché nulla li cancellava.
  console.log(`[audit] azzerati ${PASSATA.join(', ')} per questa passata`);
}
