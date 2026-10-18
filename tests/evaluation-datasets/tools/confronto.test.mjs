// #4482: il confronto fra citazione verificata e chunk recuperati. Si lancia con
//   node --test tests/evaluation-datasets/tools/
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { cercaCitazione, frammenti, linguaDelTesto, normalizza, SOGLIA_PREDEFINITA } from './confronto.mjs';

const CITAZIONE_CATAN = 'During your turn, other players may only trade with you, not with each other or with the supply.';

test('normalizza ricompone la parola spezzata a fine riga, toglie accenti e punteggiatura', () => {
  assert.equal(normalizza('Re-\nsource  Cards, più­tardi: l’abilità!'), 'resource cards piutardi l abilita');
  assert.equal(normalizza('re- source'), 'resource');
  // Un trattino davanti a una maiuscola non è un a capo: le parole restano distinte.
  assert.equal(normalizza('Catan - The Game'), 'catan the game');
});

test('la stessa frase con a capo, trattini e spazi diversi è trovata', () => {
  const chunk = [
    'BUILDING COSTS\nRoad: 1 Brick, 1 Lumber.',
    'TRADE\nDuring your turn, other play-\ners may only trade with you,\nnot with each other or  with the\nsupply. Domestic trade is…',
  ];

  const esito = cercaCitazione(CITAZIONE_CATAN, chunk);

  assert.equal(esito.trovata, true);
  assert.equal(esito.posizione, 2);
  assert.equal(esito.frammenti[0].punteggio, 1);
});

test('una frase diversa con le stesse parole comuni non è trovata', () => {
  const chunk = [
    'On your turn you may trade with the other players, and each player may trade with the supply; '
      + 'other players with you, not only during your turn.',
  ];

  const esito = cercaCitazione(CITAZIONE_CATAN, chunk);

  assert.equal(esito.trovata, false);
  assert.ok(esito.frammenti[0].migliore.punteggio < SOGLIA_PREDEFINITA);
});

test('due regole di forma parallela non si scambiano', () => {
  const longestRoad = 'The first player to have 5 continuous roads in play receives this tile. '
    + 'If another player has more continuous roads in play, they immediately receive this tile.';

  const esito = cercaCitazione(
    'If another player has more Knight cards in play, they immediately receive this tile.', [longestRoad]);

  assert.equal(esito.trovata, false);
});

test('una citazione corta è trovata solo se le sue parole stanno di seguito', () => {
  const corta = 'Pesca due carte.';
  assert.deepEqual(frammenti(corta).map((f) => f.n), [3]);

  assert.equal(cercaCitazione(corta, ['Al suo turno il giocatore pesca due carte Treno.']).trovata, true);
  assert.equal(cercaCitazione(corta, ['Pesca una carta, poi due giocatori scartano le carte.']).trovata, false);
});

test('una citazione di due parole usa sequenze di due parole', () => {
  const [frammento] = frammenti('Due carte');
  assert.equal(frammento.n, 2);
  assert.equal(cercaCitazione('Due carte', ['pesca due carte']).trovata, true);
  assert.equal(cercaCitazione('Due carte', ['due giocatori, tre carte']).trovata, false);
});

test('i frammenti separati da […] possono stare in chunk diversi; la posizione è quella del più basso', () => {
  const citazione = 'Settlements are worth 1 victory point each. […] Cities are worth 2 victory points each.';
  const chunk = [
    'Cities are worth 2 victory points each, and replace a settlement.',
    'Nothing relevant here at all, only dice and hexes.',
    'Settlements are worth 1 victory point each on the board.',
  ];

  const esito = cercaCitazione(citazione, chunk);

  assert.equal(esito.trovata, true);
  assert.deepEqual(esito.frammenti.map((f) => f.posizione), [3, 1]);
  assert.equal(esito.posizione, 3);
});

test('un frammento solo è una citazione parziale, non trovata', () => {
  const esito = cercaCitazione(
    'Settlements are worth 1 victory point each. [...] Cities are worth 2 victory points each.',
    ['Settlements are worth 1 victory point each.']);

  assert.equal(esito.trovata, false);
  assert.equal(esito.parziale, true);
  assert.equal(esito.posizione, null);
});

test('un frammento troppo corto per identificare un passaggio è ignorato', () => {
  const citazione = 'Il seguace può essere posto solo sulla tessera appena giocata. [...] Contadino [...] in una parte di campo';
  assert.deepEqual(frammenti(citazione).map((f) => f.parole.length), [10, 5]);
});

test('un passaggio spezzato fra due chunk consecutivi dello stesso documento è trovato, e la coppia segnalata', () => {
  const chunk = [
    { testo: 'During your turn, other players may only trade', documento: 'pdf-1', indice: 7 },
    { testo: 'Unrelated text about the robber and the desert.', documento: 'pdf-1', indice: 2 },
    { testo: 'with you, not with each other or with the supply.', documento: 'pdf-1', indice: 8 },
  ];

  const esito = cercaCitazione(CITAZIONE_CATAN, chunk);

  assert.equal(esito.trovata, true);
  assert.deepEqual(esito.frammenti[0].coppia, [1, 3]);
  assert.equal(esito.posizione, 3);
});

test('due chunk non consecutivi, o senza documento, non fanno coppia', () => {
  const lontani = [
    { testo: 'During your turn, other players may only trade', documento: 'pdf-1', indice: 7 },
    { testo: 'with you, not with each other or with the supply.', documento: 'pdf-1', indice: 30 },
  ];
  const senzaDocumento = ['During your turn, other players may only trade', 'with you, not with each other or with the supply.'];

  assert.equal(cercaCitazione(CITAZIONE_CATAN, lontani).trovata, false);
  assert.equal(cercaCitazione(CITAZIONE_CATAN, senzaDocumento).trovata, false);
});

test('linguaDelTesto distingue un regolamento inglese da uno italiano', () => {
  assert.equal(linguaDelTesto(CITAZIONE_CATAN), 'en');
  assert.equal(linguaDelTesto('Al suo turno, un giocatore deve eseguire una delle tre azioni seguenti'), 'it');
});
