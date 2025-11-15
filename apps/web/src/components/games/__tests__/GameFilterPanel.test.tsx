import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameFilterPanel } from '../GameFilterPanel';
import { GameFilters } from '@/lib/api';

describe('GameFilterPanel', () => {
  const mockOnChange = jest.fn();
  const defaultFilters: GameFilters = {};

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('should render the filters title', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      expect(screen.getByText('Filters')).toBeInTheDocument();
    });

    it('should render all filter sections', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      // Check for main filter labels
      expect(screen.getByText('Players')).toBeInTheDocument();
      expect(screen.getByText('Play Time (minutes)')).toBeInTheDocument();
      expect(screen.getByText('Year Published')).toBeInTheDocument();
      expect(screen.getByText('Show only BGG games')).toBeInTheDocument();
    });

    it('should render reset filters button', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      expect(screen.getByRole('button', { name: /reset filters/i })).toBeInTheDocument();
    });
  });

  describe('Player Count Filter', () => {
    it('should call onChange when min players is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      // Use more specific query with within for Player section
      const playersSection = within(screen.getByText('Players').parentElement!);
      const minPlayersSelect = playersSection.getAllByRole('combobox')[0]; // First combobox in Players section
      await user.click(minPlayersSelect);

      // Select "2" players
      const option = await screen.findByRole('option', { name: '2' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ minPlayers: 2 });
    });

    it('should call onChange when max players is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const playersSection = within(screen.getByText('Players').parentElement!);
      const maxPlayersSelect = playersSection.getAllByRole('combobox')[1]; // Second combobox in Players section
      await user.click(maxPlayersSelect);

      // Select "4" players
      const option = await screen.findByRole('option', { name: '4' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ maxPlayers: 4 });
    });

    it('should display current min/max player values', () => {
      const filters: GameFilters = { minPlayers: 2, maxPlayers: 4 };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const playersSection = within(screen.getByText('Players').parentElement!);
      const [minPlayersSelect, maxPlayersSelect] = playersSection.getAllByRole('combobox');

      expect(minPlayersSelect).toHaveTextContent('2');
      expect(maxPlayersSelect).toHaveTextContent('4');
    });
  });

  describe('Play Time Filter', () => {
    it('should call onChange when min play time is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const minPlayTimeSelect = within(screen.getByText('Play Time (minutes)').parentElement!).getByLabelText('Min');
      await user.click(minPlayTimeSelect);

      const option = await screen.findByRole('option', { name: '30' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ minPlayTime: 30 });
    });

    it('should call onChange when max play time is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const maxPlayTimeSelect = within(screen.getByText('Play Time (minutes)').parentElement!).getByLabelText('Max');
      await user.click(maxPlayTimeSelect);

      const option = await screen.findByRole('option', { name: '60' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ maxPlayTime: 60 });
    });

    it('should display current min/max play time values', () => {
      const filters: GameFilters = { minPlayTime: 30, maxPlayTime: 90 };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const minPlayTimeSelect = within(screen.getByText('Play Time (minutes)').parentElement!).getByLabelText('Min');
      const maxPlayTimeSelect = within(screen.getByText('Play Time (minutes)').parentElement!).getByLabelText('Max');

      expect(minPlayTimeSelect).toHaveTextContent('30');
      expect(maxPlayTimeSelect).toHaveTextContent('90');
    });
  });

  describe('Year Published Filter', () => {
    it('should call onChange when year from is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const yearFromSelect = within(screen.getByText('Year Published').parentElement!).getByLabelText('From');
      await user.click(yearFromSelect);

      const option = await screen.findByRole('option', { name: '2020' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ yearFrom: 2020 });
    });

    it('should call onChange when year to is selected', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const yearToSelect = within(screen.getByText('Year Published').parentElement!).getByLabelText('To');
      await user.click(yearToSelect);

      const option = await screen.findByRole('option', { name: '2025' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ yearTo: 2025 });
    });

    it('should display current year from/to values', () => {
      const filters: GameFilters = { yearFrom: 2015, yearTo: 2024 };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const yearFromSelect = within(screen.getByText('Year Published').parentElement!).getByLabelText('From');
      const yearToSelect = within(screen.getByText('Year Published').parentElement!).getByLabelText('To');

      expect(yearFromSelect).toHaveTextContent('2015');
      expect(yearToSelect).toHaveTextContent('2024');
    });
  });

  describe('BGG Only Filter', () => {
    it('should call onChange when BGG only checkbox is clicked', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const checkbox = screen.getByRole('checkbox', { name: /show only bgg games/i });
      await user.click(checkbox);

      expect(mockOnChange).toHaveBeenCalledWith({ bggOnly: true });
    });

    it('should display checked state when bggOnly is true', () => {
      const filters: GameFilters = { bggOnly: true };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const checkbox = screen.getByRole('checkbox', { name: /show only bgg games/i });
      expect(checkbox).toBeChecked();
    });

    it('should display unchecked state when bggOnly is false', () => {
      const filters: GameFilters = { bggOnly: false };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const checkbox = screen.getByRole('checkbox', { name: /show only bgg games/i });
      expect(checkbox).not.toBeChecked();
    });
  });

  describe('Reset Functionality', () => {
    it('should disable reset button when no filters are active', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const resetButton = screen.getByRole('button', { name: /reset filters/i });
      expect(resetButton).toBeDisabled();
    });

    it('should enable reset button when filters are active', () => {
      const filters: GameFilters = { minPlayers: 2 };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const resetButton = screen.getByRole('button', { name: /reset filters/i });
      expect(resetButton).toBeEnabled();
    });

    it('should call onChange with empty object when reset is clicked', async () => {
      const user = userEvent.setup();
      const filters: GameFilters = { minPlayers: 2, maxPlayers: 4, bggOnly: true };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const resetButton = screen.getByRole('button', { name: /reset filters/i });
      await user.click(resetButton);

      expect(mockOnChange).toHaveBeenCalledWith({});
    });
  });

  describe('Collapsible Behavior', () => {
    it('should render collapse button when collapsible is true', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} collapsible={true} />);

      expect(screen.getByRole('button', { name: /collapse filters/i })).toBeInTheDocument();
    });

    it('should not render collapse button when collapsible is false', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} collapsible={false} />);

      expect(screen.queryByRole('button', { name: /collapse filters/i })).not.toBeInTheDocument();
    });

    it('should toggle filters visibility when collapse button is clicked', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} collapsible={true} />);

      // Initially expanded
      expect(screen.getByText('Players')).toBeInTheDocument();

      // Click collapse button
      const collapseButton = screen.getByRole('button', { name: /collapse filters/i });
      await user.click(collapseButton);

      // Should be collapsed
      expect(screen.queryByText('Players')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /expand filters/i })).toBeInTheDocument();

      // Click expand button
      const expandButton = screen.getByRole('button', { name: /expand filters/i });
      await user.click(expandButton);

      // Should be expanded again
      expect(screen.getByText('Players')).toBeInTheDocument();
    });

    it('should start collapsed when defaultCollapsed is true', () => {
      render(
        <GameFilterPanel
          filters={defaultFilters}
          onChange={mockOnChange}
          collapsible={true}
          defaultCollapsed={true}
        />
      );

      // Should be collapsed initially
      expect(screen.queryByText('Players')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /expand filters/i })).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels for collapse button', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} collapsible={true} />);

      const collapseButton = screen.getByRole('button', { name: /collapse filters/i });
      expect(collapseButton).toHaveAttribute('aria-expanded', 'true');
      expect(collapseButton).toHaveAttribute('aria-label', 'Collapse filters');
    });

    it('should have associated labels for all selects', () => {
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      // Check all comboboxes have IDs
      const playersSection = within(screen.getByText('Players').parentElement!);
      const [minPlayers, maxPlayers] = playersSection.getAllByRole('combobox');
      expect(minPlayers).toHaveAttribute('id', 'minPlayers');
      expect(maxPlayers).toHaveAttribute('id', 'maxPlayers');
    });

    it('should have checkbox label as clickable', async () => {
      const user = userEvent.setup();
      render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      const label = screen.getByText('Show only BGG games');
      await user.click(label);

      expect(mockOnChange).toHaveBeenCalledWith({ bggOnly: true });
    });
  });

  describe('Edge Cases', () => {
    it('should handle undefined filter values', () => {
      const filters: GameFilters = { minPlayers: undefined, maxPlayers: undefined };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const playersSection = within(screen.getByText('Players').parentElement!);
      const [minPlayersSelect, maxPlayersSelect] = playersSection.getAllByRole('combobox');

      expect(minPlayersSelect).toHaveTextContent('Any');
      expect(maxPlayersSelect).toHaveTextContent('Any');
    });

    it('should handle multiple filter changes', async () => {
      const user = userEvent.setup();
      const { rerender } = render(<GameFilterPanel filters={defaultFilters} onChange={mockOnChange} />);

      // Change min players
      const playersSection = within(screen.getByText('Players').parentElement!);
      const minPlayersSelect = playersSection.getAllByRole('combobox')[0];
      await user.click(minPlayersSelect);
      const option1 = await screen.findByRole('option', { name: '2' });
      await user.click(option1);

      // Rerender with new filters
      rerender(<GameFilterPanel filters={{ minPlayers: 2 }} onChange={mockOnChange} />);

      // Change BGG checkbox
      const checkbox = screen.getByRole('checkbox', { name: /show only bgg games/i });
      await user.click(checkbox);

      expect(mockOnChange).toHaveBeenCalledTimes(2);
      expect(mockOnChange).toHaveBeenNthCalledWith(1, { minPlayers: 2 });
      expect(mockOnChange).toHaveBeenNthCalledWith(2, { minPlayers: 2, bggOnly: true });
    });

    it('should preserve existing filters when changing one filter', async () => {
      const user = userEvent.setup();
      const filters: GameFilters = { minPlayers: 2, bggOnly: true };
      render(<GameFilterPanel filters={filters} onChange={mockOnChange} />);

      const playersSection = within(screen.getByText('Players').parentElement!);
      const maxPlayersSelect = playersSection.getAllByRole('combobox')[1];
      await user.click(maxPlayersSelect);
      const option = await screen.findByRole('option', { name: '4' });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({ minPlayers: 2, bggOnly: true, maxPlayers: 4 });
    });
  });
});
