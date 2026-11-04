/**
 * #4107 — il contratto dei colori giocatore, verificato contro il backend.
 *
 * Il difetto che questi test impediscono di ripetere non era un bug di logica: era un
 * **disallineamento di vocabolario** fra due file che nessuno confrontava. Il frontend offriva
 * dieci colori, il dominio ne conosceva otto, e `{"color":"White"}` riceveva `400`. Nessun
 * test, di nessuno dei due lati, poteva accorgersene — perché nessuno guardava entrambi.
 *
 * Per questo il primo test **legge l'enum C#**. È inusuale, e è il punto: un contratto fra due
 * linguaggi si verifica solo da qualcosa che vede entrambi.
 */

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, it, expect } from 'vitest';

import { PlayerColorSchema } from '@/lib/api/schemas/live-sessions.schemas';
import {
  API_PLAYER_COLORS,
  PLAYER_COLOR_PALETTE,
  hexFromPlayerColor,
  nextFreePlayerColor,
  playerColorFromHex,
} from '@/lib/sessions/player-colors';

/** I nomi dei membri di `PlayerColor`, letti dal sorgente del dominio. */
function readBackendPlayerColors(): string[] {
  // I test girano con cwd = apps/web.
  const source = readFileSync(
    resolve(
      process.cwd(),
      '../api/src/Api/BoundedContexts/GameManagement/Domain/Enums/PlayerColor.cs'
    ),
    'utf8'
  );
  // `Nome = n,` — i commenti XML e quelli `//` non corrispondono.
  return [...source.matchAll(/^\s{4}([A-Z][A-Za-z]*)\s*=\s*\d+\s*,?\s*$/gm)].map(m => m[1]);
}

describe('contratto dei colori giocatore fra frontend e dominio', () => {
  it('il vocabolario del frontend e quello del dominio sono lo stesso insieme', () => {
    const backend = readBackendPlayerColors();

    // Sanità della lettura: se la regex smettesse di agganciare, l'array sarebbe vuoto e il
    // confronto passerebbe contro il nulla. È il modo in cui un gate diventa verde e cieco.
    expect(backend.length).toBeGreaterThanOrEqual(8);

    // Insiemi, non sequenze: l'ordine numerico dell'enum C# non è il contratto (la colonna è
    // `character varying`, la conversione JSON è per nome). Asserire l'ordine renderebbe questo
    // test rosso per una riorganizzazione innocua — ed è il motivo per cui la prima stesura
    // falliva: il frontend elenca White/Black prima di Pink/Teal, il dominio dopo.
    expect([...PlayerColorSchema.options].sort()).toEqual([...backend].sort());
  });

  it('la palette copre esattamente i colori dell API, senza duplicati', () => {
    expect([...API_PLAYER_COLORS].sort()).toEqual([...PlayerColorSchema.options].sort());
    expect(new Set(PLAYER_COLOR_PALETTE.map(e => e.hex)).size).toBe(PLAYER_COLOR_PALETTE.length);
  });
});

describe('nextFreePlayerColor', () => {
  it('restituisce il primo colore libero, non il primo in assoluto', () => {
    expect(nextFreePlayerColor(['Red', 'Blue'])).toBe('Green');
  });

  it('ignora i buchi: un colore liberato torna disponibile', () => {
    expect(nextFreePlayerColor(['Blue', 'Green'])).toBe('Red');
  });

  it('tollera gli undefined in lista', () => {
    expect(nextFreePlayerColor(['Red', undefined, 'Green'])).toBe('Blue');
  });

  it('restituisce undefined quando sono tutti occupati, invece di riciclarne uno', () => {
    // È il comportamento che evita il difetto di #4107: riciclare produrrebbe la collisione
    // «Color X is already taken by another player» con un messaggio che non spiega nulla.
    expect(nextFreePlayerColor(API_PLAYER_COLORS)).toBeUndefined();
  });
});

describe('playerColorFromHex', () => {
  it('traduce gli hex della palette, in qualunque cassa', () => {
    expect(playerColorFromHex('#ef4444')).toBe('Red');
    expect(playerColorFromHex('#EF4444')).toBe('Red');
    expect(playerColorFromHex('  #14b8a6  ')).toBe('Teal');
  });

  it('non inventa un fallback per un hex sconosciuto', () => {
    // Restituire `Red` qui sarebbe il difetto di #4107 per un'altra strada.
    expect(playerColorFromHex('#123456')).toBeUndefined();
  });

  it('è l inverso di hexFromPlayerColor su tutti i colori', () => {
    for (const color of API_PLAYER_COLORS) {
      const hex = hexFromPlayerColor(color);
      expect(hex, `nessun hex per ${color}`).toBeDefined();
      expect(playerColorFromHex(hex as string)).toBe(color);
    }
  });
});
