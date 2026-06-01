/**
 * LiveEventLog Component Tests — Task 3.4
 * Issue #1718: F4.1 Monitor LiveEventLog
 *
 * TDD: 7 scenarios covering panel header, virtualized rows, empty state,
 * error state, pause/resume toggle, onEventClick callback, and level
 * color-class application.
 *
 * Coverage target: ≥85% of LiveEventLog.tsx
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import type { DomainEventDto } from '../live-event-types';
import type { UseLiveEventsResult } from '../use-live-events';

// ============================================================================
// Mock useLiveEvents (module-level)
// ============================================================================

vi.mock('../use-live-events', () => ({
  useLiveEvents: vi.fn(),
}));

// Dynamically import so the mock is already in place
import { useLiveEvents } from '../use-live-events';
import { LiveEventLog } from '../LiveEventLog';

const mockUseLiveEvents = useLiveEvents as ReturnType<typeof vi.fn>;

// ============================================================================
// Helpers
// ============================================================================

function makeEvent(overrides: Partial<DomainEventDto> = {}): DomainEventDto {
  return {
    id: 'aaaaaaaa-0000-0000-0000-000000000001',
    eventId: 'bbbbbbbb-0000-0000-0000-000000000001',
    eventType: 'agent.created',
    aggregateType: 'Agent',
    aggregateId: 'cccccccc-0000-0000-0000-000000000001',
    userId: 'dddddddd-0000-0000-0000-000000000001',
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: '2026-05-31T14:23:08.412Z',
    loggedAt: '2026-05-31T14:23:08.412Z',
    ...overrides,
  };
}

function makeHookResult(overrides: Partial<UseLiveEventsResult> = {}): UseLiveEventsResult {
  return {
    events: [],
    isLoading: false,
    isStreaming: true,
    error: null,
    pause: vi.fn(),
    resume: vi.fn(),
    refetch: vi.fn(),
    ...overrides,
  };
}

// ============================================================================
// Mock react-window List to avoid jsdom virtualization issues
// ============================================================================

vi.mock('react-window', () => ({
  List: vi.fn(
    ({
      rowComponent: RowComponent,
      rowCount,
      rowProps,
    }: {
      rowComponent: React.ComponentType<{
        ariaAttributes: { 'aria-posinset': number; 'aria-setsize': number; role: 'listitem' };
        index: number;
        style: React.CSSProperties;
        events: DomainEventDto[];
        onEventClick?: (event: DomainEventDto) => void;
      }>;
      rowCount: number;
      rowProps: { events: DomainEventDto[]; onEventClick?: (event: DomainEventDto) => void };
    }) => {
      const { events, onEventClick } = rowProps;
      return (
        <div data-testid="virtualized-list" role="log" aria-live="polite">
          {events.slice(0, rowCount).map((evt, idx) => (
            <RowComponent
              key={evt.id}
              ariaAttributes={{
                'aria-posinset': idx + 1,
                'aria-setsize': rowCount,
                role: 'listitem',
              }}
              index={idx}
              style={{}}
              events={events}
              onEventClick={onEventClick}
            />
          ))}
        </div>
      );
    }
  ),
}));

// Need React import for JSX in mock
import React from 'react';

// ============================================================================
// Tests
// ============================================================================

describe('LiveEventLog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ── Test 1: Panel header ──────────────────────────────────────────────────
  it('LiveEventLog_Renders_PanelHeader', () => {
    mockUseLiveEvents.mockReturnValue(makeHookResult());

    render(<LiveEventLog />);

    // h3 with "Live event stream"
    const heading = screen.getByRole('heading', { level: 3 });
    expect(heading).toBeInTheDocument();
    expect(heading).toHaveTextContent('Live event stream');

    // endpoint slug
    expect(heading.textContent).toContain('/api/v1/admin/events/stream');

    // live pill
    const pill = screen.getByText(/streaming.*SSE/i);
    expect(pill).toBeInTheDocument();
  });

  // ── Test 2: Virtualized rows ──────────────────────────────────────────────
  it('LiveEventLog_WithEvents_RendersVirtualizedRows', () => {
    const events = Array.from({ length: 5 }, (_, i) =>
      makeEvent({
        id: `event-id-${i}`,
        eventId: `evt-${i}`,
        eventType: 'agent.created',
        occurredAt: `2026-05-31T14:23:0${i}.000Z`,
        loggedAt: `2026-05-31T14:23:0${i}.000Z`,
      })
    );
    mockUseLiveEvents.mockReturnValue(makeHookResult({ events }));

    render(<LiveEventLog />);

    // The virtualized list is rendered
    expect(screen.getByTestId('virtualized-list')).toBeInTheDocument();

    // At least the first event type is visible
    const rows = screen.getAllByRole('listitem');
    expect(rows.length).toBeGreaterThanOrEqual(1);

    // The event type is displayed
    expect(screen.getAllByText(/agent\.created/).length).toBeGreaterThanOrEqual(1);
  });

  // ── Test 3: Empty state ───────────────────────────────────────────────────
  it('LiveEventLog_EmptyState_RendersPlaceholder', () => {
    mockUseLiveEvents.mockReturnValue(makeHookResult({ events: [], isLoading: false }));

    render(<LiveEventLog />);

    // Empty state placeholder
    expect(screen.getByText(/In ascolto/i)).toBeInTheDocument();
    expect(screen.getByText(/0 eventi/i)).toBeInTheDocument();
  });

  // ── Test 4: Error state ───────────────────────────────────────────────────
  it('LiveEventLog_ErrorState_RendersBannerAndRetry', () => {
    const refetch = vi.fn();
    mockUseLiveEvents.mockReturnValue(
      makeHookResult({
        error: new Error('Backfill failed: 503'),
        refetch,
      })
    );

    render(<LiveEventLog />);

    // Error banner contains error message
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/Backfill failed: 503/i)).toBeInTheDocument();

    // Retry button
    const retryBtn = screen.getByRole('button', { name: /retry/i });
    expect(retryBtn).toBeInTheDocument();

    // Click retry → refetch called
    fireEvent.click(retryBtn);
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  // ── Test 5: Pause / Resume toggle ────────────────────────────────────────
  it('LiveEventLog_PauseButton_TogglesPauseResume', () => {
    const pause = vi.fn();
    const resume = vi.fn();

    // Streaming state
    const streamingResult = makeHookResult({ isStreaming: true, pause, resume });
    mockUseLiveEvents.mockReturnValue(streamingResult);

    const { rerender } = render(<LiveEventLog />);

    // Pause button present when streaming
    const pauseBtn = screen.getByRole('button', { name: /pause stream/i });
    expect(pauseBtn).toBeInTheDocument();

    fireEvent.click(pauseBtn);
    expect(pause).toHaveBeenCalledTimes(1);

    // Re-render with paused state (isStreaming=false)
    const pausedResult = makeHookResult({ isStreaming: false, pause, resume });
    mockUseLiveEvents.mockReturnValue(pausedResult);
    rerender(<LiveEventLog />);

    // Resume button present when paused
    const resumeBtn = screen.getByRole('button', { name: /resume/i });
    expect(resumeBtn).toBeInTheDocument();

    fireEvent.click(resumeBtn);
    expect(resume).toHaveBeenCalledTimes(1);
  });

  // ── Test 6: onEventClick callback ────────────────────────────────────────
  it('LiveEventLog_EventClick_CallsOnEventClick', () => {
    const event = makeEvent({ id: 'click-event-id', eventType: 'kb.doc.indexed' });
    const onEventClick = vi.fn();

    mockUseLiveEvents.mockReturnValue(makeHookResult({ events: [event] }));

    render(<LiveEventLog onEventClick={onEventClick} />);

    // Find the row and click it
    const rows = screen.getAllByRole('listitem');
    expect(rows.length).toBeGreaterThanOrEqual(1);
    fireEvent.click(rows[0]);

    expect(onEventClick).toHaveBeenCalledTimes(1);
    expect(onEventClick).toHaveBeenCalledWith(event);
  });

  // ── Test 7: Level color class ─────────────────────────────────────────────
  it('LiveEventLog_LevelColor_AppliesEntityClass', () => {
    const errorEvent = makeEvent({
      id: 'err-event-id',
      eventType: 'chat.session.failed',
      aggregateType: 'ChatSession',
    });

    mockUseLiveEvents.mockReturnValue(makeHookResult({ events: [errorEvent] }));

    render(<LiveEventLog />);

    // Find the level badge — should contain "ERR" for .failed event
    const levelBadge = screen.getByText('ERR');
    expect(levelBadge).toBeInTheDocument();

    // Should have the error color class
    expect(levelBadge).toHaveClass('text-entity-event');
  });
});
