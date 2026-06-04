import { describe, expect, it } from 'vitest';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';

import {
  buildLogMessage,
  deriveLogLevel,
  formatLogTimestamp,
  isStaleEvent,
} from '../_utils/event-log-mapper';

describe('deriveLogLevel', () => {
  it.each([
    ['Container.Created', 'OK'],
    ['Container.Started', 'OK'],
    ['Service.Healthy', 'OK'],
    ['Container.Updated', 'INFO'],
    ['Container.Restarting', 'INFO'],
    ['UnknownAggregate.SomethingElse', 'INFO'],
    ['Service.Degraded', 'WARN'],
    ['Service.SlowResponse', 'WARN'],
    ['Container.Failed', 'ERR'],
    ['Service.Unhealthy', 'ERR'],
    ['Container.Crashed', 'ERR'],
  ] as const)('maps %s -> %s', (eventType, expected) => {
    expect(deriveLogLevel(eventType)).toBe(expected);
  });
});

describe('formatLogTimestamp', () => {
  it('formats ISO timestamp as HH:mm:ss.SSS in UTC', () => {
    expect(formatLogTimestamp('2026-06-03T14:23:08.412Z')).toBe('14:23:08.412');
  });

  it('pads single-digit components', () => {
    expect(formatLogTimestamp('2026-06-03T01:02:03.040Z')).toBe('01:02:03.040');
  });

  it('returns placeholder for invalid input', () => {
    expect(formatLogTimestamp('not-a-date')).toBe('--:--:--.---');
  });
});

function makeEvent(overrides: Partial<DomainEventDto> = {}): DomainEventDto {
  return {
    id: 'evt-1',
    eventId: 'evt-id-1',
    eventType: 'Container.Restarted',
    aggregateType: 'Container',
    aggregateId: 'meepleai-api',
    userId: null,
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: '2026-06-03T14:23:08.412Z',
    loggedAt: '2026-06-03T14:23:08.412Z',
    ...overrides,
  };
}

describe('buildLogMessage', () => {
  it('uses lowercase aggregate type + event suffix + aggregateId', () => {
    expect(buildLogMessage(makeEvent())).toBe('container restarted aggregate=meepleai-api');
  });

  it('falls back to full eventType when no dot suffix', () => {
    expect(buildLogMessage(makeEvent({ eventType: 'OpaqueEvent' }))).toBe(
      'container OpaqueEvent aggregate=meepleai-api'
    );
  });

  it('omits aggregate= when aggregateId is null', () => {
    expect(buildLogMessage(makeEvent({ aggregateId: null }))).toBe('container restarted');
  });

  it('omits aggregate= when aggregateId is empty string', () => {
    expect(buildLogMessage(makeEvent({ aggregateId: '' }))).toBe('container restarted');
  });

  it('uses "event" as fallback when aggregateType is null', () => {
    expect(buildLogMessage(makeEvent({ aggregateType: null }))).toBe(
      'event restarted aggregate=meepleai-api'
    );
  });
});

describe('isStaleEvent', () => {
  const now = Date.parse('2026-06-03T14:23:38.000Z');

  it('returns false for events within threshold', () => {
    expect(isStaleEvent('2026-06-03T14:23:08.000Z', now, 30_000)).toBe(false);
  });

  it('returns true for events older than threshold', () => {
    expect(isStaleEvent('2026-06-03T14:23:07.999Z', now, 30_000)).toBe(true);
  });

  it('returns true for invalid timestamps (safe default - fade out)', () => {
    expect(isStaleEvent('garbage', now, 30_000)).toBe(true);
  });
});
