/**
 * Unit test per il collettore di evidenze — ondata 0 dell'audit esaustivo.
 *
 * Il collettore risponde a due domande attorno a ogni azione: il backend ha
 * loggato errori? il database è cambiato, e dove? Le funzioni pure qui sotto
 * sono testabili senza Docker; l'I/O vive in `snapshotCounts` e `readErrorLogs`.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import { diffCounts, filterErrorLines, parseTableCounts } from '../collect-evidence';

describe('parseTableCounts', () => {
  it("legge l'output tabellare di psql -t -A", () => {
    expect(parseTableCounts('games|42\nusers|7\n')).toEqual({ games: 42, users: 7 });
  });

  it('ignora le righe vuote', () => {
    expect(parseTableCounts('games|42\n\n')).toEqual({ games: 42 });
  });
});

describe('diffCounts', () => {
  it('riporta solo le tabelle cambiate, ordinate per entità del cambiamento', () => {
    expect(
      diffCounts({ games: 1, sessions: 5, users: 3 }, { games: 2, sessions: 9, users: 3 })
    ).toEqual([
      { table: 'sessions', delta: 4 },
      { table: 'games', delta: 1 },
    ]);
  });

  it("riporta le tabelle comparse dopo l'azione", () => {
    expect(diffCounts({}, { outbox: 2 })).toEqual([{ table: 'outbox', delta: 2 }]);
  });

  it('riporta le cancellazioni come delta negativo', () => {
    expect(diffCounts({ games: 5 }, { games: 3 })).toEqual([{ table: 'games', delta: -2 }]);
  });

  it('restituisce lista vuota quando nulla è cambiato', () => {
    expect(diffCounts({ games: 5 }, { games: 5 })).toEqual([]);
  });
});

describe('filterErrorLines', () => {
  it('tiene solo Error e Fatal', () => {
    const log = [
      '[12:00:00 INF] richiesta servita',
      '[12:00:01 ERR] NullReferenceException in GameHandler',
      '[12:00:02 WRN] cache miss',
      '[12:00:03 FTL] host terminato',
    ].join('\n');

    expect(filterErrorLines(log)).toEqual([
      '[12:00:01 ERR] NullReferenceException in GameHandler',
      '[12:00:03 FTL] host terminato',
    ]);
  });

  it('riconosce anche il formato con livello esteso', () => {
    expect(filterErrorLines('level=Error msg="boom"')).toEqual(['level=Error msg="boom"']);
  });

  it('restituisce lista vuota su log pulito', () => {
    expect(filterErrorLines('[12:00:00 INF] tutto bene')).toEqual([]);
  });

  it('non scambia per errore una riga che contiene la parola in altro contesto', () => {
    // 'ERR' dentro un identificatore non è un livello di log: contarlo
    // riempirebbe il report di falsi positivi e renderebbe inutile il segnale.
    expect(filterErrorLines('[12:00:00 INF] caricato modulo ERRORHANDLING_V2')).toEqual([]);
  });
});
