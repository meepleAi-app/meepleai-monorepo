/**
 * Registrazione delle osservazioni delle wave di interazione.
 *
 * Estratto da `wave-ui-interazioni.spec.ts` perché serve a più di una wave, e duplicarlo farebbe
 * divergere il formato del JSONL che `render-report.ts` legge.
 *
 * 🔴 Due categorie, e la differenza è il punto del file. `registra()` **non fa fallire il test**:
 * descrive ciò che si è visto, e un `difforme` resta verde. Va bene per «il prodotto si comporta
 * in modo diverso dall'atteso», che è un finding da triagare, non un gate.
 *
 * Non va bene per «non ho potuto osservare»: se il pulsante che il flusso richiede non esiste, il
 * test non ha misurato niente, e un verde li` è peggio di un rosso — dice che il flusso funziona
 * quando nessuno l'ha percorso. Per quel caso c'è `prerequisito()`, che **fallisce**.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';

import { expect, type Locator } from '@playwright/test';

const RESULTS = path.join(__dirname, '../../audit-results');

export type Esito = 'atteso' | 'difforme' | 'da-guardare';

export type Osservazione = {
  caso: string;
  rotta: string;
  ruolo: string;
  esito: Esito;
  osservato: string;
};

/** Scrive l'osservazione nel JSONL della wave e la stampa. Non asserisce. */
export function registraIn(file: string, o: Osservazione): void {
  mkdirSync(RESULTS, { recursive: true });
  appendFileSync(path.join(RESULTS, file), JSON.stringify(o) + '\n', 'utf8');
  const tag = o.esito === 'atteso' ? 'OK  ' : o.esito === 'difforme' ? 'DIFF' : 'GUAR';

  console.log(`${tag} ${o.caso.padEnd(46)} ${o.osservato.slice(0, 90)}`);
}

/**
 * Il flusso richiede questo elemento per essere percorso: se manca, il test **fallisce**.
 *
 * Non è pedanteria: senza questa distinzione un selettore che cambia trasforma sei test di flusso
 * in sei verdi che non hanno cliccato niente, e la suite diventa un ornamento.
 */
export async function prerequisito(
  locator: Locator,
  cosa: string,
  timeout = 10_000
): Promise<void> {
  await expect(locator, `prerequisito assente: ${cosa}`).toBeVisible({ timeout });
}
