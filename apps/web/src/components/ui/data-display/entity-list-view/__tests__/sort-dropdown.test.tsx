/**
 * Tests for SortDropdown component
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SortDropdown } from '../components/sort-dropdown';
import { Star, TrendingUp } from 'lucide-react';
import type { SortOption } from '../entity-list-view.types';
import { vi } from 'vitest';

interface MockItem {
  title: string;
  rating: number;
}

const mockSortOptions: SortOption<MockItem>[] = [
  {
    value: 'rating',
    label: 'Rating',
    icon: Star,
    compareFn: (a, b) => b.rating - a.rating,
  },
  {
    value: 'popularity',
    label: 'Popularity',
    icon: TrendingUp,
    compareFn: (a, b) => a.title.localeCompare(b.title),
  },
];

describe('SortDropdown', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render current sort option', () => {
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      expect(screen.getByLabelText(/sort by rating/i)).toBeInTheDocument();
    });

    it('should show current option label', () => {
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      expect(screen.getByText('Rating')).toBeInTheDocument();
    });

    it('should render icon for current option', () => {
      const { container } = render(
        <SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />
      );

      const icons = container.querySelectorAll('svg');
      expect(icons.length).toBeGreaterThan(0);
    });
  });

  describe('Dropdown Interaction', () => {
    it('should open dropdown on button click', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button');
      await user.click(button);

      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });

    it('should close dropdown on second click', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button');
      await user.click(button);
      await user.click(button);

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    });

    it('should render all sort options when open', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      await user.click(screen.getByRole('button'));

      const options = screen.getAllByRole('option');
      expect(options).toHaveLength(2);
    });

    it('should mark current option as selected', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      await user.click(screen.getByRole('button'));

      const ratingOption = screen.getByRole('option', { name: /rating/i });
      expect(ratingOption).toHaveAttribute('aria-selected', 'true');
    });
  });

  describe('Selection', () => {
    it('should call onChange when option clicked', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      await user.click(screen.getByRole('button'));
      await user.click(screen.getByRole('option', { name: /popularity/i }));

      expect(mockOnChange).toHaveBeenCalledWith('popularity');
    });

    it('should close dropdown after selection', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      await user.click(screen.getByRole('button'));
      await user.click(screen.getByRole('option', { name: /popularity/i }));

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    });
  });

  describe('Keyboard Navigation', () => {
    it('should close on Escape key', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      await user.click(screen.getByRole('button'));
      await user.keyboard('{Escape}');

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have aria-haspopup', () => {
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-haspopup', 'listbox');
    });

    it('should have aria-expanded reflecting state', async () => {
      const user = userEvent.setup();
      render(<SortDropdown value="rating" options={mockSortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-expanded', 'false');

      await user.click(button);
      expect(button).toHaveAttribute('aria-expanded', 'true');
    });
  });
});
