/**
 * Risolve i segmenti dinamici delle rotte in id reali, interrogando il Postgres
 * dello stack locale.
 *
 * Un parametro non risolto significa rotte non visitate: il crawler le salta e
 * il conteggio finale lo rende visibile. Le query falliscono in silenzio per
 * costruzione (tabella assente, DB non pronto), quindi i nomi vanno verificati
 * contro lo schema reale prima di fidarsi del risultato.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { execFileSync } from 'node:child_process';

export type SqlRunner = (sql: string) => string;

/** Un id reale per ogni segmento dinamico che compare nelle rotte. */
export const PARAM_QUERIES: Record<string, string> = {
  gameId: 'SELECT id FROM games WHERE is_deleted = false LIMIT 1',
  threadId: 'SELECT id FROM chat_threads LIMIT 1',
  sessionId: 'SELECT id FROM game_sessions LIMIT 1',
  userId: 'SELECT id FROM users LIMIT 1',
  agentId: 'SELECT id FROM agents LIMIT 1',
};

/** Esegue le query e raccoglie i valori. Un fallimento singolo non ferma gli altri. */
export function resolveParams(run: SqlRunner): Record<string, string> {
  const params: Record<string, string> = {};

  for (const [name, sql] of Object.entries(PARAM_QUERIES)) {
    try {
      const value = run(sql).trim().split('\n')[0]?.trim();
      if (value) params[name] = value;
    } catch {
      // Tabella assente o DB non pronto: il parametro resta non risolto e il
      // crawler salterà le rotte che lo richiedono, segnalandole nel report.
    }
  }
  return params;
}

/** Runner reale: psql dentro il container Postgres dello stack locale. */
export function psqlRunner(container = 'meepleai-postgres'): SqlRunner {
  return sql =>
    execFileSync(
      'docker',
      ['exec', container, 'psql', '-U', 'meepleai', '-d', 'meepleai', '-t', '-A', '-c', sql],
      { encoding: 'utf8' }
    );
}
