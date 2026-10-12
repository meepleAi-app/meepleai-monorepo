/**
 * ThreadCreator — which thread-creation failures get an explanation (issue #4230).
 *
 * ApiError is imported from its own module, not through `@/lib/api`: the callers' tests mock
 * `@/lib/api`, and a mock that leaves ApiError out would make `instanceof` silently false.
 */
import { describe, expect, it } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';

import { PRIVATE_GAME_CHAT_UNAVAILABLE, explainThreadCreationFailure } from '../ThreadCreator';

describe('explainThreadCreationFailure', () => {
  it('explains the refusal of a thread on the user’s own private game', () => {
    const refusal = new ApiError({
      message: 'La chat sui giochi privati non è ancora disponibile.',
      statusCode: 422,
      code: PRIVATE_GAME_CHAT_UNAVAILABLE,
    });

    expect(explainThreadCreationFailure(refusal)).toBe(
      'La chat sui giochi privati non è ancora disponibile.'
    );
  });

  it('has nothing to say about a failure it does not recognise', () => {
    expect(
      explainThreadCreationFailure(new ApiError({ message: 'boom', statusCode: 500 }))
    ).toBeNull();
    expect(
      explainThreadCreationFailure(
        new ApiError({ message: 'not found', statusCode: 404, code: 'not_found' })
      )
    ).toBeNull();
    expect(explainThreadCreationFailure(new Error('network'))).toBeNull();
    expect(explainThreadCreationFailure(undefined)).toBeNull();
  });

  it('does not trust a look-alike that is not an ApiError', () => {
    // A plain object with the same code must not be read as the backend's refusal.
    expect(explainThreadCreationFailure({ code: PRIVATE_GAME_CHAT_UNAVAILABLE })).toBeNull();
  });
});
