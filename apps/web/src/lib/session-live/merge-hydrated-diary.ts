import type { LiveSessionDiaryEntryDto } from '@/lib/api/schemas/live-sessions.schemas';

import type { SessionEvent } from './sse-events';

/**
 * #2575: merges hydrated historical diary entries (from GET /live-sessions/{id}/diary) with the
 * live SSE event stream for composeSessionLiveState.
 *
 * Hydrated entries are converted to session:diary events, deduped by entryId against the SSE
 * stream (hydrated wins — an SSE diary event for the same entry that races the initial fetch is
 * dropped), then the combined list is sorted by timestamp. The sort is required because
 * composeSessionLiveState appends to actionLog in array order and assumes ascending order:
 * without it, a hydrated diary entry would render ahead of a score/chat/tool SSE event that
 * happened earlier (mixed-event sessions reopened mid-stream). ISO-8601 timestamps sort
 * lexicographically, and Array.prototype.sort is stable in V8 so the hydrated-wins dedup is
 * preserved for equal-timestamp diary duplicates.
 */
export function mergeHydratedDiary(
  diaryEntries: ReadonlyArray<LiveSessionDiaryEntryDto>,
  sseEvents: ReadonlyArray<SessionEvent>,
  sessionId: string
): SessionEvent[] {
  const diaryEvents: SessionEvent[] = diaryEntries.map(d => ({
    type: 'session:diary',
    sessionId,
    entryId: d.id,
    authorId: d.authorId,
    content: d.text,
    timestamp: d.createdAt,
  }));

  const seenDiaryIds = new Set<string>();
  return [...diaryEvents, ...sseEvents]
    .filter(e => {
      if (e.type !== 'session:diary') return true;
      if (seenDiaryIds.has(e.entryId)) return false;
      seenDiaryIds.add(e.entryId);
      return true;
    })
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));
}
