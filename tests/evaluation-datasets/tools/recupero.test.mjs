// #4482 (review di #4492): le parti del banco che decidono senza la rete. Si lancia con
//   node --test tests/evaluation-datasets/tools/recupero.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { controllaServizi, intestazioneCookie, riepilogo, valuta } from './recupero.mjs';

const CITAZIONE = 'Se il brigante arriva su un esagono con insediamenti di più giocatori, scegli tu chi derubare.';

const domanda = (extra = {}) => ({
  id: 'CAT-19',
  game: 'Catan',
  question: 'Brigante su più giocatori: a chi rubo?',
  question_type: 'Caso Limite',
  verifica: { citazione: CITAZIONE, pagina: 6 },
  ...extra,
});

const chunk = (testo, extra = {}) => ({
  text: testo,
  pdfDocumentId: 'pdf-it',
  chunkIndex: 3,
  pageNumber: 6,
  isTranslation: false,
  ...extra,
});

const sonda = ({ pool = [], final = [], ...extra } = {}) => ({
  language: 'it',
  languageSource: 'Detected',
  profileVersionId: 'v4',
  reranking: 'Applied',
  vectorArm: 'Hit',
  embeddingModel: 'huggingface/multilingual-e5-base',
  rerankerModel: 'BAAI/bge-reranker-v2-m3',
  fusedCount: pool.length,
  pool,
  final,
  ...extra,
});

const opzioni = { soglia: 0.7, linguaRegolamento: 'it' };

test('cookie: il valore nudo diventa nome=valore, anche quando finisce con = (base64)', () => {
  assert.equal(intestazioneCookie('abc', 'meepleai_session'), 'meepleai_session=abc');
  assert.equal(intestazioneCookie('QUJDRA==', 'meepleai_session'), 'meepleai_session=QUJDRA==');
  assert.equal(intestazioneCookie(' a+b/c= ', 'meepleai_session'), 'meepleai_session=a+b/c=');
});

test('cookie: è un header intero solo se comincia con il nome del cookie e =', () => {
  assert.equal(intestazioneCookie('meepleai_session=abc==', 'meepleai_session'), 'meepleai_session=abc==');
  assert.equal(intestazioneCookie('sessione=xyz', 'sessione'), 'sessione=xyz');
  assert.equal(intestazioneCookie('altro=xyz', 'meepleai_session'), 'meepleai_session=altro=xyz');
  assert.equal(intestazioneCookie('   ', 'meepleai_session'), null);
});

test('controllaServizi: si ferma su un embedding finto', () => {
  assert.throws(() => controllaServizi(sonda({ embeddingModel: 'mock-e5-base' })), /mock-e5-base/);
});

test('controllaServizi: si ferma su un reranker finto', () => {
  assert.throws(() => controllaServizi(sonda({ rerankerModel: 'Mock-bge-reranker-jaccard' })), /reranker/);
});

test('controllaServizi: accetta i servizi veri, e un reranker che non ha risposto', () => {
  controllaServizi(sonda());
  controllaServizi(sonda({ rerankerModel: null }));
});

test('un braccio vettoriale fallito è un errore, non una citazione mancata', () => {
  const esito = valuta(domanda(), sonda({ vectorArm: 'Failed', pool: [chunk('nulla di utile qui')] }), opzioni);

  assert.match(esito.errore, /vettoriale/);
  assert.equal(esito.pool, undefined);
  const conte = riepilogo([esito]).per_gioco.Catan.in_ambito;
  assert.equal(conte.errori, 1);
  assert.equal(conte.nel_pool, 0);
});

test('un braccio vettoriale vuoto non è un errore, ma si conta', () => {
  const esito = valuta(domanda(), sonda({ vectorArm: 'Empty', pool: [chunk(CITAZIONE)], final: [chunk(CITAZIONE)] }), opzioni);

  assert.equal(esito.errore, undefined);
  assert.equal(esito.braccio_vettoriale, 'Empty');
  const conte = riepilogo([esito]).per_gioco.Catan.in_ambito;
  assert.equal(conte.vettoriale_vuoto, 1);
  assert.equal(conte.nei_finali, 1);
});

test('mancata nei finali, con la traduzione dello stesso PDF nei finali: segnalata e contata', () => {
  const esito = valuta(domanda(), sonda({
    pool: [chunk(CITAZIONE)],
    final: [
      chunk('If the robber moves to a hex with several players, you choose one player to rob.',
        { chunkIndex: 43, pageNumber: 6, isTranslation: true }),
      chunk('Si pescano due carte sviluppo.', { chunkIndex: 9, pageNumber: 8 }),
    ],
  }), opzioni);

  assert.equal(esito.finali.trovata, false);
  assert.equal(esito.traduzione_nei_finali, true);
  assert.deepEqual(esito.pagine_tradotte_nei_finali, [6]);
  assert.equal(riepilogo([esito]).per_lingua_regolamento.it.in_ambito.mancate_con_traduzione_nei_finali, 1);
});

test('la traduzione di un altro PDF nei finali non conta', () => {
  const esito = valuta(domanda(), sonda({
    pool: [chunk(CITAZIONE)],
    final: [chunk('Move the robber.', { pdfDocumentId: 'pdf-altro', chunkIndex: 43, isTranslation: true })],
  }), opzioni);

  assert.equal(esito.traduzione_nei_finali, false);
});

test('trovata nei finali: la traduzione accanto non conta', () => {
  const esito = valuta(domanda(), sonda({
    pool: [chunk(CITAZIONE)],
    final: [chunk(CITAZIONE), chunk('Move the robber.', { chunkIndex: 43, isTranslation: true })],
  }), opzioni);

  assert.equal(esito.finali.trovata, true);
  assert.equal(esito.traduzione_nei_finali, false);
});
