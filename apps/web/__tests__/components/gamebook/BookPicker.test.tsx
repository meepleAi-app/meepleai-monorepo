/**
 * BookPicker Component Tests
 *
 * Tests for the BookPicker component (Phase E1 of the gamebook multi-book
 * generalization). Used inside the photo-translate form when a game has
 * 2+ narrative books — the user picks which book this photo belongs to.
 *
 * Contract:
 *   - When only 1 book is supplied: render nothing (hidden — auto-selected).
 *   - When 2+ books are supplied: render a radio group with one button per book.
 *   - On click, fire `onChange(bookId)`.
 */

import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BookPicker, type BookPickerOption } from '@/components/features/gamebook/BookPicker';

const narrativeBooks: BookPickerOption[] = [
  { id: 'b1', displayName: 'Storybook', roles: 4 /* Narrative */ },
  { id: 'b2', displayName: 'Encounter Book', roles: 8 /* Encounter */ },
];

describe('BookPicker', () => {
  it('renders nothing when only 1 narrative book', () => {
    const { container } = render(
      <BookPicker books={[narrativeBooks[0]]} value="b1" onChange={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('renders book options when 2+ narrative books', () => {
    render(<BookPicker books={narrativeBooks} value="b1" onChange={vi.fn()} />);
    expect(screen.getByText('Storybook')).toBeInTheDocument();
    expect(screen.getByText('Encounter Book')).toBeInTheDocument();
  });

  it('calls onChange when user selects a book', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<BookPicker books={narrativeBooks} value="b1" onChange={onChange} />);
    await user.click(screen.getByText('Encounter Book'));
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('b2');
  });
});
