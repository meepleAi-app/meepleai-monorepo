/**
 * #4692 — la creazione di una serata porta un `id` scelto dal chiamante.
 *
 * Lo stesso `id` dello stesso organizzatore e' la stessa creazione per il server: e' cio' che rende
 * sicuro ripetere la richiesta.
 */

import { describe, expect, it, vi } from 'vitest';

import type { HttpClient } from '../../core/httpClient';
import { CreateGameNightInputSchema } from '../../schemas/game-nights.schemas';
import { createGameNightsClient } from '../gameNightsClient';

describe('gameNightsClient.create (#4692)', () => {
  it("manda l'id nel corpo di POST /api/v1/game-nights, insieme al resto", async () => {
    const post = vi.fn().mockResolvedValue('night-1');
    const client = createGameNightsClient({ httpClient: { post } as unknown as HttpClient });
    const id = '3f2b8a9e-6c1d-4f7a-9b35-1d2e4c6a8b90';

    const created = await client.create({
      id,
      title: 'Serata di prova',
      scheduledAt: '2030-01-01T20:00:00.000Z',
      invitedEmails: ['ospite@example.com'],
    });

    expect(created).toBe('night-1');
    expect(post).toHaveBeenCalledWith(
      '/api/v1/game-nights',
      expect.objectContaining({
        id,
        title: 'Serata di prova',
        invitedEmails: ['ospite@example.com'],
      })
    );
  });
});

describe('CreateGameNightInputSchema.id (#4692)', () => {
  const base = { title: 'Serata di prova', scheduledAt: '2030-01-01T20:00:00.000Z' };

  it("e' facoltativo: senza id l'input resta valido", () => {
    expect(CreateGameNightInputSchema.safeParse(base).success).toBe(true);
  });

  it("accetta un UUID e rifiuta una stringa che non lo e'", () => {
    const uuid = '3f2b8a9e-6c1d-4f7a-9b35-1d2e4c6a8b90';
    expect(CreateGameNightInputSchema.safeParse({ ...base, id: uuid }).success).toBe(true);
    expect(CreateGameNightInputSchema.safeParse({ ...base, id: 'non-un-uuid' }).success).toBe(
      false
    );
  });
});
