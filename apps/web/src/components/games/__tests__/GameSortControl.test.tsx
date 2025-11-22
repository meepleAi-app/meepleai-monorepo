import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameSortControl } from '../GameSortControl';
import { GameSortOptions, GameSortField } from '@/lib/api';

describe('GameSortControl', () => {
  const mockOnChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('should render sort field label', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      expect(screen.getByText('Sort by')).toBeInTheDocument();
    });

    it('should render sort direction button', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      expect(screen.getByRole('button', { name: /sort direction/i })).toBeInTheDocument();
    });

    it('should display "None" placeholder when no sort is selected', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      expect(screen.getByText('None')).toBeInTheDocument();
    });
  });

  describe('Sort Field Selection', () => {
    it('should call onChange with title field when title is selected', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const titleOption = await screen.findByRole('option', { name: 'Title' });
      await user.click(titleOption);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'title',
        direction: 'asc'
      });
    });

    it('should call onChange with yearPublished field when year is selected', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const yearOption = await screen.findByRole('option', { name: 'Year' });
      await user.click(yearOption);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'yearPublished',
        direction: 'asc'
      });
    });

    it('should call onChange with minPlayers field when min players is selected', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const minPlayersOption = await screen.findByRole('option', { name: 'Min Players' });
      await user.click(minPlayersOption);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'minPlayers',
        direction: 'asc'
      });
    });

    it('should call onChange with maxPlayers field when max players is selected', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const maxPlayersOption = await screen.findByRole('option', { name: 'Max Players' });
      await user.click(maxPlayersOption);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'maxPlayers',
        direction: 'asc'
      });
    });

    it('should call onChange with null when "None" is selected', async () => {
      const user = userEvent.setup();
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const noneOption = await screen.findByRole('option', { name: 'None' });
      await user.click(noneOption);

      expect(mockOnChange).toHaveBeenCalledWith(null);
    });

    it('should display selected sort field', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      expect(screen.getByText('Title')).toBeInTheDocument();
    });
  });

  describe('Sort Direction Toggle', () => {
    it('should toggle from ascending to descending', async () => {
      const user = userEvent.setup();
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /ascending/i });
      await user.click(directionButton);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'title',
        direction: 'desc'
      });
    });

    it('should toggle from descending to ascending', async () => {
      const user = userEvent.setup();
      const sortOptions: GameSortOptions = { field: 'title', direction: 'desc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /descending/i });
      await user.click(directionButton);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'title',
        direction: 'asc'
      });
    });

    it('should disable direction button when no sort field is selected', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /sort direction/i });
      expect(directionButton).toBeDisabled();
    });

    it('should enable direction button when sort field is selected', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /ascending/i });
      expect(directionButton).toBeEnabled();
    });
  });

  describe('Sort Icons', () => {
    it('should display ArrowUpDown icon when no sort is selected', () => {
      const { container } = render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      // Check for SVG with specific class
      const button = screen.getByRole('button', { name: /sort direction/i });
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('should display ArrowUp icon when direction is ascending', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      const { container } = render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button', { name: /ascending/i });
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('should display ArrowDown icon when direction is descending', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'desc' };
      const { container } = render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const button = screen.getByRole('button', { name: /descending/i });
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper aria-label for direction button when ascending', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /ascending/i });
      expect(directionButton).toHaveAttribute('aria-label', 'Sort direction: Ascending');
    });

    it('should have proper aria-label for direction button when descending', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'desc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /descending/i });
      expect(directionButton).toHaveAttribute('aria-label', 'Sort direction: Descending');
    });

    it('should have helpful title attribute when direction button is disabled', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /sort direction/i });
      expect(directionButton).toHaveAttribute('title', 'Select a sort field first');
    });

    it('should have helpful title attribute when direction button is enabled', () => {
      const sortOptions: GameSortOptions = { field: 'title', direction: 'asc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /ascending/i });
      expect(directionButton).toHaveAttribute('title', 'Currently: Ascending');
    });

    it('should have associated label for select', () => {
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      expect(select).toBeInTheDocument();
      expect(select).toHaveAttribute('id', 'sortField');
    });
  });

  describe('Edge Cases', () => {
    it('should preserve direction when changing sort field', async () => {
      const user = userEvent.setup();
      const sortOptions: GameSortOptions = { field: 'title', direction: 'desc' };
      render(<GameSortControl sortOptions={sortOptions} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const yearOption = await screen.findByRole('option', { name: 'Year' });
      await user.click(yearOption);

      // Should preserve 'desc' direction
      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'yearPublished',
        direction: 'desc'
      });
    });

    it('should default to ascending when no previous direction exists', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const select = screen.getByLabelText('Sort by');
      await user.click(select);

      const titleOption = await screen.findByRole('option', { name: 'Title' });
      await user.click(titleOption);

      expect(mockOnChange).toHaveBeenCalledWith({
        field: 'title',
        direction: 'asc'
      });
    });

    it('should not call onChange when direction button is clicked while disabled', async () => {
      const user = userEvent.setup();
      render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      const directionButton = screen.getByRole('button', { name: /sort direction/i });

      // Try to click disabled button (should not trigger onChange)
      await user.click(directionButton);

      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('should handle multiple direction toggles', async () => {
      const user = userEvent.setup();
      const { rerender } = render(
        <GameSortControl sortOptions={{ field: 'title', direction: 'asc' }} onChange={mockOnChange} />
      );

      // First toggle: asc -> desc
      const directionButton1 = screen.getByRole('button', { name: /ascending/i });
      await user.click(directionButton1);
      expect(mockOnChange).toHaveBeenNthCalledWith(1, { field: 'title', direction: 'desc' });

      // Rerender with new state
      rerender(<GameSortControl sortOptions={{ field: 'title', direction: 'desc' }} onChange={mockOnChange} />);

      // Second toggle: desc -> asc
      const directionButton2 = screen.getByRole('button', { name: /descending/i });
      await user.click(directionButton2);
      expect(mockOnChange).toHaveBeenNthCalledWith(2, { field: 'title', direction: 'asc' });
    });
  });

  describe('Integration', () => {
    it('should handle complete sort workflow', async () => {
      const user = userEvent.setup();
      const { rerender } = render(<GameSortControl sortOptions={null} onChange={mockOnChange} />);

      // Step 1: Select a sort field
      const select = screen.getByLabelText('Sort by');
      await user.click(select);
      const titleOption = await screen.findByRole('option', { name: 'Title' });
      await user.click(titleOption);

      expect(mockOnChange).toHaveBeenNthCalledWith(1, { field: 'title', direction: 'asc' });

      // Step 2: Rerender with new state
      rerender(<GameSortControl sortOptions={{ field: 'title', direction: 'asc' }} onChange={mockOnChange} />);

      // Step 3: Toggle direction
      const directionButton = screen.getByRole('button', { name: /ascending/i });
      await user.click(directionButton);

      expect(mockOnChange).toHaveBeenNthCalledWith(2, { field: 'title', direction: 'desc' });

      // Step 4: Rerender again
      rerender(<GameSortControl sortOptions={{ field: 'title', direction: 'desc' }} onChange={mockOnChange} />);

      // Step 5: Clear sort
      await user.click(select);
      const noneOption = await screen.findByRole('option', { name: 'None' });
      await user.click(noneOption);

      expect(mockOnChange).toHaveBeenNthCalledWith(3, null);
    });
  });
});
