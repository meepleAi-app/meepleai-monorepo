/**
 * Raccoglie le evidenze attorno a un'azione dell'audit: righe di log in errore
 * emesse dal backend nella finestra dell'azione, e tabelle il cui numero di
 * righe è cambiato.
 *
 * Il diff per conteggi è volutamente grossolano: dice *dove* si è scritto
 * ("questa azione ha toccato game_sessions e outbox_messages"), non se il
 * contenuto sia corretto. Per le funzioni L2/L3 si legge poi la riga vera.
 *
 * Sorgente dei log: `docker logs meepleai-api`. Seq è opzionale — gira solo
 * sotto il profilo `monitoring` e non espone porte sull'host.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { execFileSync } from 'node:child_process';

import type { SqlRunner } from './resolve-params';

export type TableCounts = Record<string, number>;
export type Evidence = {
  errors: string[];
  changedTables: Array<{ table: string; delta: number }>;
};

const COUNTS_SQL = "SELECT relname || '|' || n_live_tup FROM pg_stat_user_tables ORDER BY relname";
const ERROR_LINE = /\b(ERR|FTL|ERROR|FATAL)\b|level=(Error|Fatal)/;

export function parseTableCounts(psqlOutput: string): TableCounts {
  const counts: TableCounts = {};
  for (const line of psqlOutput.split('\n')) {
    const [table, value] = line.trim().split('|');
    if (table && value !== undefined) counts[table] = Number(value);
  }
  return counts;
}

export function diffCounts(before: TableCounts, after: TableCounts): Evidence['changedTables'] {
  return Object.keys({ ...before, ...after })
    .map(table => ({ table, delta: (after[table] ?? 0) - (before[table] ?? 0) }))
    .filter(d => d.delta !== 0)
    .sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta) || a.table.localeCompare(b.table));
}

export function filterErrorLines(logOutput: string): string[] {
  return logOutput.split('\n').filter(line => ERROR_LINE.test(line));
}

export function snapshotCounts(run: SqlRunner): TableCounts {
  return parseTableCounts(run(COUNTS_SQL));
}

/**
 * Righe di errore emesse dall'API dal marker in poi.
 *
 * Un fallimento qui NON va silenziato: un collettore che restituisce sempre
 * lista vuota renderebbe verde l'intero audit senza aver letto nulla.
 */
export function readErrorLogs(sinceIso: string, container = 'meepleai-api'): string[] {
  const out = execFileSync('docker', ['logs', container, '--since', sinceIso], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  return filterErrorLines(out);
}
