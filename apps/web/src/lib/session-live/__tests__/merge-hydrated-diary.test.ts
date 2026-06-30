import { describe, expect, it } from 'vitest';

import type { LiveSessionDiaryEntryDto } from '@/lib/api/schemas/live-sessions.schemas';

import { mergeHydratedDiary } from '../merge-hydrated-diary';
import type { SessionEvent } from '../sse-events';

const SESSION_ID = 'sess-1';

function diary(id: string, createdAt: string): LiveSessionDiaryEntryDto {
  return { id, authorId: 'u-1', createdAt, text: `entry ${id}` };
}

function scoreEvent(timestamp: string): Extract<SessionEvent, { type: 'session:score' }> {
  return {
    type: 'session:score',
    sessionId: SESSION_ID,
    participantId: 'p-1',
    score: 1,
    updatedBy: 'p-1',
    timestamp,
  };
}

function diaryEvent(
  entryId: string,
  timestamp: string
): Extract<SessionEvent, { type: 'session:diary' }> {
  return {
    type: 'session:diary',
    sessionId: SESSION_ID,
    entryId,
    authorId: 'u-1',
    content: `sse ${entryId}`,
    timestamp,
  };
}

describe('mergeHydratedDiary (#2575)', () => {
  it('converts hydrated diary entries to session:diary events', () => {
    const merged = mergeHydratedDiary([diary('d1', '2026-01-01T10:00:00Z')], [], SESSION_ID);
    expect(merged).toHaveLength(1);
    expect(merged[0]).toMatchObject({
      type: 'session:diary',
      sessionId: SESSION_ID,
      entryId: 'd1',
      content: 'entry d1',
      timestamp: '2026-01-01T10:00:00Z',
    });
  });

  it('sorts the merged stream chronologically so a hydrated diary lands between earlier/later SSE events', () => {
    // Hydrated diary at 10:01; SSE score at 10:00 and 10:02. Naive prepend would put the diary
    // first; the timestamp sort must interleave it correctly.
    const merged = mergeHydratedDiary(
      [diary('d1', '2026-01-01T10:01:00Z')],
      [scoreEvent('2026-01-01T10:00:00Z'), scoreEvent('2026-01-01T10:02:00Z')],
      SESSION_ID
    );
    expect(merged.map(e => e.timestamp)).toEqual([
      '2026-01-01T10:00:00Z',
      '2026-01-01T10:01:00Z',
      '2026-01-01T10:02:00Z',
    ]);
    expect(merged[1].type).toBe('session:diary');
  });

  it('dedups by entryId (hydrated wins) when the same entry also arrives over SSE', () => {
    const merged = mergeHydratedDiary(
      [diary('d1', '2026-01-01T10:00:00Z')],
      [diaryEvent('d1', '2026-01-01T10:00:00Z'), scoreEvent('2026-01-01T10:05:00Z')],
      SESSION_ID
    );
    const diaryEntries = merged.filter(e => e.type === 'session:diary');
    expect(diaryEntries).toHaveLength(1);
    // hydrated version wins: content is the hydrated 'entry d1', not the SSE 'sse d1'
    expect((diaryEntries[0] as Extract<SessionEvent, { type: 'session:diary' }>).content).toBe(
      'entry d1'
    );
  });

  it('passes non-diary SSE events through unchanged', () => {
    const score = scoreEvent('2026-01-01T10:00:00Z');
    const merged = mergeHydratedDiary([], [score], SESSION_ID);
    expect(merged).toEqual([score]);
  });

  it('returns an empty array when there is nothing to merge', () => {
    expect(mergeHydratedDiary([], [], SESSION_ID)).toEqual([]);
  });
});
