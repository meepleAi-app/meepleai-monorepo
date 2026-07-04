import { describe, it, expect } from 'vitest';

import {
  AddDiaryEntryRequestSchema,
  LiveSessionDiaryEntryDtoSchema,
} from '../live-sessions.schemas';

/**
 * #2575 — diary write-path contracts. The request mirrors the BE validator
 * (NotEmpty + MaximumLength 2000); the DTO mirrors BE DiaryEntryDto.
 */
describe('AddDiaryEntryRequestSchema (#2575)', () => {
  it('accepts a 1..2000-char text', () => {
    expect(AddDiaryEntryRequestSchema.safeParse({ text: 'a' }).success).toBe(true);
    expect(AddDiaryEntryRequestSchema.safeParse({ text: 'x'.repeat(2000) }).success).toBe(true);
  });

  it('rejects an empty text', () => {
    expect(AddDiaryEntryRequestSchema.safeParse({ text: '' }).success).toBe(false);
  });

  it('rejects a text longer than 2000 chars', () => {
    expect(AddDiaryEntryRequestSchema.safeParse({ text: 'x'.repeat(2001) }).success).toBe(false);
  });
});

describe('LiveSessionDiaryEntryDtoSchema (#2575)', () => {
  const valid = {
    id: '11111111-1111-4111-8111-111111111111',
    authorId: '22222222-2222-4222-8222-222222222222',
    createdAt: '2026-01-01T10:00:00Z',
    text: 'Played the Forest Witch card',
  };

  it('parses a well-formed diary entry', () => {
    expect(LiveSessionDiaryEntryDtoSchema.parse(valid)).toEqual(valid);
  });

  it('rejects a non-uuid id', () => {
    expect(LiveSessionDiaryEntryDtoSchema.safeParse({ ...valid, id: 'not-a-uuid' }).success).toBe(
      false
    );
  });
});
