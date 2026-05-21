/**
 * ResumeBooksList Component Tests
 *
 * Tests for the ResumeBooksList component (Phase E2 of the gamebook
 * multi-book generalization). Renders per-book progress rows on the play
 * page so the user can resume each book they have started.
 *
 * Contract:
 *   - Non-empty progress → one row per book with name + lastLocation + resume button.
 *   - Empty progress → "Nessun libro in corso." copy.
 *   - Resume button fires `onResume(bookId)`.
 */

import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ResumeBooksList } from '@/components/features/gamebook/ResumeBooksList';

describe('ResumeBooksList', () => {
  const progress = [
    {
      bookId: 'b1',
      bookName: 'Storybook',
      lastLocation: '§289',
      lastVisitedAt: '2026-05-19T10:00:00Z',
    },
    {
      bookId: 'b2',
      bookName: 'Encounter Book',
      lastLocation: 'E7',
      lastVisitedAt: '2026-05-19T11:00:00Z',
    },
  ];

  it('renders a row per book with last location', () => {
    render(<ResumeBooksList progress={progress} onResume={vi.fn()} />);
    expect(screen.getByText('Storybook')).toBeInTheDocument();
    expect(screen.getByText('§289')).toBeInTheDocument();
    expect(screen.getByText('Encounter Book')).toBeInTheDocument();
    expect(screen.getByText('E7')).toBeInTheDocument();
  });

  it('renders empty state when no progress', () => {
    render(<ResumeBooksList progress={[]} onResume={vi.fn()} />);
    expect(screen.getByText(/nessun libro/i)).toBeInTheDocument();
  });

  it('fires onResume with the book id when resume button is clicked', async () => {
    const onResume = vi.fn();
    const user = userEvent.setup();
    render(<ResumeBooksList progress={progress} onResume={onResume} />);
    const buttons = screen.getAllByRole('button', { name: /riprendi/i });
    await user.click(buttons[1]);
    expect(onResume).toHaveBeenCalledTimes(1);
    expect(onResume).toHaveBeenCalledWith('b2');
  });
});
