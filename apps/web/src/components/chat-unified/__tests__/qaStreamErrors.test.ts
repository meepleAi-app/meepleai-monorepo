/**
 * #4388 — il rifiuto 429 delle rotte di risposta.
 *
 * Il backend limita a due le risposte aperte per utente: la terza domanda riceve 429 prima che il
 * gateway sia chiamato. Le viste devono dirlo in italiano e non ritentare (un nuovo tentativo
 * arriverebbe mentre le altre due sono ancora in corso), non mostrare «HTTP 429» né il guasto
 * generico.
 */

import { describe, expect, it } from 'vitest';

import {
  QA_TOO_MANY_REQUESTS_MESSAGE,
  QaTooManyRequestsError,
  qaRequestFailureMessage,
  throwIfQaTooManyRequests,
} from '../qaStreamErrors';

describe('QA_TOO_MANY_REQUESTS_MESSAGE', () => {
  it('says to wait for an answer in progress, in Italian', () => {
    expect(QA_TOO_MANY_REQUESTS_MESSAGE).toBe(
      'Hai già due risposte in corso. Aspetta che una finisca, poi riprova.'
    );
  });
});

describe('throwIfQaTooManyRequests', () => {
  it('throws the error that carries the message, for a 429', () => {
    expect(() => throwIfQaTooManyRequests({ status: 429 })).toThrow(QaTooManyRequestsError);
    expect(() => throwIfQaTooManyRequests({ status: 429 })).toThrow(QA_TOO_MANY_REQUESTS_MESSAGE);
  });

  it.each([200, 401, 403, 404, 500, 503])(
    'lets a %i through to the caller own handling',
    status => {
      expect(() => throwIfQaTooManyRequests({ status })).not.toThrow();
    }
  );
});

describe('qaRequestFailureMessage', () => {
  it('gives the message of the 429 when that is what failed', () => {
    expect(qaRequestFailureMessage(new QaTooManyRequestsError(), 'Errore generico')).toBe(
      QA_TOO_MANY_REQUESTS_MESSAGE
    );
  });

  it.each([
    ['another error', new Error('QA stream failed: HTTP 500')],
    ['a network failure', new TypeError('Failed to fetch')],
    ['something that is not an error', 'boom'],
    ['nothing', undefined],
  ])('gives the fallback for %s', (_label, failure) => {
    expect(qaRequestFailureMessage(failure, 'Errore generico')).toBe('Errore generico');
  });
});
