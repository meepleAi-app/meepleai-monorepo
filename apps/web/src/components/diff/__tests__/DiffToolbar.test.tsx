/**
 * Unit tests for DiffToolbar component
 * Tests toolbar with statistics, search, and navigation
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffToolbar } from '../DiffToolbar';
import type { DiffStatistics as DiffStats } from '@/lib/diffProcessor';

describe('DiffToolbar', () => {
  const mockStatistics: DiffStats = {
    added: 10,
    deleted: 5,
    modified: 3,
    unchanged: 50,
    totalLines: 68,
  };

  const defaultProps = {
    statistics: mockStatistics,
    searchQuery: '',
    onSearchChange: jest.fn(),
    currentChangeIndex: 0,
    totalChanges: 5,
    onNavigatePrev: jest.fn(),
    onNavigateNext: jest.fn(),
    showNavigation: true,
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render toolbar with statistics section', () => {
      render(<DiffToolbar {...defaultProps} />);

      expect(screen.getByText('+10')).toBeInTheDocument();
      expect(screen.getByText('Added')).toBeInTheDocument();
    });

    it('should render search section when showNavigation is true', () => {
      render(<DiffToolbar {...defaultProps} />);

      expect(screen.getByPlaceholderText('Search in diff...')).toBeInTheDocument();
    });

    it('should render navigation section when showNavigation is true', () => {
      render(<DiffToolbar {...defaultProps} />);

      expect(screen.getByLabelText('Previous change')).toBeInTheDocument();
      expect(screen.getByLabelText('Next change')).toBeInTheDocument();
    });

    it('should not render search when showNavigation is false', () => {
      render(<DiffToolbar {...defaultProps} showNavigation={false} />);

      expect(screen.queryByPlaceholderText('Search in diff...')).not.toBeInTheDocument();
    });

    it('should not render navigation when showNavigation is false', () => {
      render(<DiffToolbar {...defaultProps} showNavigation={false} />);

      expect(screen.queryByLabelText('Previous change')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Next change')).not.toBeInTheDocument();
    });
  });

  describe('Compact Mode', () => {
    it('should apply compact class when compact is true', () => {
      const { container } = render(<DiffToolbar {...defaultProps} compact={true} />);

      expect(container.querySelector('.diff-toolbar--compact')).toBeInTheDocument();
    });

    it('should not apply compact class when compact is false', () => {
      const { container } = render(<DiffToolbar {...defaultProps} compact={false} />);

      expect(container.querySelector('.diff-toolbar--compact')).not.toBeInTheDocument();
    });

    it('should pass compact prop to DiffStatistics', () => {
      render(<DiffToolbar {...defaultProps} compact={true} />);

      // In compact mode, labels should not be shown
      expect(screen.queryByText('Added')).not.toBeInTheDocument();
      expect(screen.getByText('+10')).toBeInTheDocument();
    });
  });

  describe('Search Integration', () => {
    it('should render DiffSearchInput with correct props', () => {
      render(<DiffToolbar {...defaultProps} searchQuery="test" />);

      const searchInput = screen.getByRole('textbox');

      expect(searchInput).toHaveValue('test');
    });

    it('should pass matchCount to search input', () => {
      render(<DiffToolbar {...defaultProps} totalChanges={10} />);

      expect(screen.getByText('10 matches')).toBeInTheDocument();
    });

    it('should call onSearchChange when search input changes', async () => {
      const user = userEvent.setup({ delay: null });
      const mockOnSearchChange = jest.fn();

      render(<DiffToolbar {...defaultProps} onSearchChange={mockOnSearchChange} />);

      const clearButton = screen.getByLabelText('Clear search');
      await user.click(clearButton);

      expect(mockOnSearchChange).toHaveBeenCalledWith('');
    });
  });

  describe('Navigation Integration', () => {
    it('should render DiffNavigationControls with correct props', () => {
      render(<DiffToolbar {...defaultProps} currentChangeIndex={2} totalChanges={5} />);

      expect(screen.getByText('3 / 5 changes')).toBeInTheDocument();
    });

    it('should call onNavigatePrev when prev button clicked', async () => {
      const user = userEvent.setup();
      const mockOnNavigatePrev = jest.fn();

      render(
        <DiffToolbar
          {...defaultProps}
          currentChangeIndex={1}
          onNavigatePrev={mockOnNavigatePrev}
        />
      );

      const prevButton = screen.getByLabelText('Previous change');
      await user.click(prevButton);

      expect(mockOnNavigatePrev).toHaveBeenCalledTimes(1);
    });

    it('should call onNavigateNext when next button clicked', async () => {
      const user = userEvent.setup();
      const mockOnNavigateNext = jest.fn();

      render(
        <DiffToolbar
          {...defaultProps}
          currentChangeIndex={0}
          onNavigateNext={mockOnNavigateNext}
        />
      );

      const nextButton = screen.getByLabelText('Next change');
      await user.click(nextButton);

      expect(mockOnNavigateNext).toHaveBeenCalledTimes(1);
    });
  });

  describe('CSS Classes', () => {
    it('should apply correct section classes', () => {
      const { container } = render(<DiffToolbar {...defaultProps} />);

      expect(container.querySelector('.diff-toolbar-section--stats')).toBeInTheDocument();
      expect(container.querySelector('.diff-toolbar-section--search')).toBeInTheDocument();
      expect(container.querySelector('.diff-toolbar-section--navigation')).toBeInTheDocument();
    });

    it('should only show stats section when navigation is hidden', () => {
      const { container } = render(<DiffToolbar {...defaultProps} showNavigation={false} />);

      expect(container.querySelector('.diff-toolbar-section--stats')).toBeInTheDocument();
      expect(container.querySelector('.diff-toolbar-section--search')).not.toBeInTheDocument();
      expect(container.querySelector('.diff-toolbar-section--navigation')).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero changes', () => {
      render(<DiffToolbar {...defaultProps} totalChanges={0} />);

      // Navigation should not render when totalChanges is 0
      expect(screen.queryByLabelText('Previous change')).not.toBeInTheDocument();
    });

    it('should handle empty search query', () => {
      render(<DiffToolbar {...defaultProps} searchQuery="" />);

      const searchInput = screen.getByRole('textbox');

      expect(searchInput).toHaveValue('');
      expect(screen.queryByLabelText('Clear search')).not.toBeInTheDocument();
    });

    it('should handle large number of changes', () => {
      render(<DiffToolbar {...defaultProps} totalChanges={1000} currentChangeIndex={499} />);

      expect(screen.getByText('500 / 1000 changes')).toBeInTheDocument();
    });
  });
});
