import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';

export type LogLevel = 'INFO' | 'WARN' | 'ERR' | 'OK';

const OK_SUFFIXES = ['.Created', '.Started', '.Healthy'] as const;
const ERR_SUFFIXES = ['.Failed', '.Unhealthy', '.Crashed'] as const;
const WARN_SUFFIXES = ['.Degraded', '.SlowResponse'] as const;

const INVALID_TIMESTAMP = '--:--:--.---';

export function deriveLogLevel(eventType: string): LogLevel {
  if (OK_SUFFIXES.some(s => eventType.endsWith(s))) return 'OK';
  if (ERR_SUFFIXES.some(s => eventType.endsWith(s))) return 'ERR';
  if (WARN_SUFFIXES.some(s => eventType.endsWith(s))) return 'WARN';
  return 'INFO';
}

export function formatLogTimestamp(iso: string): string {
  const ms = Date.parse(iso);
  if (Number.isNaN(ms)) return INVALID_TIMESTAMP;
  const d = new Date(ms);
  const pad2 = (n: number) => String(n).padStart(2, '0');
  const pad3 = (n: number) => String(n).padStart(3, '0');
  return `${pad2(d.getUTCHours())}:${pad2(d.getUTCMinutes())}:${pad2(d.getUTCSeconds())}.${pad3(d.getUTCMilliseconds())}`;
}

export function buildLogMessage(event: DomainEventDto): string {
  const aggregateLower = event.aggregateType ? event.aggregateType.toLowerCase() : 'event';
  const dotIdx = event.eventType.lastIndexOf('.');
  const verb = dotIdx >= 0 ? event.eventType.slice(dotIdx + 1).toLowerCase() : event.eventType;
  const parts = [aggregateLower, verb];
  if (event.aggregateId) parts.push(`aggregate=${event.aggregateId}`);
  return parts.join(' ');
}

export function isStaleEvent(loggedAt: string, now: number, thresholdMs: number): boolean {
  const ms = Date.parse(loggedAt);
  if (Number.isNaN(ms)) return true;
  return now - ms > thresholdMs;
}
