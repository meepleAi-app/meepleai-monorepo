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
