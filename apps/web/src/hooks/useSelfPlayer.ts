/**
 * Il giocatore «io» dei wizard di creazione sessione (#4750).
 *
 * Chi crea una partita di solito ci gioca. I wizard scrivevano ogni giocatore per nome, senza
 * `userId`: la partita non risultava giocata da nessun account, e restava fuori da storico,
 * statistiche e classifiche, che leggono il giocatore collegato (`RecordPlayer.UserId`).
 * Ora il creatore è il primo giocatore, collegato al suo account; chi fa solo il segnapunti lo
 * toglie con «Io non gioco».
 */

import { useMemo } from 'react';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

export interface SelfPlayer {
  userId: string;
  displayName: string;
}

/**
 * Il nome del profilo; per chi non l'ha, il nome neutro che i wizard davano al primo giocatore.
 * Il nome va a tutti i partecipanti della partita: dall'email non si ricava niente.
 */
export function selfDisplayName(displayName?: string | null): string {
  return displayName?.trim() || 'Giocatore 1';
}

/** `null` finché l'utente non è noto: il wizard resta com'era, senza collegare nessuno. */
export function useSelfPlayer(): SelfPlayer | null {
  const { data: user } = useCurrentUser();
  const id = user?.id;
  const displayName = user?.displayName;

  return useMemo(
    () => (id ? { userId: id, displayName: selfDisplayName(displayName) } : null),
    [id, displayName]
  );
}

/**
 * Il collegamento che un posto può mandare al server: solo se è dell'utente corrente. L'utente
 * può cambiare dopo che il posto è stato seminato (un altro account dopo un refetch): il posto
 * non è più suo, perde la targhetta e «Io non gioco», e non deve partire con lo userId di prima.
 */
export function ownUserId(userId: string | undefined, self: SelfPlayer | null): string | undefined {
  return self && userId === self.userId ? userId : undefined;
}

/**
 * Il giocatore collegato va aggiunto per primo: `LiveGameSession.AddPlayer` dà il ruolo Host a
 * chi entra per primo, e il riepilogo e il toolkit leggono quel ruolo per indicare il
 * proprietario. L'ordine dei turni è un'altra cosa, e lo fissa `updateTurnOrder`.
 */
export function linkedFirst<T extends { userId?: string }>(players: readonly T[]): T[] {
  return [...players.filter(p => p.userId), ...players.filter(p => !p.userId)];
}
