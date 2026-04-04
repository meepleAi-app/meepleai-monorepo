/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { HouseRulesSection } from '../HouseRulesSection';

// ============================================================================
// Mocks
// ============================================================================

const mockMutate = vi.fn();

const mockUseGameMemory = vi.fn().mockReturnValue({
  data: null,
  isLoading: false,
});

const mockUseAddHouseRule = vi.fn().mockReturnValue({
  mutate: mockMutate,
  isPending: false,
});

vi.mock('@/hooks/queries/useGameMemory', () => ({
  useGameMemory: (...args: unknown[]) => mockUseGameMemory(...args),
  useAddHouseRule: (...args: unknown[]) => mockUseAddHouseRule(...args),
}));

// ============================================================================
// Helpers
// ============================================================================

const GAME_ID = 'game-123';

function renderSection(gameId = GAME_ID) {
  return render(<HouseRulesSection gameId={gameId} />);
}

// ============================================================================
// Tests
// ============================================================================

describe('HouseRulesSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    mockUseGameMemory.mockReturnValue({
      data: null,
      isLoading: false,
    });

    mockUseAddHouseRule.mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    });
  });

  it('renders house rules list', () => {
    mockUseGameMemory.mockReturnValue({
      data: {
        id: 'mem-1',
        gameId: GAME_ID,
        ownerId: 'user-1',
        houseRules: [
          {
            description: 'No trading on first round',
            addedAt: '2026-03-20T00:00:00Z',
            source: 'User',
          },
          {
            description: 'Double points on weekends',
            addedAt: '2026-03-21T00:00:00Z',
            source: 'Admin',
          },
        ],
        notes: [],
      },
      isLoading: false,
    });

    renderSection();

    const items = screen.getAllByTestId('house-rule-item');
    expect(items).toHaveLength(2);
    expect(screen.getByText('No trading on first round')).toBeInTheDocument();
    expect(screen.getByText('Double points on weekends')).toBeInTheDocument();
    expect(screen.getByTestId('house-rules-count')).toHaveTextContent('2');
  });

  it('shows empty state when no rules', () => {
    mockUseGameMemory.mockReturnValue({
      data: {
        id: 'mem-1',
        gameId: GAME_ID,
        ownerId: 'user-1',
        houseRules: [],
        notes: [],
      },
      isLoading: false,
    });

    renderSection();

    expect(screen.getByTestId('house-rules-empty')).toBeInTheDocument();
    expect(screen.getByText('Nessuna regola di casa. Aggiungine una!')).toBeInTheDocument();
  });

  it('shows empty state when memory is null', () => {
    mockUseGameMemory.mockReturnValue({
      data: null,
      isLoading: false,
    });

    renderSection();

    expect(screen.getByTestId('house-rules-empty')).toBeInTheDocument();
  });

  it('shows loading skeleton', () => {
    mockUseGameMemory.mockReturnValue({
      data: null,
      isLoading: true,
    });

    renderSection();

    expect(screen.getByTestId('house-rules-skeleton')).toBeInTheDocument();
    expect(screen.getByTestId('house-rules-count')).toHaveTextContent('...');
  });

  it('adds house rule via input + click', async () => {
    mockMutate.mockImplementation((_desc: string, opts?: { onSuccess?: () => void }) => {
      opts?.onSuccess?.();
    });

    renderSection();

    const input = screen.getByTestId('house-rule-input');
    const addBtn = screen.getByTestId('add-house-rule-btn');

    await userEvent.type(input, 'New house rule');
    await userEvent.click(addBtn);

    expect(mockMutate).toHaveBeenCalledWith(
      'New house rule',
      expect.objectContaining({
        onSuccess: expect.any(Function),
      })
    );
  });

  it('adds house rule via Enter key', async () => {
    mockMutate.mockImplementation((_desc: string, opts?: { onSuccess?: () => void }) => {
      opts?.onSuccess?.();
    });

    renderSection();

    const input = screen.getByTestId('house-rule-input');

    await userEvent.type(input, 'Enter key rule');
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(mockMutate).toHaveBeenCalledWith(
      'Enter key rule',
      expect.objectContaining({
        onSuccess: expect.any(Function),
      })
    );
  });

  it('does not submit empty input', async () => {
    renderSection();

    const addBtn = screen.getByTestId('add-house-rule-btn');
    await userEvent.click(addBtn);

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('does not submit whitespace-only input', async () => {
    renderSection();

    const input = screen.getByTestId('house-rule-input');
    await userEvent.type(input, '   ');

    const addBtn = screen.getByTestId('add-house-rule-btn');
    await userEvent.click(addBtn);

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows source badges (User, Admin, System)', () => {
    mockUseGameMemory.mockReturnValue({
      data: {
        id: 'mem-1',
        gameId: GAME_ID,
        ownerId: 'user-1',
        houseRules: [
          { description: 'Rule A', addedAt: '2026-03-20T00:00:00Z', source: 'User' },
          { description: 'Rule B', addedAt: '2026-03-21T00:00:00Z', source: 'Admin' },
          { description: 'Rule C', addedAt: '2026-03-22T00:00:00Z', source: 'System' },
        ],
        notes: [],
      },
      isLoading: false,
    });

    renderSection();

    const badges = screen.getAllByTestId('source-badge');
    expect(badges).toHaveLength(3);
    expect(badges[0]).toHaveTextContent('User');
    expect(badges[1]).toHaveTextContent('Admin');
    expect(badges[2]).toHaveTextContent('System');
  });

  it('disables input when mutation is pending', () => {
    mockUseAddHouseRule.mockReturnValue({
      mutate: mockMutate,
      isPending: true,
    });

    renderSection();

    const input = screen.getByTestId('house-rule-input');
    expect(input).toBeDisabled();
  });

  it('clears input on successful add', async () => {
    mockMutate.mockImplementation((_desc: string, opts?: { onSuccess?: () => void }) => {
      opts?.onSuccess?.();
    });

    renderSection();

    const input = screen.getByTestId('house-rule-input') as HTMLInputElement;
    await userEvent.type(input, 'Will be cleared');
    await userEvent.click(screen.getByTestId('add-house-rule-btn'));

    await waitFor(() => {
      expect(input.value).toBe('');
    });
  });

  it('passes gameId to hooks', () => {
    renderSection('custom-game-id');

    expect(mockUseGameMemory).toHaveBeenCalledWith('custom-game-id');
    expect(mockUseAddHouseRule).toHaveBeenCalledWith('custom-game-id');
  });
});
