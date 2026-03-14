import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { TurnSummaryButton } from '../TurnSummaryButton';

const mockGetTurnSummary = vi.fn();

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    sessionTracking: {
      getTurnSummary: mockGetTurnSummary,
    },
  }),
}));

describe('TurnSummaryButton', () => {
  const defaultProps = {
    sessionId: 'session-123',
    lastNEvents: 20,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetTurnSummary.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders the Summarize Turn button', () => {
    render(<TurnSummaryButton {...defaultProps} />);
    expect(screen.getByRole('button', { name: /generate ai turn summary/i })).toBeInTheDocument();
    expect(screen.getByText('Summarize Turn')).toBeInTheDocument();
  });

  it('shows loading state when generating', async () => {
    const user = userEvent.setup();
    mockGetTurnSummary.mockImplementation(() => new Promise(() => {}));

    render(<TurnSummaryButton {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /generate ai turn summary/i }));

    expect(screen.getByText('Generating...')).toBeInTheDocument();
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('displays summary in dialog on success', async () => {
    const user = userEvent.setup();
    const summaryResult = {
      summaryEventId: 'evt-456',
      summary: 'The session was full of exciting dice rolls and strategic moves.',
      eventsAnalyzed: 15,
      generatedAt: '2026-03-14T10:30:00Z',
    };
    mockGetTurnSummary.mockResolvedValue(summaryResult);

    render(<TurnSummaryButton {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /generate ai turn summary/i }));

    await waitFor(() => {
      expect(screen.getByText('Turn Summary')).toBeInTheDocument();
    });
    expect(
      screen.getByText('The session was full of exciting dice rolls and strategic moves.')
    ).toBeInTheDocument();
    expect(screen.getByText(/AI analysis of 15 events/)).toBeInTheDocument();
  });

  it('displays error message on failure', async () => {
    const user = userEvent.setup();
    mockGetTurnSummary.mockRejectedValue(new Error('AI service unavailable'));

    render(<TurnSummaryButton {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /generate ai turn summary/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
    expect(screen.getByText('AI service unavailable')).toBeInTheDocument();
  });

  it('calls API with lastNEvents when no phase range provided', async () => {
    const user = userEvent.setup();
    mockGetTurnSummary.mockResolvedValue({
      summaryEventId: 'evt-789',
      summary: 'Summary text.',
      eventsAnalyzed: 10,
      generatedAt: '2026-03-14T11:00:00Z',
    });

    render(<TurnSummaryButton sessionId="session-456" lastNEvents={30} />);
    await user.click(screen.getByRole('button', { name: /generate ai turn summary/i }));

    await waitFor(() => {
      expect(mockGetTurnSummary).toHaveBeenCalledWith('session-456', {
        lastNEvents: 30,
        fromPhase: undefined,
        toPhase: undefined,
      });
    });
  });

  it('calls API with phase range when provided', async () => {
    const user = userEvent.setup();
    mockGetTurnSummary.mockResolvedValue({
      summaryEventId: 'evt-abc',
      summary: 'Phase summary.',
      eventsAnalyzed: 8,
      generatedAt: '2026-03-14T12:00:00Z',
    });

    render(<TurnSummaryButton sessionId="session-789" fromPhase={1} toPhase={3} />);
    await user.click(screen.getByRole('button', { name: /generate ai turn summary/i }));

    await waitFor(() => {
      expect(mockGetTurnSummary).toHaveBeenCalledWith('session-789', {
        lastNEvents: undefined,
        fromPhase: 1,
        toPhase: 3,
      });
    });
  });

  it('is disabled when disabled prop is true', () => {
    render(<TurnSummaryButton {...defaultProps} disabled />);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('re-enables button after API call completes', async () => {
    const user = userEvent.setup();
    mockGetTurnSummary.mockResolvedValue({
      summaryEventId: 'evt-def',
      summary: 'Done.',
      eventsAnalyzed: 5,
      generatedAt: '2026-03-14T13:00:00Z',
    });

    render(<TurnSummaryButton {...defaultProps} />);
    const button = screen.getByRole('button', { name: /generate ai turn summary/i });
    await user.click(button);

    await waitFor(() => {
      expect(screen.getByText('Turn Summary')).toBeInTheDocument();
    });
    expect(screen.getByText('Summarize Turn')).toBeInTheDocument();
  });
});
