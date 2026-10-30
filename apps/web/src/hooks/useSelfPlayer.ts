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

/** Il nome del profilo; per chi non l'ha, la parte locale dell'email. */
export function selfDisplayName(displayName?: string | null, email?: string | null): string {
  return displayName?.trim() || email?.split('@')[0]?.trim() || 'Io';
}

/** `null` finché l'utente non è noto: il wizard resta com'era, senza collegare nessuno. */
export function useSelfPlayer(): SelfPlayer | null {
  const { data: user } = useCurrentUser();
  const id = user?.id;
  const displayName = user?.displayName;
  const email = user?.email;

  return useMemo(
    () => (id ? { userId: id, displayName: selfDisplayName(displayName, email) } : null),
    [id, displayName, email]
  );
}

/**
 * Il giocatore collegato va aggiunto per primo: `LiveGameSession.AddPlayer` dà il ruolo Host a
 * chi entra per primo, e il riepilogo e il toolkit leggono quel ruolo per indicare il
 * proprietario. L'ordine dei turni è un'altra cosa, e lo fissa `updateTurnOrder`.
 */
export function linkedFirst<T extends { userId?: string }>(players: readonly T[]): T[] {
  return [...players.filter(p => p.userId), ...players.filter(p => !p.userId)];
}
