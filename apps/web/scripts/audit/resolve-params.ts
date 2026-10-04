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

import { KNOWN_PROVIDERS } from '../../src/lib/api/schemas/providers';

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
 *
 * #4056. Le sorgenti di condivisione (share token, codici di join) stanno sotto
 * perché le rotte che le usano erano le 15 che il crawler saltava. Una colonna
 * vuota non è un errore di query: significa che nessuno ha ancora creato quella
 * condivisione, e il valore va seminato con `seed-share-params.ts` prima di
 * rigenerare la mappa.
 */
export const PARAM_QUERIES: Record<string, string> = {
  gameId:
    'SELECT COALESCE((SELECT shared_game_id FROM user_library_entries WHERE shared_game_id IS NOT NULL LIMIT 1), (SELECT id FROM shared_games LIMIT 1))',
  threadId: 'SELECT id FROM chat_sessions LIMIT 1',
  sessionId: 'SELECT "Id" FROM game_sessions LIMIT 1',
  userId: 'SELECT "Id" FROM users LIMIT 1',
  // #4056. Era `agent_sessions`, che nello stack locale ha zero righe: la query non falliva, non
  // restituiva nulla, e il parametro restava assente in silenzio. La sorgente verificata è
  // `knowledge_base.agent_definitions` — `GET /api/v1/agents/{id}` con quell'id risponde 200.
  agentId: 'SELECT id FROM knowledge_base.agent_definitions WHERE NOT is_deleted LIMIT 1',

  // #4056 — sorgenti dei token di condivisione e dei codici di ingresso.
  // `invitation_tokens` memorizza `token_hash`, non il token: l'invito utente
  // NON è ricavabile da qui per costruzione, e arriva solo dal seeder.
  gameNightShareToken:
    'SELECT share_token FROM game_night_events WHERE share_token IS NOT NULL ORDER BY created_at DESC LIMIT 1',
  gameNightInviteToken:
    'SELECT token FROM game_night_invitations WHERE token IS NOT NULL ORDER BY created_at DESC LIMIT 1',
  libraryShareToken:
    'SELECT share_token FROM library_share_links WHERE share_token IS NOT NULL AND revoked_at IS NULL ORDER BY created_at DESC LIMIT 1',
  playRecordShareToken:
    'SELECT "ShareToken" FROM play_records WHERE "ShareToken" IS NOT NULL ORDER BY "CreatedAt" DESC LIMIT 1',
  liveSessionCode:
    'SELECT session_code FROM live_game_sessions WHERE session_code IS NOT NULL ORDER BY created_at DESC LIMIT 1',
};

/**
 * Valori che non vengono dal database perché non ci stanno.
 *
 * `/admin/providers/[name]` non prende un id ma un nome di provider, e la pagina
 * respinge con `notFound()` tutto ciò che non è in `KNOWN_PROVIDERS`. Il valore
 * si importa da lì invece di scriverlo: se un provider viene rinominato, la
 * mappa segue da sola anziché far ricomparire un salto.
 */
export const STATIC_PARAMS: Record<string, string> = {
  providerName: KNOWN_PROVIDERS[0],
};

/**
 * Che cosa significa un parametro generico a seconda di dove compare.
 *
 * `[id]` è il più diffuso (40 rotte su 220) ed è generico: in `/admin/users/[id]`
 * è un utente, in `/games/[id]` un gioco. Usare un valore unico produrrebbe 404
 * su tutte le rotte di tipo diverso, e chiameremmo "rotto" ciò che è solo mal
 * indirizzato. Il prefisso più lungo vince.
 *
 * 🔴 #4056. `[token]` e `[code]` sono generici **esattamente come** `[id]`, e
 * prima lo era solo `[id]`: `[token]` significa cinque cose diverse (invito
 * utente, condivisione game-night, join di una live session, condivisione
 * play-record, condivisione libreria) e un'unica chiave `token` nella mappa ne
 * avrebbe soddisfatta una sola, mandando le altre quattro su un 404 che il
 * report avrebbe attribuito al prodotto. Quando aggiungi una rotta con un
 * parametro generico, aggiungi qui la sua riga: senza, il crawler la salta.
 */
const GENERIC_PARAM_SOURCES: Record<string, Array<[prefix: string, source: string]>> = {
  id: [
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
    // #4056 — prefissi verificati con una GET diretta sull'endpoint corrispondente.
    ['/agents', 'agentId'],
    ['/admin/agents/definitions', 'agentId'],
    ['/hub/games', 'gameId'],
  ],
  token: [
    ['/invites', 'userInviteToken'],
    ['/game-nights/shared', 'gameNightShareToken'],
    ['/play-records/shared', 'playRecordShareToken'],
    ['/library/shared', 'libraryShareToken'],
    // `/join/[token]` interroga `/api/v1/live-sessions/code/{…}`: nonostante il
    // nome del segmento, il valore atteso è un codice di sessione.
    ['/join', 'liveSessionCode'],
  ],
  code: [
    ['/join/event', 'gameNightInviteToken'],
    ['/join/session', 'liveSessionCode'],
  ],
  name: [['/admin/providers', 'providerName']],
};

/** Il valore da usare per un parametro generico in una data rotta, se c'è. */
function resolveGenericParam(
  route: string,
  name: string,
  params: Record<string, string>
): string | undefined {
  const source = (GENERIC_PARAM_SOURCES[name] ?? [])
    .filter(([prefix]) => route === prefix || route.startsWith(`${prefix}/`))
    .sort((a, b) => b[0].length - a[0].length)[0]?.[1];

  return source ? params[source] : undefined;
}

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

    const generic = resolveGenericParam(route, name, params);
    if (generic) return generic;

    unresolved = true;
    return '';
  });

  return unresolved ? null : url;
}

/** Esegue le query e raccoglie i valori. Un fallimento singolo non ferma gli altri. */
export function resolveParams(run: SqlRunner): Record<string, string> {
  const params: Record<string, string> = { ...STATIC_PARAMS };

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
