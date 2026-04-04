/**
 * ScoreAssistant Component Tests
 * Issue #121 — AI Score Tracking
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ScoreAssistant } from '../ScoreAssistant';

// Mock the api module
const mockParseScore = vi.fn();
const mockConfirmScore = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      parseScore: (...args: unknown[]) => mockParseScore(...args),
      confirmScore: (...args: unknown[]) => mockConfirmScore(...args),
    },
  },
}));

describe('ScoreAssistant', () => {
  const sessionId = '550e8400-e29b-41d4-a716-446655440000';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render the input and submit button', () => {
    render(<ScoreAssistant sessionId={sessionId} />);
    expect(screen.getByTestId('score-assistant')).toBeInTheDocument();
    expect(screen.getByTestId('score-input')).toBeInTheDocument();
    expect(screen.getByTestId('score-submit')).toBeInTheDocument();
    expect(screen.getByText('Assistente punteggi')).toBeInTheDocument();
  });

  it('should disable submit when input is empty', () => {
    render(<ScoreAssistant sessionId={sessionId} />);
    expect(screen.getByTestId('score-submit')).toBeDisabled();
  });

  it('should call parseScore on submit', async () => {
    mockParseScore.mockResolvedValueOnce({
      status: 'recorded',
      playerName: 'Marco',
      playerId: '11111111-1111-1111-1111-111111111111',
      dimension: 'points',
      value: 5,
      round: 1,
      confidence: 0.95,
      requiresConfirmation: false,
      message: 'Score recorded: Marco scored 5 points in round 1.',
    });

    const onScoreRecorded = vi.fn();
    render(<ScoreAssistant sessionId={sessionId} onScoreRecorded={onScoreRecorded} />);

    const input = screen.getByTestId('score-input');
    fireEvent.change(input, { target: { value: 'Marco ha 5 punti' } });
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(mockParseScore).toHaveBeenCalledWith(sessionId, {
        message: 'Marco ha 5 punti',
        autoRecord: true,
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId('score-result')).toBeInTheDocument();
      expect(onScoreRecorded).toHaveBeenCalled();
    });
  });

  it('should show confirmation button when requiresConfirmation is true', async () => {
    mockParseScore.mockResolvedValueOnce({
      status: 'parsed',
      playerName: 'Marco',
      playerId: '11111111-1111-1111-1111-111111111111',
      dimension: 'points',
      value: 5,
      round: 1,
      confidence: 0.6,
      requiresConfirmation: true,
      message: 'Detected: Marco scored 5 points. Please confirm.',
    });

    render(<ScoreAssistant sessionId={sessionId} />);

    fireEvent.change(screen.getByTestId('score-input'), {
      target: { value: 'Marco forse 5 punti' },
    });
    fireEvent.submit(screen.getByTestId('score-input').closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('score-confirm')).toBeInTheDocument();
    });
  });

  it('should call confirmScore when confirm button is clicked', async () => {
    const parsedResult = {
      status: 'parsed' as const,
      playerName: 'Marco',
      playerId: '11111111-1111-1111-1111-111111111111',
      dimension: 'points',
      value: 5,
      round: 1,
      confidence: 0.6,
      requiresConfirmation: true,
      message: 'Detected: Marco scored 5 points. Please confirm.',
    };
    mockParseScore.mockResolvedValueOnce(parsedResult);
    mockConfirmScore.mockResolvedValueOnce(undefined);

    const onScoreRecorded = vi.fn();
    render(<ScoreAssistant sessionId={sessionId} onScoreRecorded={onScoreRecorded} />);

    fireEvent.change(screen.getByTestId('score-input'), { target: { value: 'Marco 5' } });
    fireEvent.submit(screen.getByTestId('score-input').closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('score-confirm')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('score-confirm'));

    await waitFor(() => {
      expect(mockConfirmScore).toHaveBeenCalledWith(sessionId, {
        playerId: '11111111-1111-1111-1111-111111111111',
        dimension: 'points',
        value: 5,
        round: 1,
      });
      expect(onScoreRecorded).toHaveBeenCalled();
    });
  });

  it('should show ambiguous candidates when status is ambiguous', async () => {
    mockParseScore.mockResolvedValueOnce({
      status: 'ambiguous',
      playerName: 'M',
      dimension: 'points',
      value: 5,
      round: 1,
      confidence: 0.7,
      requiresConfirmation: true,
      message: "Multiple players match 'M'. Please specify.",
      ambiguousCandidates: ['Marco', 'Maria'],
    });

    render(<ScoreAssistant sessionId={sessionId} />);

    fireEvent.change(screen.getByTestId('score-input'), { target: { value: 'M ha 5 punti' } });
    fireEvent.submit(screen.getByTestId('score-input').closest('form')!);

    await waitFor(() => {
      expect(screen.getByText('Marco')).toBeInTheDocument();
      expect(screen.getByText('Maria')).toBeInTheDocument();
    });
  });

  it('should show error state on API failure', async () => {
    mockParseScore.mockRejectedValueOnce(new Error('Network error'));

    render(<ScoreAssistant sessionId={sessionId} />);

    fireEvent.change(screen.getByTestId('score-input'), { target: { value: 'test' } });
    fireEvent.submit(screen.getByTestId('score-input').closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('score-error')).toBeInTheDocument();
    });
  });

  it('should dismiss result on dismiss button click', async () => {
    mockParseScore.mockResolvedValueOnce({
      status: 'unrecognized',
      confidence: 0.1,
      requiresConfirmation: false,
      message: 'No score information detected.',
    });

    render(<ScoreAssistant sessionId={sessionId} />);

    fireEvent.change(screen.getByTestId('score-input'), { target: { value: 'hello' } });
    fireEvent.submit(screen.getByTestId('score-input').closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('score-result')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('score-dismiss'));

    expect(screen.queryByTestId('score-result')).not.toBeInTheDocument();
  });
});
