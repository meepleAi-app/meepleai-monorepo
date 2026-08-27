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

/**
 * Un id reale per ogni segmento dinamico che compare nelle rotte.
 *
 * I nomi sono quelli verificati sullo schema il 2026-08-26, non quelli
 * plausibili: non esiste una tabella `games` (il catalogo è `shared_games`) né
 * `agents`, e la convenzione dei nomi di colonna cambia da tabella a tabella —
 * `users."Id"` in PascalCase quotato, `shared_games.id` in snake_case.
 *
 * `/library/[gameId]` risolve su shared_games: il page-client interroga il
 * dettaglio con `GameRefKind.Shared`. Si preferisce un gioco già presente in
 * una libreria, così la pagina ha davvero qualcosa da mostrare.
 */
export const PARAM_QUERIES: Record<string, string> = {
  gameId:
    'SELECT COALESCE((SELECT shared_game_id FROM user_library_entries WHERE shared_game_id IS NOT NULL LIMIT 1), (SELECT id FROM shared_games LIMIT 1))',
  threadId: 'SELECT id FROM chat_sessions LIMIT 1',
  sessionId: 'SELECT "Id" FROM game_sessions LIMIT 1',
  userId: 'SELECT "Id" FROM users LIMIT 1',
  agentId: 'SELECT "Id" FROM agent_sessions LIMIT 1',
};

/**
 * Che cosa significa `[id]` a seconda di dove compare.
 *
 * `[id]` è il parametro più diffuso (40 rotte su 220) ed è generico: in
 * `/admin/users/[id]` è un utente, in `/games/[id]` un gioco. Usare un valore
 * unico produrrebbe 404 su tutte le rotte di tipo diverso, e chiameremmo
 * "rotto" ciò che è solo mal indirizzato. Il prefisso più lungo vince.
 */
const ID_SOURCE_BY_PREFIX: Array<[string, string]> = [
  ['/admin/users', 'userId'],
  ['/admin/games', 'gameId'],
  ['/admin/shared-games', 'gameId'],
  ['/games', 'gameId'],
  ['/library', 'gameId'],
  ['/shared-games', 'gameId'],
  ['/sessions', 'sessionId'],
  ['/play-records', 'sessionId'],
  ['/game-nights', 'sessionId'],
  ['/players', 'userId'],
];

/**
 * Sostituisce i segmenti dinamici di una rotta con id reali.
 * Ritorna null se anche un solo parametro non è risolvibile: meglio saltare la
 * rotta e contarla che visitarla con un id inventato.
 */
export function resolveRouteUrl(route: string, params: Record<string, string>): string | null {
  let unresolved = false;

  const url = route.replace(/\[(?:\.\.\.)?(\w+)\]/g, (_, name: string) => {
    const direct = params[name];
    if (direct) return direct;

    if (name === 'id') {
      const source = ID_SOURCE_BY_PREFIX.filter(
        ([prefix]) => route === prefix || route.startsWith(`${prefix}/`)
      ).sort((a, b) => b[0].length - a[0].length)[0]?.[1];
      const value = source ? params[source] : undefined;
      if (value) return value;
    }

    unresolved = true;
    return '';
  });

  return unresolved ? null : url;
}

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

/**
 * Runner reale: psql dentro il container Postgres dello stack locale.
 *
 * Il database si chiama `meepleai_staging` anche in locale: puntare a
 * `meepleai` fa fallire ogni query, e il fallimento è silenzioso per come
 * `resolveParams` gestisce gli errori.
 */
export function psqlRunner(
  container = 'meepleai-postgres',
  database = process.env.AUDIT_PG_DATABASE ?? 'meepleai_staging',
  user = process.env.AUDIT_PG_USER ?? 'meepleai'
): SqlRunner {
  return sql =>
    execFileSync(
      'docker',
      ['exec', container, 'psql', '-U', user, '-d', database, '-t', '-A', '-c', sql],
      { encoding: 'utf8' }
    );
}
