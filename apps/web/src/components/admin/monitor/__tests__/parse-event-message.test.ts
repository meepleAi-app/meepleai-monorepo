import { describe, it, expect } from 'vitest';

import { parseEventMessage } from '../parse-event-message';
import type { DomainEventDto } from '../live-event-types';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeEvent(overrides: Partial<DomainEventDto> = {}): DomainEventDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    eventId: '00000000-0000-0000-0000-000000000002',
    eventType: 'test.event',
    aggregateType: null,
    aggregateId: null,
    userId: null,
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: '2026-05-31T10:00:00Z',
    loggedAt: '2026-05-31T10:00:01Z',
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('parseEventMessage', () => {
  it('parseEventMessage_AgentCreated_ReturnsAgentColor', () => {
    const event = makeEvent({
      eventType: 'agent.created',
      aggregateType: 'Agent',
      aggregateId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
    });

    const result = parseEventMessage(event);

    expect(result.eventType).toBe('agent.created');
    const aggregateFragment = result.fragments.find(f => f.key === 'aggregateType');
    expect(aggregateFragment).toBeDefined();
    expect(aggregateFragment?.value).toBe('Agent');
    expect(aggregateFragment?.entityColor).toBe('agent');
  });

  it('parseEventMessage_KbDocIndexed_ReturnsKbColor', () => {
    const event = makeEvent({
      eventType: 'kb.doc.indexed',
      aggregateType: 'PdfDocument',
      aggregateId: 'b2c3d4e5-f6a7-8901-bcde-f12345678901',
    });

    const result = parseEventMessage(event);

    const aggregateFragment = result.fragments.find(f => f.key === 'aggregateType');
    expect(aggregateFragment).toBeDefined();
    expect(aggregateFragment?.entityColor).toBe('kb');
  });

  it('parseEventMessage_UnknownAggregate_ReturnsEventColor', () => {
    const event = makeEvent({
      eventType: 'unknown.happened',
      aggregateType: 'SomeUnknownEntity',
      aggregateId: 'c3d4e5f6-a7b8-9012-cdef-012345678902',
    });

    const result = parseEventMessage(event);

    const aggregateFragment = result.fragments.find(f => f.key === 'aggregateType');
    expect(aggregateFragment).toBeDefined();
    expect(aggregateFragment?.entityColor).toBe('event');
  });

  it('parseEventMessage_NullAggregateType_OmitsFragment', () => {
    const event = makeEvent({
      eventType: 'system.ping',
      aggregateType: null,
    });

    const result = parseEventMessage(event);

    const aggregateFragment = result.fragments.find(f => f.key === 'aggregateType');
    expect(aggregateFragment).toBeUndefined();
  });

  it('parseEventMessage_WithAggregateId_TruncatesGuid', () => {
    const event = makeEvent({
      eventType: 'session.started',
      aggregateType: 'Session',
      aggregateId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
    });

    const result = parseEventMessage(event);

    const idFragment = result.fragments.find(f => f.key === 'aggregateId');
    expect(idFragment).toBeDefined();
    // 8 chars, no dashes
    expect(idFragment?.value).toHaveLength(8);
    expect(idFragment?.value).not.toContain('-');
    expect(idFragment?.value).toBe('a1b2c3d4');
  });

  it('parseEventMessage_WithUserId_AddsUserFragmentWithSessionColor', () => {
    const event = makeEvent({
      eventType: 'chat.message.sent',
      userId: 'd4e5f6a7-b8c9-0123-def0-123456789012',
    });

    const result = parseEventMessage(event);

    const userFragment = result.fragments.find(f => f.key === 'user');
    expect(userFragment).toBeDefined();
    expect(userFragment?.entityColor).toBe('session');
    expect(userFragment?.value).toBe('d4e5f6a7');
  });

  it('parseEventMessage_AllFields_ReturnsAllFragmentsInOrder', () => {
    const event = makeEvent({
      eventType: 'agent.created',
      aggregateType: 'Agent',
      aggregateId: 'aaaabbbb-cccc-dddd-eeee-ffffabcd1234',
      userId: '11112222-3333-4444-5555-666677778888',
    });

    const result = parseEventMessage(event);

    expect(result.fragments).toHaveLength(3);
    expect(result.fragments[0].key).toBe('aggregateType');
    expect(result.fragments[1].key).toBe('aggregateId');
    expect(result.fragments[2].key).toBe('user');
  });

  it('parseEventMessage_NoOptionalFields_ReturnsOnlyEventType', () => {
    const event = makeEvent({
      eventType: 'system.heartbeat',
      aggregateType: null,
      aggregateId: null,
      userId: null,
    });

    const result = parseEventMessage(event);

    expect(result.eventType).toBe('system.heartbeat');
    expect(result.fragments).toHaveLength(0);
  });
});
