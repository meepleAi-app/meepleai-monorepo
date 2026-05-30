import { describe, expect, it } from 'vitest';
import { toActivityItem } from '../activity-adapter';
import type { ActivityItemDto } from '@/lib/api/schemas/activity.schemas';

const dtoBase: Omit<ActivityItemDto, 'eventType' | 'entityType' | 'title'> = {
  id: '11111111-1111-1111-1111-111111111111',
  eventId: '22222222-2222-2222-2222-222222222222',
  userId: '33333333-3333-3333-3333-333333333333',
  entityId: '44444444-4444-4444-4444-444444444444',
  timestamp: '2026-05-28T11:00:00+00:00',
  loggedAt: '2026-05-28T11:00:01+00:00',
  payloadVersion: 1,
};

const fallback = {
  agent: 'Nuovo agent creato',
  chat: 'Nuova chat',
  kbIndexed: 'Documento indicizzato',
  play: 'Sessione',
  removed: 'Rimosso dalla libreria',
};

describe('toActivityItem — eventType → kind mapping (R2)', () => {
  it('maps agent.created → kind="agent"', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'Catan Tutor' },
      fallback
    );
    expect(r.kind).toBe('agent');
    expect(r.entityTitle).toBe('Catan Tutor');
  });
  it('maps chat.session.created → kind="chat"', () => {
    const r = toActivityItem(
      {
        ...dtoBase,
        eventType: 'chat.session.created',
        entityType: 'ChatSession',
        title: 'Catan chat',
      },
      fallback
    );
    expect(r.kind).toBe('chat');
  });
  it('maps kb.doc.indexed → kind="kb-indexed"', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'kb.doc.indexed', entityType: 'PdfDocument', title: 'rules.pdf' },
      fallback
    );
    expect(r.kind).toBe('kb-indexed');
  });
  it('maps session.created → kind="play"', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'session.created', entityType: 'Session', title: null },
      fallback
    );
    expect(r.kind).toBe('play');
  });
  it('maps session.finalized → kind="play"', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'session.finalized', entityType: 'Session', title: null },
      fallback
    );
    expect(r.kind).toBe('play');
  });
  it('maps library.entry.removed → kind="removed"', () => {
    const r = toActivityItem(
      {
        ...dtoBase,
        eventType: 'library.entry.removed',
        entityType: 'UserLibraryEntry',
        title: null,
      },
      fallback
    );
    expect(r.kind).toBe('removed');
  });
  it('maps library.session.recorded → kind="play"', () => {
    const r = toActivityItem(
      {
        ...dtoBase,
        eventType: 'library.session.recorded',
        entityType: 'UserLibraryEntry',
        title: null,
      },
      fallback
    );
    expect(r.kind).toBe('play');
  });
  it('unknown eventType falls back to kind="add"', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'something.unknown', entityType: 'Other', title: null },
      fallback
    );
    expect(r.kind).toBe('add');
  });
});

describe('toActivityItem — title fallback (R2)', () => {
  it('uses dto.title when present', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'My Agent' },
      fallback
    );
    expect(r.entityTitle).toBe('My Agent');
  });
  it('falls back per kind when title null (kb)', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'kb.doc.indexed', entityType: 'PdfDocument', title: null },
      fallback
    );
    expect(r.entityTitle).toBe('Documento indicizzato');
  });
  it('falls back per kind when title null (play)', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'session.created', entityType: 'Session', title: null },
      fallback
    );
    expect(r.entityTitle).toBe('Sessione');
  });
  it('preserves id and timestamp', () => {
    const r = toActivityItem(
      { ...dtoBase, eventType: 'agent.created', entityType: 'Agent', title: 'X' },
      fallback
    );
    expect(r.id).toBe(dtoBase.id);
    expect(r.timestamp).toBe(dtoBase.timestamp);
  });
});
