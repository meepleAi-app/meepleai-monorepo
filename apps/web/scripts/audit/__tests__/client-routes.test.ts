/**
 * Confronto fra le chiamate dei client e le rotte del backend, verbo compreso.
 *
 * #4752: «Aggiungi partita» faceva `POST /api/v1/games/{id}/sessions`, e il backend registra su quel
 * percorso solo il `GET`: 405 a ogni creazione, e nessun confronto per percorso lo vede, perché il
 * percorso esiste. #3954: le chiamate a rotte che non esistono affatto.
 */

import { describe, expect, it } from 'vitest';

import {
  compareCallsToEndpoints,
  extractClientCalls,
  mismatchKey,
  type ClientCall,
} from '../client-routes';
import type { EndpointEntry } from '../types';

const endpoint = (method: string, path: string): EndpointEntry => ({
  method,
  path,
  auth: 'authenticated',
  tags: [],
  file: 'Routing/X.cs',
  line: 1,
});

const call = (method: string, path: string): ClientCall => ({
  method,
  path,
  file: 'src/lib/api/x.ts',
  line: 1,
});

describe('extractClientCalls', () => {
  it('legge verbo e percorso di una chiamata con tipo generico, normalizzando i parametri', () => {
    const source = [
      'const r = await httpClient.post<CreateSessionResult>(',
      '  `/api/v1/games/${encodeURIComponent(gameId)}/sessions`,',
      '  request',
      ');',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts')).toEqual([
      { method: 'POST', path: '/api/v1/games/{}/sessions', file: 'x.ts', line: 1 },
    ]);
  });

  it('risolve le costanti di base dichiarate nel file', () => {
    const source = [
      "const GAMES_BASE = '/api/v1/games';",
      'const SESSIONS = `${GAMES_BASE}/sessions`;',
      'await httpClient.get(`${GAMES_BASE}/${id}/kb-readiness`);',
      'await apiClient.delete(`${SESSIONS}/${sid}`);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => `${c.method} ${c.path}`)).toEqual([
      'GET /api/v1/games/{}/kb-readiness',
      'DELETE /api/v1/games/sessions/{}',
    ]);
  });

  it('toglie la query string e il suffisso interpolato attaccato all’ultimo segmento', () => {
    const source = [
      'await httpClient.get(`/api/v1/sessions/${id}/diary${qs}`);',
      "await httpClient.get('/api/v1/library?page=1');",
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual([
      '/api/v1/sessions/{}/diary',
      '/api/v1/library',
    ]);
  });

  it('una query string condizionale, che annida un template, non lascia residui nel percorso', () => {
    const source =
      "await httpClient.get(`/api/v1/prompts/${id}/audit-log${query ? `?${query}` : ''}`);";

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual([
      '/api/v1/prompts/{}/audit-log',
    ]);
  });

  // #4752 (revisione): la chiamata riscritta dalla PR era `httpClient.post(GAME_SESSION_BASE, body)`, con la
  // costante come argomento nudo: l'estrattore la risolveva solo dentro `${…}`, quindi il gate non la vedeva.
  it('risolve una costante di file passata come argomento nudo', () => {
    const source = [
      "const SESSIONS = '/api/v1/games/x/sessions';",
      'await httpClient.post(SESSIONS, body);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => `${c.method} ${c.path}`)).toEqual([
      'POST /api/v1/games/x/sessions',
    ]);
  });

  it('risolve una costante-template di file, con i parametri a {}', () => {
    const source = [
      'const url = `/api/v1/games/${id}/sessions`;',
      'await httpClient.post(url, body);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => `${c.method} ${c.path}`)).toEqual([
      'POST /api/v1/games/{}/sessions',
    ]);
  });

  it('una costante può comporsi con altre costanti, anche dichiarate dopo', () => {
    const source = [
      'const LIST = `${BASE}/list`;',
      "const BASE = '/api/v1/things';",
      'await httpClient.get(LIST);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual(['/api/v1/things/list']);
  });

  // #4752 (revisione): `const url = …` dichiarato in ogni metodo di un client (es.
  // gamesClient.ts) rendeva ambigua la costante e invisibili le chiamate. Una costante locale vale
  // dentro il blocco che la dichiara: la dichiarazione più vicina, ancora aperta alla chiamata, è la sua.
  it('una costante locale si risolve con la dichiarazione del blocco che contiene la chiamata', () => {
    const source = [
      "function a() { const URL = '/api/v1/a'; return httpClient.get(URL); }",
      "function b() { const URL = '/api/v1/b'; return httpClient.get(URL); }",
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual(['/api/v1/a', '/api/v1/b']);
  });

  it('una costante-template locale si risolve anche dentro una funzione freccia', () => {
    const source = [
      'export const startGame = async (id: string) => {',
      '  const url = `/api/v1/games/${id}/sessions`;',
      '  return httpClient.post(url, {});',
      '};',
      'export const listGames = async () => {',
      "  const url = '/api/v1/games';",
      '  return httpClient.get(url);',
      '};',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => `${c.method} ${c.path}`)).toEqual([
      'POST /api/v1/games/{}/sessions',
      'GET /api/v1/games',
    ]);
  });

  it('una dichiarazione in un blocco già chiuso non vale per le chiamate che seguono', () => {
    const source = [
      "function a() { const URL = '/api/v1/a'; return 1; }",
      'function b(URL: string) { return httpClient.get(URL); }',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts')).toEqual([]);
  });

  it('fra due dichiarazioni locali ancora aperte vince la più vicina alla chiamata', () => {
    const source = [
      'function f(flag: boolean) {',
      "  const URL = '/api/v1/outer';",
      '  if (flag) {',
      "    const URL = '/api/v1/inner';",
      '    return httpClient.get(URL);',
      '  }',
      '  return httpClient.get(URL);',
      '}',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual([
      '/api/v1/inner',
      '/api/v1/outer',
    ]);
  });

  it('una costante locale ha la precedenza su una di file con lo stesso nome', () => {
    const source = [
      "const URL = '/api/v1/top';",
      "function f() { const URL = '/api/v1/local'; return httpClient.get(URL); }",
      'function g() { return httpClient.get(URL); }',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual([
      '/api/v1/local',
      '/api/v1/top',
    ]);
  });

  it('una costante di file dichiarata più sotto vale alla chiamata (la funzione gira dopo)', () => {
    const source = [
      'export async function load() { return httpClient.get(BASE); }',
      "const BASE = '/api/v1/late';",
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual(['/api/v1/late']);
  });

  it('due costanti di file con lo stesso nome e valori diversi sono ambigue: la chiamata non si legge', () => {
    const source = [
      "const URL = '/api/v1/a';",
      "const URL = '/api/v1/b';",
      'await httpClient.get(URL);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts')).toEqual([]);
  });

  // #4752 (revisione): `http.post('/api/v1/admin/email-templates/' + id + '/publish')` si leggeva come
  // `POST /api/v1/admin/email-templates` e passava per coincidenza: il letterale non è l'intero argomento.
  it('un letterale seguito da + non è il percorso: la chiamata non si legge', () => {
    const source = [
      "await http.post('/api/v1/admin/email-templates/' + id + '/publish');",
      'await http.get(`/api/v1/things/${id}` + suffix);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts')).toEqual([]);
  });

  it('un letterale seguito da virgola o parentesi è il primo argomento', () => {
    const source = [
      "await http.get('/api/v1/a');",
      "await http.post('/api/v1/b', body);",
      "await http.put(\n  '/api/v1/c',\n  body\n);",
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual([
      '/api/v1/a',
      '/api/v1/b',
      '/api/v1/c',
    ]);
  });

  it('un identificatore che non è una costante di file non si indovina', () => {
    const source = ['function f(url: string) {', '  return httpClient.get(url);', '}'].join('\n');
    expect(extractClientCalls(source, 'x.ts')).toEqual([]);
  });

  it('gli esempi nei commenti non sono chiamate', () => {
    const source = [
      '/**',
      " * Esempio: httpClient.post('/api/v1/games/{id}/sessions', body)",
      ' */',
      "// httpClient.delete('/api/v1/old')",
      "await httpClient.get('/api/v1/real');",
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts').map(c => `${c.method} ${c.path}`)).toEqual([
      'GET /api/v1/real',
    ]);
  });

  it('un // dentro una stringa non apre un commento', () => {
    const source = "const base = 'http://x'; await httpClient.get('/api/v1/after');";
    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual(['/api/v1/after']);
  });

  it('un ternario dentro l’interpolazione non taglia il percorso al ?', () => {
    const source = "await httpClient.get(`/api/v1/a/${flag ? 'x' : 'y'}/b`);";
    expect(extractClientCalls(source, 'x.ts').map(c => c.path)).toEqual(['/api/v1/a/{}/b']);
  });

  it('ignora ciò che non è una chiamata a un percorso dell’API', () => {
    const source = [
      "params.get('page');",
      "cache.delete('key');",
      'await httpClient.get(url);',
      'await httpClient.get(`${endpoints.users}/${id}`);',
    ].join('\n');

    expect(extractClientCalls(source, 'x.ts')).toEqual([]);
  });

  it('riporta la riga della chiamata', () => {
    const source = ['// intestazione', '', 'await httpClient.put(`/api/v1/a/${id}`, body);'].join(
      '\n'
    );
    expect(extractClientCalls(source, 'x.ts')[0].line).toBe(3);
  });
});

describe('compareCallsToEndpoints', () => {
  const endpoints = [
    endpoint('GET', '/api/v1/games/{id:guid}/sessions'),
    endpoint('GET', '/api/v1/games/{gameId}/sessions/active'),
    endpoint('POST', '/api/v1/game-sessions'),
    endpoint('POST', '/api/v1/game-nights/{id:guid}/sessions'),
  ];

  it('una chiamata che combacia per verbo e percorso non è un rilievo', () => {
    const calls = [
      call('GET', '/api/v1/games/{}/sessions'),
      call('POST', '/api/v1/game-sessions'),
      call('POST', '/api/v1/game-nights/{}/sessions'),
    ];
    expect(compareCallsToEndpoints(calls, endpoints)).toEqual([]);
  });

  it('POST su un percorso con solo GET registrato è un verbo sbagliato (il caso di #4752)', () => {
    const [mismatch] = compareCallsToEndpoints(
      [call('POST', '/api/v1/games/{}/sessions')],
      endpoints
    );

    expect(mismatch).toMatchObject({
      kind: 'wrong-verb',
      method: 'POST',
      path: '/api/v1/games/{}/sessions',
      registeredMethods: ['GET'],
    });
  });

  it('un percorso che il backend non registra con nessun verbo è un percorso sconosciuto (#3954)', () => {
    const [mismatch] = compareCallsToEndpoints(
      [call('GET', '/api/v1/badges/my-badges')],
      endpoints
    );

    expect(mismatch).toMatchObject({ kind: 'unknown-path', method: 'GET' });
  });

  it('un parametro vale per qualunque segmento, con o senza vincolo di tipo', () => {
    const calls = [call('GET', '/api/v1/games/{}/sessions/active')];
    expect(compareCallsToEndpoints(calls, endpoints)).toEqual([]);
  });

  it('il numero di segmenti conta: un prefisso del percorso registrato non combacia', () => {
    const [mismatch] = compareCallsToEndpoints([call('GET', '/api/v1/games/{}')], endpoints);
    expect(mismatch.kind).toBe('unknown-path');
  });

  it('il verbo sbagliato si riconosce anche sul percorso più lungo', () => {
    const [mismatch] = compareCallsToEndpoints(
      [call('POST', '/api/v1/games/{}/sessions/active')],
      endpoints
    );
    expect(mismatch.kind).toBe('wrong-verb');
    expect(mismatch.registeredMethods).toEqual(['GET']);
  });

  it('un segmento di coda catch-all del backend assorbe il resto del percorso', () => {
    const catchAll = [endpoint('GET', '/api/v1/files/{**path}')];
    expect(compareCallsToEndpoints([call('GET', '/api/v1/files/a/b/c')], catchAll)).toEqual([]);
  });

  // #4752 (revisione): le rotte registrate da un metodo che riceve un sottogruppo già prefissato
  // (`DocumentCollectionEndpoints`, `RagExecutionAdminEndpoints`) perdono il prefisso e diventano
  // `/api/v1/{id}/replay`: un jolly che assorbiva `POST /api/v1/qualunque-cosa/replay`.
  it('una rotta con un parametro come primo segmento (prefisso perso) non assorbe chiamate', () => {
    const phantom = [endpoint('POST', '/api/v1/{executionId:guid}/replay')];
    const [mismatch] = compareCallsToEndpoints(
      [call('POST', '/api/v1/nonexistent-thing/replay')],
      phantom
    );
    expect(mismatch).toMatchObject({ kind: 'unknown-path' });
  });

  it('una chiamata ripetuta dà un solo rilievo, con la prima posizione', () => {
    const calls = [
      { ...call('POST', '/api/v1/games/{}/sessions'), line: 10 },
      { ...call('POST', '/api/v1/games/{}/sessions'), line: 90 },
    ];
    const result = compareCallsToEndpoints(calls, endpoints);
    expect(result).toHaveLength(1);
    expect(result[0].line).toBe(10);
  });

  it('la chiave del rilievo non dipende da file e riga', () => {
    const [mismatch] = compareCallsToEndpoints(
      [call('POST', '/api/v1/games/{}/sessions')],
      endpoints
    );
    expect(mismatchKey(mismatch)).toBe('POST /api/v1/games/{}/sessions');
  });
});
