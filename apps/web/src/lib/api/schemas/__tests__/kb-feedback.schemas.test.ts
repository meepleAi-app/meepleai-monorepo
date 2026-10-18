import { describe, it, expect } from 'vitest';

import { AdminKbFeedbackSchema, AdminKbFeedbackSummarySchema } from '../knowledge-base.schemas';

/**
 * #4469: le risposte di GET /api/v1/admin/kb/feedback e /summary come le scrive l'API, che scrive i
 * campi nulli come `null` (non li omette).
 */
describe('AdminKbFeedbackSchema', () => {
  const item = {
    id: '11111111-1111-1111-1111-111111111111',
    userId: '22222222-2222-2222-2222-222222222222',
    gameId: '33333333-3333-3333-3333-333333333333',
    gameName: 'Catan',
    chatSessionId: '44444444-4444-4444-4444-444444444444',
    messageId: '55555555-5555-5555-5555-555555555555',
    profileVersionId: '66666666-6666-6666-6666-666666666666',
    versionNumber: 4,
    outcome: 'not_helpful',
    comment: 'Non risponde alla domanda',
    createdAt: '2026-10-18T10:00:00Z',
    answerExcerpt: 'Ogni giocatore pesca cinque carte.',
  };

  it('parses a feedback with its game, version and answer', () => {
    expect(AdminKbFeedbackSchema.parse({ total: 1, items: [item] }).items[0]).toEqual(item);
  });

  it('parses a feedback without comment, version, game name or answer: the API writes them as null', () => {
    const bare = {
      ...item,
      gameName: null,
      profileVersionId: null,
      versionNumber: null,
      comment: null,
      answerExcerpt: null,
    };

    expect(AdminKbFeedbackSchema.parse({ total: 1, items: [bare] }).items[0]).toEqual(bare);
  });
});

describe('AdminKbFeedbackSummarySchema', () => {
  it('parses the versions, the bucket without a version, and the games', () => {
    const summary = {
      versions: [
        {
          profileVersionId: '66666666-6666-6666-6666-666666666666',
          versionNumber: 4,
          status: 'Published',
          publishedAt: '2026-10-15T08:00:00Z',
          helpful: 3,
          notHelpful: 1,
        },
        {
          profileVersionId: null,
          versionNumber: null,
          status: null,
          publishedAt: null,
          helpful: 1,
          notHelpful: 0,
        },
      ],
      games: [{ gameId: '33333333-3333-3333-3333-333333333333', gameName: null }],
    };

    expect(AdminKbFeedbackSummarySchema.parse(summary)).toEqual(summary);
  });
});
