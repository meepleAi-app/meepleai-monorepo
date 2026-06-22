import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';

import { LiveEventLog } from '../LiveEventLog';

function evt(o: Partial<DomainEventDto> = {}): DomainEventDto {
  return {
    id: o.id ?? `evt-${Math.random()}`,
    eventId: 'evt-id-1',
    eventType: 'Container.Restarted',
    aggregateType: 'Container',
    aggregateId: 'meepleai-api',
    userId: null,
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: '2026-06-03T14:23:08.412Z',
    loggedAt: '2026-06-03T14:23:08.412Z',
    ...o,
  };
}

describe('LiveEventLog rendering', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-03T14:23:10.000Z'));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders one row per event with ts + level + message', () => {
    render(<LiveEventLog events={[evt({ id: '1' })]} isStreaming isLoading={false} />);
    expect(screen.getByText('14:23:08.412')).toBeInTheDocument();
    expect(screen.getByText('INFO')).toBeInTheDocument();
    expect(screen.getByText(/container restarted aggregate=meepleai-api/)).toBeInTheDocument();
  });

  it('container has role=log and aria-live=polite', () => {
    render(<LiveEventLog events={[evt()]} isStreaming isLoading={false} />);
    const log = screen.getByRole('log');
    expect(log).toHaveAttribute('aria-live', 'polite');
  });

  it('limits visible rows to maxVisible (default 20)', () => {
    const events = Array.from({ length: 25 }, (_, i) => evt({ id: `e-${i}` }));
    render(<LiveEventLog events={events} isStreaming isLoading={false} />);
    expect(screen.getAllByTestId('live-event-row')).toHaveLength(20);
  });

  it('respects custom maxVisible prop', () => {
    const events = Array.from({ length: 10 }, (_, i) => evt({ id: `e-${i}` }));
    render(<LiveEventLog events={events} isStreaming isLoading={false} maxVisible={5} />);
    expect(screen.getAllByTestId('live-event-row')).toHaveLength(5);
  });

  it('shows empty placeholder when no events and streaming', () => {
    render(<LiveEventLog events={[]} isStreaming isLoading={false} />);
    expect(screen.getByText(/Nessun evento ricevuto/)).toBeInTheDocument();
  });

  it('shows disconnected placeholder when not streaming and no events', () => {
    render(<LiveEventLog events={[]} isStreaming={false} isLoading={false} />);
    expect(screen.getByText(/Stream offline/)).toBeInTheDocument();
  });

  it('shows loading placeholder when isLoading=true even with no events', () => {
    render(<LiveEventLog events={[]} isStreaming={false} isLoading />);
    expect(screen.getByText(/Caricamento eventi/)).toBeInTheDocument();
  });

  it('shows visible/total count in header', () => {
    const events = Array.from({ length: 30 }, (_, i) => evt({ id: `e-${i}` }));
    render(<LiveEventLog events={events} isStreaming isLoading={false} maxVisible={10} />);
    expect(screen.getByText(/10 \/ 30/)).toBeInTheDocument();
  });
});

describe('LiveEventLog level colors', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-03T14:23:10.000Z'));
  });
  afterEach(() => vi.useRealTimers());

  it.each([
    ['Container.Started', 'OK'],
    ['Container.Failed', 'ERR'],
    ['Service.Degraded', 'WARN'],
    ['Container.Restarting', 'INFO'],
  ] as const)('renders level label "%s" for eventType %s', (eventType, expectedLevel) => {
    render(<LiveEventLog events={[evt({ eventType })]} isStreaming isLoading={false} />);
    expect(screen.getByText(expectedLevel)).toBeInTheDocument();
  });
});

describe('LiveEventLog fade-out', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-03T14:23:10.000Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('marks events older than 30s with reduced opacity class', () => {
    const fresh = evt({ id: 'fresh', loggedAt: '2026-06-03T14:23:00.000Z' }); // 10s old
    const stale = evt({ id: 'stale', loggedAt: '2026-06-03T14:22:30.000Z' }); // 40s old

    render(<LiveEventLog events={[fresh, stale]} isStreaming isLoading={false} />);

    const rows = screen.getAllByTestId('live-event-row');
    expect(rows[0]).toHaveClass('opacity-100');
    expect(rows[1]).toHaveClass('opacity-50');
  });

  it('respects custom staleThresholdMs prop', () => {
    const fresh = evt({ id: 'fresh', loggedAt: '2026-06-03T14:23:00.000Z' }); // 10s old
    render(
      <LiveEventLog events={[fresh]} isStreaming isLoading={false} staleThresholdMs={5_000} />
    );
    // 10s old > 5s threshold → stale
    expect(screen.getByTestId('live-event-row')).toHaveClass('opacity-50');
  });
});
