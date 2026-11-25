/**
 * useSelfPlayer — il giocatore «io» dei wizard di creazione sessione (#4750).
 */
import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { linkedFirst, ownUserId, selfDisplayName, useSelfPlayer } from '../useSelfPlayer';

type TestUser = { id: string; email: string; displayName?: string | null; role: string };
const currentUser = vi.hoisted(() => ({ value: undefined as TestUser | undefined }));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: currentUser.value }),
}));

describe('selfDisplayName', () => {
  it('usa il nome del profilo, senza spazi ai bordi', () => {
    expect(selfDisplayName('  Marco Rossi ')).toBe('Marco Rossi');
  });

  it('senza nome del profilo usa il nome neutro dei giocatori, non l’email', () => {
    // Il nome va a tutti i partecipanti: dall'email non si deve ricavare niente.
    expect(selfDisplayName(null)).toBe('Giocatore 1');
    expect(selfDisplayName(undefined)).toBe('Giocatore 1');
    expect(selfDisplayName('   ')).toBe('Giocatore 1');
  });

  // #5150: il nome del profilo arriva fino a 128 caratteri, un posto al tavolo ne prende 50. Seminato
  // intero, il server rifiutava il creatore all'aggiunta (prima: la partita non si avviava).
  it('taglia il nome del profilo al limite di un giocatore', () => {
    const name = selfDisplayName(`${'a'.repeat(49)} ${'b'.repeat(78)}`);

    expect(name).toBe('a'.repeat(49));
    expect(selfDisplayName('c'.repeat(128))).toHaveLength(50);
  });

  it('non lascia a metà un carattere fuori dal piano base', () => {
    // 49 lettere e un'emoji: l'emoji occupa due unità, e la seconda cadrebbe oltre il limite.
    expect(selfDisplayName(`${'a'.repeat(49)}🎲${'b'.repeat(10)}`)).toBe('a'.repeat(49));
  });
});

describe('ownUserId', () => {
  const self = { userId: 'user-marco', displayName: 'Marco Rossi' };

  it('dà il collegamento di un posto solo se è dell’utente corrente', () => {
    expect(ownUserId('user-marco', self)).toBe('user-marco');
  });

  it('un posto collegato a un altro utente non manda nessun collegamento', () => {
    // L'utente può cambiare dopo che il posto è stato seminato (un altro account dopo un refetch):
    // il posto non è più suo, e non deve partire con lo userId di prima.
    expect(ownUserId('user-anna', self)).toBeUndefined();
  });

  it('un posto senza collegamento, o senza utente noto, non ne manda', () => {
    expect(ownUserId(undefined, self)).toBeUndefined();
    expect(ownUserId('user-marco', null)).toBeUndefined();
  });
});

describe('linkedFirst', () => {
  it('mette per primo chi è collegato e tiene l’ordine degli altri', () => {
    const players = [{ n: 'a' }, { n: 'b' }, { n: 'me', userId: 'u1' }, { n: 'c' }];

    expect(linkedFirst(players).map(p => p.n)).toEqual(['me', 'a', 'b', 'c']);
  });

  it('non cambia nulla se nessuno è collegato, e non muta l’elenco ricevuto', () => {
    const players = [{ n: 'a' }, { n: 'b' }];

    expect(linkedFirst(players)).toEqual(players);
    expect(linkedFirst(players)).not.toBe(players);
  });
});

describe('useSelfPlayer', () => {
  beforeEach(() => {
    currentUser.value = undefined;
  });

  it('è null finché l’utente non è noto', () => {
    const { result } = renderHook(() => useSelfPlayer());

    expect(result.current).toBeNull();
  });

  it('con l’utente noto dà il suo id e il nome del profilo', () => {
    currentUser.value = {
      id: 'user-marco',
      email: 'marco@example.com',
      displayName: 'Marco Rossi',
      role: 'User',
    };

    const { result } = renderHook(() => useSelfPlayer());

    expect(result.current).toEqual({ userId: 'user-marco', displayName: 'Marco Rossi' });
  });

  it('tiene lo stesso oggetto finché l’utente non cambia, così gli effetti non ripartono', () => {
    currentUser.value = { id: 'user-marco', email: 'marco@example.com', role: 'User' };

    const { result, rerender } = renderHook(() => useSelfPlayer());
    const first = result.current;
    rerender();

    expect(result.current).toBe(first);
    expect(first).toEqual({ userId: 'user-marco', displayName: 'Giocatore 1' });
  });
});
