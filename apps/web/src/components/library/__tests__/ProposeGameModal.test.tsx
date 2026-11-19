/**
 * #5074: the proposal of a private game needs a description (decision of 2026-11-19). The modal says so before
 * anything is sent, and tells the user why the server refused a proposal.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';

import { ProposeGameModal } from '../ProposeGameModal';

function game(description: string | null): PrivateGameDto {
  return {
    id: 'game-1',
    title: 'Hand-made',
    description,
    minPlayers: 2,
    maxPlayers: 4,
  } as PrivateGameDto;
}

function renderModal(gameDto: PrivateGameDto, onPropose = vi.fn().mockResolvedValue(undefined)) {
  const onClose = vi.fn();
  const onEditGame = vi.fn();
  render(
    <ProposeGameModal
      isOpen
      onClose={onClose}
      game={gameDto}
      onPropose={onPropose}
      onEditGame={onEditGame}
    />
  );
  return { onPropose, onClose, onEditGame };
}

describe('ProposeGameModal (#5074)', () => {
  it.each([null, '', '   '])(
    'with the description %p, says so and does not let the proposal start',
    description => {
      const { onPropose } = renderModal(game(description));

      expect(screen.getByTestId('propose-missing-description')).toHaveTextContent(
        'The shared catalog needs one'
      );
      const submit = screen.getByRole('button', { name: 'Submit Proposal' });
      expect(submit).toBeDisabled();
      fireEvent.click(submit);
      expect(onPropose).not.toHaveBeenCalled();
    }
  );

  it('offers to edit the game when the description is missing', () => {
    const { onEditGame, onClose } = renderModal(game(null));

    fireEvent.click(screen.getByRole('button', { name: 'Edit game' }));

    expect(onClose).toHaveBeenCalled();
    expect(onEditGame).toHaveBeenCalledWith(expect.objectContaining({ id: 'game-1' }));
  });

  it('sends the proposal of a game with a description, with its notes', async () => {
    const { onPropose, onClose } = renderModal(game('A tile game.'));

    expect(screen.queryByTestId('propose-missing-description')).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/Notes for Reviewers/), {
      target: { value: 'Please add it' },
    });
    const submit = screen.getByRole('button', { name: 'Submit Proposal' });
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);

    await waitFor(() => expect(onPropose).toHaveBeenCalledWith('game-1', 'Please add it'));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it.each([
    [409, 'already waiting for review'],
    [422, 'Add a description to the game'],
    [500, 'could not be sent'],
  ])('shows why a %i refused the proposal, and stays open', async (statusCode, message) => {
    const onPropose = vi
      .fn()
      .mockRejectedValue(new ApiError({ message: 'refused', statusCode, endpoint: '/x' }));
    const { onClose } = renderModal(game('A tile game.'), onPropose);

    const submit = screen.getByRole('button', { name: 'Submit Proposal' });
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);

    expect(await screen.findByTestId('propose-error')).toHaveTextContent(message);
    expect(onClose).not.toHaveBeenCalled();
  });
});
