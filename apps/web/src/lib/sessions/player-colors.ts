/**
 * Il vocabolario dei colori giocatore, e le due traduzioni che servono (#4107).
 *
 * ## Perché questo file esiste
 *
 * Nel frontend convivevano **due** nozioni di «colore giocatore», e i chiamanti le
 * confondevano:
 *
 *   - la **palette UI** (`PlayerSetup.PLAYER_COLORS`), fatta di hex come `#ef4444`, usata per
 *     disegnare i pallini;
 *   - il **vocabolario dell'API** (`PlayerColorSchema`), fatto di nomi come `'Red'`, che è ciò
 *     che `POST /live-sessions/{id}/players` accetta.
 *
 * Misurato contro il backend: `{"color":"Blue"}` → **201**, `{"color":"#ef4444"}` → **400**,
 * `{"color":1}` → **400**. Quindi passare un hex all'API è sempre stato un errore, e
 * `PrivateGameHub` lo faceva.
 *
 * Peggio: `color` è `.optional()` nello schema, ma **ometterlo non significa «scegline uno»** —
 * il binder del backend lega `0`, cioè `Red`, a ogni giocatore. Da lì il difetto di #4107: la
 * pagina toolkit ometteva il colore, e dal secondo giocatore il dominio rifiutava con «Color
 * Red is already taken by another player».
 *
 * Questo modulo tiene **una** implementazione di «il primo colore libero» (prima ce n'erano due
 * copie, in `AddPlayerDialog` e in `PlayerSetup`, con vocabolari diversi) e la traduzione
 * hex → nome, così il confine fra le due nozioni è un posto solo.
 */

import { type PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

/**
 * La palette: un colore dell'API e l'hex con cui disegnarlo, **appaiati**.
 *
 * L'ordine di questa lista è l'ordine in cui i colori vengono assegnati, e combacia con quello
 * che `AddPlayerDialog` già usava (`PlayerColorSchema.options`), quindi il comportamento
 * esistente non cambia.
 *
 * ⚠️ Appaiati, non allineati per indice su due array paralleli: due liste separate si
 * disallineano in silenzio, e un `PLAYER_COLORS[i]` contro un `API_COLORS[i]` tradurrebbe
 * «Rosso» in «Blue» senza che nulla fallisca. L'ordine numerico dell'enum C# **non** è il
 * contratto — la colonna `session_players.color` è `character varying` e la conversione JSON è
 * per nome — quindi qui si verifica che i due insiemi coincidano, non che coincidano gli indici.
 */
export const PLAYER_COLOR_PALETTE: readonly { color: PlayerColor; hex: string }[] = [
  { color: 'Red', hex: '#ef4444' },
  { color: 'Blue', hex: '#3b82f6' },
  { color: 'Green', hex: '#22c55e' },
  { color: 'Yellow', hex: '#eab308' },
  { color: 'Purple', hex: '#a855f7' },
  { color: 'Orange', hex: '#f97316' },
  { color: 'White', hex: '#ffffff' },
  { color: 'Black', hex: '#1f2937' },
  { color: 'Pink', hex: '#ec4899' },
  { color: 'Teal', hex: '#14b8a6' },
];

/** I colori che l'API accetta, nell'ordine in cui vanno assegnati. */
export const API_PLAYER_COLORS: readonly PlayerColor[] = PLAYER_COLOR_PALETTE.map(e => e.color);

/**
 * Il primo colore che l'API accetta e che nessuno ha ancora preso.
 *
 * @returns `undefined` quando sono tutti occupati — il chiamante deve dirlo all'utente, non
 *   mandare un colore qualunque: il dominio lo rifiuterebbe con un 400 che parla di colori e
 *   non di «sessione piena».
 */
export function nextFreePlayerColor(
  taken: readonly (PlayerColor | undefined)[]
): PlayerColor | undefined {
  const used = new Set(taken.filter((c): c is PlayerColor => c != null));
  return API_PLAYER_COLORS.find(c => !used.has(c));
}

/**
 * Traduce un hex della palette UI nel nome che l'API accetta.
 *
 * Il confronto ignora la cassa: gli hex girano nel codice sia `#EF4444` sia `#ef4444`.
 *
 * @returns `undefined` se l'hex non è nella palette — e in quel caso **non** si inventa un
 *   fallback: mandare `Red` per un colore sconosciuto reintrodurrebbe la collisione di #4107.
 */
export function playerColorFromHex(hex: string): PlayerColor | undefined {
  const needle = hex.trim().toLowerCase();
  return PLAYER_COLOR_PALETTE.find(e => e.hex.toLowerCase() === needle)?.color;
}

/** L'hex con cui disegnare un colore dell'API. */
export function hexFromPlayerColor(color: PlayerColor): string | undefined {
  return PLAYER_COLOR_PALETTE.find(e => e.color === color)?.hex;
}
