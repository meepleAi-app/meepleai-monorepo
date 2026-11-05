/**
 * player-names — i nomi dei giocatori nei wizard di creazione sessione (#4750).
 */
import { describe, expect, it } from 'vitest';

import {
  duplicatePlayerNameMessage,
  findDuplicatePlayerName,
  nextDefaultPlayerName,
} from '../player-names';

describe('findDuplicatePlayerName', () => {
  it('è null se i nomi sono tutti diversi', () => {
    expect(findDuplicatePlayerName(['Marco', 'Anna', 'Luca'])).toBeNull();
    expect(findDuplicatePlayerName([])).toBeNull();
  });

  it('trova un nome ripetuto senza badare alle maiuscole né agli spazi ai bordi', () => {
    expect(findDuplicatePlayerName(['Marco Rossi', 'Anna', ' marco rossi '])).toBe('marco rossi');
  });

  it('dice il nome come l’ha scritto l’utente la seconda volta', () => {
    expect(findDuplicatePlayerName(['Anna', 'ANNA'])).toBe('ANNA');
  });

  it('non conta i nomi vuoti: due posti vuoti non sono due omonimi', () => {
    expect(findDuplicatePlayerName(['', '  ', 'Anna'])).toBeNull();
  });

  it('non confonde nomi diversi che si assomigliano', () => {
    expect(findDuplicatePlayerName(['Marco', 'Marco R.'])).toBeNull();
  });
});

describe('duplicatePlayerNameMessage', () => {
  it('nomina il giocatore e dice cosa fare', () => {
    expect(duplicatePlayerNameMessage('Anna')).toBe(
      'Due giocatori si chiamano «Anna»: cambia o togli uno dei due.'
    );
  });
});

describe('nextDefaultPlayerName', () => {
  it('parte da quanti giocatori ci sono già', () => {
    expect(nextDefaultPlayerName([])).toBe('Giocatore 1');
    expect(nextDefaultPlayerName(['Marco Rossi'])).toBe('Giocatore 2');
    expect(nextDefaultPlayerName(['Giocatore 1', 'Bob'])).toBe('Giocatore 3');
  });

  it('salta un numero già preso, per esempio dopo aver tolto un giocatore', () => {
    expect(nextDefaultPlayerName(['Giocatore 2'])).toBe('Giocatore 3');
    expect(nextDefaultPlayerName(['Giocatore 2', 'Giocatore 3'])).toBe('Giocatore 4');
  });

  it('non bada alle maiuscole né agli spazi ai bordi di quelli già presi', () => {
    expect(nextDefaultPlayerName([' giocatore 2 '])).toBe('Giocatore 3');
  });
});
