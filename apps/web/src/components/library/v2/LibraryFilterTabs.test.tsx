import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';

describe('LibraryFilterTabs', () => {
  it('renders 3 tabs with counts', () => {
    render(
      <LibraryFilterTabs
        value="all"
        onChange={() => {}}
        counts={{ all: 12, owned: 8, wishlist: 4 }}
      />
    );
    expect(screen.getByRole('tab', { name: /tutti.*12/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /possedut.*8/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /wishlist.*4/i })).toBeInTheDocument();
  });

  it('marks active tab with aria-selected', () => {
    render(
      <LibraryFilterTabs
        value="owned"
        onChange={() => {}}
        counts={{ all: 1, owned: 1, wishlist: 0 }}
      />
    );
    expect(screen.getByRole('tab', { name: /possedut/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onChange when tab clicked', () => {
    const onChange = vi.fn<(next: LibraryFilter) => void>();
    render(
      <LibraryFilterTabs
        value="all"
        onChange={onChange}
        counts={{ all: 0, owned: 0, wishlist: 0 }}
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /wishlist/i }));
    expect(onChange).toHaveBeenCalledWith('wishlist');
  });
});
