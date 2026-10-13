/**
 * #4168 — l'id di un messaggio di chat salvato.
 *
 * Il backend lo manda come `id` (`ChatMessageDto.Id`); chi consuma lo schema lo legge come
 * `backendMessageId`. Lo schema chiedeva solo il secondo, che il backend non manda: l'id si perdeva
 * nel parsing e ogni consumatore ricadeva su un id inventato, che il feedback mandava al backend e
 * che il validatore rifiutava.
 */

import { describe, expect, it } from 'vitest';

import { ChatThreadDtoSchema } from '../chat.schemas';

const MESSAGE_ID = '6f1c2a3b-4d5e-4f60-8a71-b2c3d4e5f607';

function thread(message: Record<string, unknown>) {
  return {
    id: '1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d',
    gameId: '2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e',
    title: 'Catan',
    createdAt: '2026-10-12T08:00:00Z',
    lastMessageAt: '2026-10-12T08:01:00Z',
    messageCount: 1,
    messages: [
      { content: 'Five cards.', role: 'assistant', timestamp: '2026-10-12T08:01:00Z', ...message },
    ],
  };
}

describe('ChatThreadDtoSchema — message id', () => {
  it('exposes the id the backend sends as backendMessageId', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({ id: MESSAGE_ID }));

    expect(parsed.messages[0].backendMessageId).toBe(MESSAGE_ID);
  });

  it('keeps an explicit backendMessageId', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({ backendMessageId: MESSAGE_ID }));

    expect(parsed.messages[0].backendMessageId).toBe(MESSAGE_ID);
  });

  it('leaves it undefined when the message carries no id', () => {
    const parsed = ChatThreadDtoSchema.parse(thread({}));

    expect(parsed.messages[0].backendMessageId).toBeUndefined();
  });
});
