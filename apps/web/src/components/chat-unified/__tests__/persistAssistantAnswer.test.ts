/**
 * #4838 — il salvataggio della risposta dell'assistente, condiviso da `ChatThreadView` e `ChatMobile`.
 */

import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

import { persistAssistantAnswer } from '../persistAssistantAnswer';

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      addMessage: vi.fn(),
    },
  },
}));

const ANSWER = { content: 'Cinque carte.', profileVersionId: 'pv-7', language: 'it' };

async function addMessage(): Promise<Mock> {
  const { api } = await import('@/lib/api');
  return api.chat.addMessage as Mock;
}

describe('persistAssistantAnswer (#4838)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    (await addMessage()).mockResolvedValue({ messages: [] });
  });

  it('saves the answer as an assistant message, with the profile version and the language', async () => {
    await persistAssistantAnswer('thread-1', ANSWER);

    expect(await addMessage()).toHaveBeenCalledWith('thread-1', {
      content: 'Cinque carte.',
      role: 'assistant',
      profileVersionId: 'pv-7',
      language: 'it',
    });
  });

  it('passes a missing profile version and language through as null', async () => {
    await persistAssistantAnswer('thread-1', {
      content: 'Cinque carte.',
      profileVersionId: null,
      language: null,
    });

    expect(await addMessage()).toHaveBeenCalledWith(
      'thread-1',
      expect.objectContaining({ profileVersionId: null, language: null })
    );
  });

  it('returns the id the backend gave to the answer: the last assistant message of the thread', async () => {
    (await addMessage()).mockResolvedValue({
      messages: [
        { role: 'user', content: 'Quante carte?', backendMessageId: 'u-1' },
        { role: 'assistant', content: 'Una risposta di prima', backendMessageId: 'a-1' },
        { role: 'user', content: 'E adesso?', backendMessageId: 'u-2' },
        { role: 'assistant', content: 'Cinque carte.', backendMessageId: 'a-2' },
      ],
    });

    await expect(persistAssistantAnswer('thread-1', ANSWER)).resolves.toBe('a-2');
  });

  it.each([
    ['the response has no messages', { messages: [] }],
    [
      'the response has only user messages',
      { messages: [{ role: 'user', backendMessageId: 'u-1' }] },
    ],
    ['the answer carries no id', { messages: [{ role: 'assistant', content: 'Cinque carte.' }] }],
    ['there is no response body', undefined],
  ])('returns null when %s', async (_case, response) => {
    (await addMessage()).mockResolvedValue(response);

    await expect(persistAssistantAnswer('thread-1', ANSWER)).resolves.toBeNull();
  });

  it('lets a rejected request reach the caller, which decides what the user sees', async () => {
    (await addMessage()).mockRejectedValue(new Error('network down'));

    await expect(persistAssistantAnswer('thread-1', ANSWER)).rejects.toThrow('network down');
  });
});
