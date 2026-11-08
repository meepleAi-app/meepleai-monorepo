import { describe, it, expect } from 'vitest';

import { AdminReferenceQuestionsSchema } from '../knowledge-base.schemas';

/**
 * #4470 (ADR-095 fetta 7b): la risposta di GET /api/v1/admin/kb/reference-questions come la scrive
 * l'API, che scrive i campi nulli come `null` (non li omette).
 */
describe('AdminReferenceQuestionsSchema', () => {
  const question = {
    code: 'CAT-02',
    gameId: '33333333-3333-3333-3333-333333333333',
    gameName: 'Catan',
    question: 'Posso giocare più di una carta Sviluppo nello stesso turno?',
    expectedAnswer: 'No, al massimo una per turno.',
    category: 'Sviluppo / Condizioni di Vittoria',
    difficulty: 'Hard',
    questionType: 'Eccezione / Limite',
    sourceReference: 'Manuale Catan - Carte Sviluppo',
    outOfCorpus: false,
    inGate: true,
  };

  it('parses the questions and the games with their count', () => {
    const response = {
      items: [question],
      games: [{ gameId: question.gameId, gameName: 'Catan', questionCount: 30 }],
    };

    expect(AdminReferenceQuestionsSchema.parse(response)).toEqual(response);
  });

  it('parses a question whose game is no longer in the catalog: the API writes the name as null', () => {
    const response = {
      items: [{ ...question, gameName: null }],
      games: [{ gameId: question.gameId, gameName: null, questionCount: 1 }],
    };

    expect(AdminReferenceQuestionsSchema.parse(response)).toEqual(response);
  });

  it('refuses a question without the gate flag', () => {
    const { inGate: _inGate, ...withoutGate } = question;

    expect(() =>
      AdminReferenceQuestionsSchema.parse({ items: [withoutGate], games: [] })
    ).toThrow();
  });
});
