import { describe, expect, it } from 'vitest';
import {
  ActivityFeedResponseSchema,
  ActivityItemDtoSchema,
  type ActivityItemDto,
} from '../activity.schemas';

describe('activity schemas', () => {
  const validItem: ActivityItemDto = {
    id: '550e8400-e29b-41d4-a716-446655440000',
    eventId: '6ba7b810-9dad-11d1-80b4-00c04fd430c8',
    eventType: 'agent.created',
    userId: '6ba7b811-9dad-11d1-80b4-00c04fd430c8',
    entityType: 'Agent',
    entityId: '6ba7b812-9dad-11d1-80b4-00c04fd430c8',
    title: 'Catan Tutor',
    timestamp: '2026-05-28T11:00:00+00:00',
    loggedAt: '2026-05-28T11:00:01+00:00',
    payloadVersion: 1,
  };

  it('accepts a full populated item', () => {
    expect(() => ActivityItemDtoSchema.parse(validItem)).not.toThrow();
  });

  it('accepts title=null', () => {
    const parsed = ActivityItemDtoSchema.parse({ ...validItem, title: null });
    expect(parsed.title).toBeNull();
  });

  it('rejects items missing eventId (BE-3 contract has eventId for idempotency)', () => {
    const { eventId, ...rest } = validItem;
    expect(() => ActivityItemDtoSchema.parse(rest)).toThrow();
  });

  it('rejects items with a "payload" field (BE-3 explicitly does not project payload)', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, payload: { foo: 'bar' } })).toThrow();
  });

  it('rejects items with a "message" field (no message in BE-3 DTO)', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, message: 'hi' })).toThrow();
  });

  it('rejects malformed timestamp', () => {
    expect(() => ActivityItemDtoSchema.parse({ ...validItem, timestamp: 'yesterday' })).toThrow();
  });

  it('parses the envelope { success, items, count }', () => {
    const envelope = { success: true, items: [validItem], count: 1 };
    const parsed = ActivityFeedResponseSchema.parse(envelope);
    expect(parsed.items).toHaveLength(1);
    expect(parsed.count).toBe(1);
  });

  it('rejects envelope without success flag', () => {
    expect(() => ActivityFeedResponseSchema.parse({ items: [], count: 0 })).toThrow();
  });
});
