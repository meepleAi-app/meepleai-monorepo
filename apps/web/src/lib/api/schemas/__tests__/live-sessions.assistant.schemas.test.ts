import { describe, expect, it } from 'vitest';

import {
  AssistantProposalDtoSchema,
  AssistantTurnEventSchema,
  SessionAssistantMessageDtoSchema,
  SessionAssistantPanelDtoSchema,
} from '../live-sessions.schemas';

/**
 * #4238 A4 (#4373) — the contract of the in-game assistant, consumers before producers (#4154).
 *
 * The payloads below are the shapes the backend writes: `AssistantProposalDto` and
 * `AssistantTurnEvent` serialized for SSE, `SessionAssistantPanelDto` from the panel read (#4372).
 * The schemas must accept them, and must not break on a field or a status the server adds later.
 */

const proposal = {
  id: '6a1f6d5e-0f3b-4c1e-9d55-2f1e9b7f1a01',
  kind: 'Score',
  status: 'Pending',
  messageId: '0199e3a4-5b6c-7d8e-9f00-112233445566',
  playerId: '6a1f6d5e-0f3b-4c1e-9d55-2f1e9b7f1a02',
  playerName: 'Marco',
  dimension: 'punti',
  round: 1,
  points: 5,
  cellValueBefore: 0,
  resultingValue: 5,
  expectedTurnIndex: null,
  confirmedPoints: null,
  failureCode: null,
  summary: '+5 punti a Marco, round 1',
};

describe('AssistantProposalDtoSchema (#4373)', () => {
  it('accepts the proposal the backend writes', () => {
    expect(AssistantProposalDtoSchema.parse(proposal)).toMatchObject({ kind: 'Score', points: 5 });
  });

  it('accepts a turn proposal, whose score fields are null', () => {
    const turn = {
      ...proposal,
      kind: 'AdvanceTurn',
      playerId: null,
      playerName: null,
      dimension: null,
      round: null,
      points: null,
      cellValueBefore: null,
      resultingValue: null,
      expectedTurnIndex: 4,
      summary: 'Passare il turno al giocatore successivo',
    };
    expect(AssistantProposalDtoSchema.parse(turn).expectedTurnIndex).toBe(4);
  });

  it('does not break on a field or a status the server adds later', () => {
    const later = { ...proposal, status: 'Superseded', confidence: 0.9 };
    const parsed = AssistantProposalDtoSchema.parse(later);
    expect(parsed.status).toBe('Superseded');
    expect(parsed).not.toHaveProperty('confidence');
  });
});

describe('AssistantTurnEventSchema (#4373)', () => {
  it.each([
    [{ type: 'status' }],
    [{ type: 'status', tool: 'stato_partita' }],
    [{ type: 'proposal', proposal }],
    [{ type: 'answer', text: 'Ho preparato la proposta.' }],
    [
      {
        type: 'done',
        responseId: '0199e3a4-5b6c-7d8e-9f00-112233445566',
        profileVersionId: '6a1f6d5e-0f3b-4c1e-9d55-2f1e9b7f1a03',
        profileVersionNumber: 3,
      },
    ],
    [{ type: 'error', code: 'turn_timeout' }],
  ])('accepts %j', event => {
    expect(AssistantTurnEventSchema.safeParse(event).success).toBe(true);
  });

  it('accepts the null fields the serializer may write for an unused field', () => {
    const status = {
      type: 'status',
      tool: null,
      proposal: null,
      text: null,
      responseId: null,
      profileVersionId: null,
      profileVersionNumber: null,
      code: null,
    };
    expect(AssistantTurnEventSchema.safeParse(status).success).toBe(true);
  });
});

describe('SessionAssistantPanelDtoSchema (#4372, #4373)', () => {
  it('accepts the panel read', () => {
    const panel = {
      availability: { enabled: true, code: null },
      messages: [
        {
          id: '0199e3a4-5b6c-7d8e-9f00-112233445500',
          role: 'user',
          content: 'Marco fa 5 punti',
          createdAt: '2026-10-14T10:00:00Z',
        },
      ],
      proposals: [proposal],
    };
    const parsed = SessionAssistantPanelDtoSchema.parse(panel);
    expect(parsed.messages).toHaveLength(1);
    expect(parsed.proposals).toHaveLength(1);
  });

  it('accepts an assistant that is off, with the code of the turn', () => {
    const parsed = SessionAssistantPanelDtoSchema.parse({
      availability: { enabled: false, code: 'assistant_disabled' },
      messages: [],
      proposals: [],
    });
    expect(parsed.availability).toEqual({ enabled: false, code: 'assistant_disabled' });
  });
});

describe('citations (#4411)', () => {
  const full = {
    documentId: 'pdf-1',
    pageNumber: 3,
    relevanceScore: 0.9,
    snippetPreview: 'Si pescano 4 tessere.',
    copyrightTier: 'full',
    paraphrasedSnippet: null,
    isPublic: true,
  };
  const protectedCitation = {
    ...full,
    snippetPreview: null,
    copyrightTier: 'protected',
    paraphrasedSnippet: 'Quattro tessere.',
  };

  it('reads an answer without citations, as the server sends it today', () => {
    const event = AssistantTurnEventSchema.parse({ type: 'answer', text: 'Tocca a Marco.' });
    expect(event.citations ?? null).toBeNull();
  });

  it('reads the citations of an answer', () => {
    const event = AssistantTurnEventSchema.parse({
      type: 'answer',
      text: 'Quattro.',
      citations: [full, protectedCitation],
    });
    expect(event.citations).toHaveLength(2);
    expect(event.citations?.[1]?.copyrightTier).toBe('protected');
  });

  it('keeps the answer when a citation has a shape it does not know', () => {
    const event = AssistantTurnEventSchema.parse({
      type: 'answer',
      text: 'Quattro.',
      citations: [{ ...full, copyrightTier: 'partial' }],
    });
    expect(event.text).toBe('Quattro.');
    expect(event.citations).toBeNull();
  });

  it('reads the citations of a saved message', () => {
    const message = SessionAssistantMessageDtoSchema.parse({
      id: 'm1',
      role: 'assistant',
      content: 'Quattro.',
      createdAt: '2026-10-15T10:00:00Z',
      citations: [full],
    });
    expect(message.citations).toHaveLength(1);
  });
});
