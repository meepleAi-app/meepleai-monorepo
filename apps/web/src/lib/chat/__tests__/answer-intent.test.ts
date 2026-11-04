/**
 * Answer intents — #4404 (ADR-098 §5).
 *
 * The chat selector stops choosing an «agent» and chooses the SHAPE of the answer the single agent gives. The list
 * lives in one place on each side, and a test compares them: a list that drifts silently is how the selector came
 * to offer `stratega` and `narratore` while the backend refused both.
 */

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

import {
  ANSWER_INTENTS,
  ANSWER_INTENT_DESCRIPTIONS,
  ANSWER_INTENT_LABELS,
  intentOf,
} from '../answer-intent';

describe('ANSWER_INTENTS', () => {
  it('are auto, tutor and secca, in the order the selector shows them', () => {
    expect([...ANSWER_INTENTS]).toEqual(['auto', 'tutor', 'secca']);
  });

  it('each has a label and a description', () => {
    for (const intent of ANSWER_INTENTS) {
      expect(ANSWER_INTENT_LABELS[intent].trim().length).toBeGreaterThan(0);
      expect(ANSWER_INTENT_DESCRIPTIONS[intent].trim().length).toBeGreaterThan(0);
    }
  });

  it('call the third one «Risposta secca», not by its id', () => {
    expect(ANSWER_INTENT_LABELS.secca).toBe('Risposta secca');
  });
});

describe('intentOf', () => {
  it.each([
    ['auto', 'auto'],
    ['tutor', 'tutor'],
    ['secca', 'secca'],
    ['Tutor', 'tutor'],
    ['  SECCA ', 'secca'],
  ])('reads the stored %j as %j', (stored, expected) => {
    expect(intentOf(stored)).toBe(expected);
  });

  it.each([null, undefined, '', '   ', 'arbitro', 'decisore', 'stratega', 'narratore', 'whatever'])(
    'reads %j as auto, so the selector is never left empty',
    stored => {
      expect(intentOf(stored)).toBe('auto');
    }
  );
});

describe('the two lists', () => {
  // The backend list is read from its source: there is no endpoint to ask, and the test must fail the moment
  // one side changes without the other.
  const source = readFileSync(
    resolve(
      process.cwd(),
      '../api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AnswerIntent.cs'
    ),
    'utf8'
  );

  const constants = new Map(
    [...source.matchAll(/public const string (\w+) = "([^"]+)";/g)].map(m => [m[1], m[2]])
  );

  it('the backend names its intents with the same ids', () => {
    const all = /public static IReadOnlyList<string> All \{ get; \} = \[([^\]]+)\];/.exec(source);
    expect(all, 'the All list of AnswerIntent.cs').not.toBeNull();

    const backend = all![1]
      .split(',')
      .map(name => constants.get(name.trim()))
      .filter((id): id is string => id !== undefined);

    expect(backend).toEqual([...ANSWER_INTENTS]);
  });
});
