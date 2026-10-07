/**
 * Da `LiveSessionDto` (API) a `Session` (UI) — #4113.
 *
 * ## Perché questa funzione esiste
 *
 * La pagina della sessione toolkit passava il DTO direttamente al componente:
 *
 * ```tsx
 * session={activeSession as unknown as Session}
 * ```
 *
 * `as unknown as` fa accettare al compilatore **qualunque** cosa, e le due forme non
 * combaciano in **quattro** punti. A runtime `session.sessionDate` era `undefined`, e
 * `SessionHeader` chiamava `.toLocaleDateString()` su quello: la pagina mostrava l'error
 * boundary ogni volta, per qualunque sessione.
 *
 * | `Session` (UI) | `LiveSessionDto` (API) |
 * |---|---|
 * | `sessionDate: Date` | assente — ci sono `createdAt` e `startedAt`, stringhe ISO |
 * | `sessionType` | assente |
 * | `participantCount` | assente — c'è `players[]` |
 * | `status: 'Active' \| 'Paused' \| 'Finalized'` | `LiveSessionStatus`, vocabolario diverso |
 *
 * Una mappatura esplicita costa cinque righe e le rende tutte e quattro visibili al
 * compilatore: se un campo di `Session` cambia, questo file non compila più. È l'opposto di
 * ciò che faceva il cast.
 */

import type { Session } from '@/components/session/types';
import type { LiveSessionDto, LiveSessionStatus } from '@/lib/api/schemas/live-sessions.schemas';

/**
 * Traduce lo stato del dominio nei tre stati che la UI sa colorare.
 *
 * `SessionHeader` indicizza `statusColors[session.status]` con una mappa di tre chiavi:
 * passare `Created` o `InProgress` dava `undefined`, cioè nessuno stile. Il vocabolario UI è
 * più grossolano di quello del dominio, e va **tradotto**, non inoltrato.
 */
export function uiSessionStatus(status: LiveSessionStatus): Session['status'] {
  switch (status) {
    case 'Paused':
      return 'Paused';
    case 'Completed':
      return 'Finalized';
    case 'Created':
    case 'Setup':
    case 'InProgress':
      return 'Active';
    default: {
      // Esaustività verificata dal compilatore: un nuovo stato nel dominio fa fallire qui,
      // invece di arrivare alla UI come una chiave che non colora niente.
      const exhaustive: never = status;
      return exhaustive;
    }
  }
}

/** La forma che i componenti `components/session/*` si aspettano. */
export function liveSessionToUiSession(dto: LiveSessionDto): Session {
  return {
    id: dto.id,
    sessionCode: dto.sessionCode,
    // Una sessione è «specifica di un gioco» quando è legata al catalogo.
    sessionType: dto.gameId != null ? 'GameSpecific' : 'Generic',
    gameId: dto.gameId,
    gameName: dto.gameName,
    // `startedAt` è la data che interessa a chi guarda l'intestazione; prima dell'avvio
    // resta `null`, e allora si mostra quando la sessione è stata creata.
    sessionDate: new Date(dto.startedAt ?? dto.createdAt),
    status: uiSessionStatus(dto.status),
    // I giocatori rimossi restano nella lista con `isActive: false` (#2561): contarli
    // mostrerebbe più partecipanti di quanti stanno giocando.
    participantCount: dto.players.filter(p => p.isActive).length,
  };
}
