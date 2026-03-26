/**
 * DuplicateWarningDialog - Tests
 * Issue #4167: Duplicate warning modal tests
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

import { DuplicateWarningDialog } from '../DuplicateWarningDialog';
import type { SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

describe('DuplicateWarningDialog', () => {
  const mockExistingGame: SharedGameDetail = {
    id: 'game-123',
    title: 'Existing Catan',
    bggId: 13,
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minAge: 10,
    playingTime: 90,
    description: 'Original description',
    imageUrl: null,
    thumbnailUrl: null,
    complexity: null,
    averageRating: null,
    ratingCount: null,
    categories: [],
    mechanics: [],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    status: 'Active',
  };

  const defaultProps = {
    open: true,
    existingGame: mockExistingGame,
    newGameTitle: 'New Catan Import',
    bggId: 13,
    onCancel: vi.fn(),
    onReplace: vi.fn(),
    onCreateAnyway: vi.fn(),
  };

  it('should render duplicate warning with existing and new game comparison', () => {
    render(<DuplicateWarningDialog {...defaultProps} />);

    expect(screen.getByText('Duplicate Game Detected')).toBeInTheDocument();
    expect(screen.getByText(/ID 13 esiste già/i)).toBeInTheDocument();

    // Existing game
    expect(screen.getByText('Existing Game')).toBeInTheDocument();
    expect(screen.getByText('Existing Catan')).toBeInTheDocument();
    expect(screen.getByText('Year: 1995')).toBeInTheDocument();
    expect(screen.getByText('Players: 3-4')).toBeInTheDocument();

    // New game
    expect(screen.getByText('New Import')).toBeInTheDocument();
    expect(screen.getByText('New Catan Import')).toBeInTheDocument();
  });

  it('should call onCancel when Cancel button clicked', async () => {
    const user = userEvent.setup();
    render(<DuplicateWarningDialog {...defaultProps} />);

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelBtn);

    expect(defaultProps.onCancel).toHaveBeenCalledOnce();
  });

  it('should call onReplace when Replace Existing button clicked', async () => {
    const user = userEvent.setup();
    render(<DuplicateWarningDialog {...defaultProps} />);

    const replaceBtn = screen.getByRole('button', { name: /replace existing/i });
    await user.click(replaceBtn);

    expect(defaultProps.onReplace).toHaveBeenCalledOnce();
  });

  it('should call onCreateAnyway when Create Anyway button clicked', async () => {
    const user = userEvent.setup();
    render(<DuplicateWarningDialog {...defaultProps} />);

    const createBtn = screen.getByRole('button', { name: /create anyway/i });
    await user.click(createBtn);

    expect(defaultProps.onCreateAnyway).toHaveBeenCalledOnce();
  });

  it('should not render if open=false', () => {
    render(<DuplicateWarningDialog {...defaultProps} open={false} />);

    expect(screen.queryByText('Duplicate Game Detected')).not.toBeInTheDocument();
  });

  it('should show warning message with action descriptions', () => {
    render(<DuplicateWarningDialog {...defaultProps} />);

    expect(screen.getByText(/what would you like to do/i)).toBeInTheDocument();
    expect(screen.getByText(/go back and change your selection/i)).toBeInTheDocument();
    expect(screen.getByText(/update the existing game/i)).toBeInTheDocument();
    expect(screen.getByText(/create as a separate entry/i)).toBeInTheDocument();
  });
});
