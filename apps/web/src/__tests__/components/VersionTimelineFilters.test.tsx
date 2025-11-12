import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { VersionTimelineFilters } from '../../components/VersionTimelineFilters';

describe('VersionTimelineFilters', () => {
  const mockAuthors = ['User One', 'User Two', 'User Three'];
  const mockOnFiltersChange = jest.fn();
  const mockOnReset = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render all filter inputs', () => {
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    expect(screen.getByLabelText('Start Date')).toBeInTheDocument();
    expect(screen.getByLabelText('End Date')).toBeInTheDocument();
    expect(screen.getByLabelText('Author')).toBeInTheDocument();
    expect(screen.getByLabelText('Search Versions')).toBeInTheDocument();
    expect(screen.getByLabelText('Reset filters')).toBeInTheDocument();
  });

  it('should display all authors in dropdown', async () => {
    const user = userEvent.setup();
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    // Open the select dropdown
    const trigger = screen.getByRole('combobox', { name: /author/i });
    await user.click(trigger);

    // Check that all authors are present
    expect(screen.getByRole('option', { name: 'All authors' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'User One' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'User Two' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'User Three' })).toBeInTheDocument();
  });

  it('should call onFiltersChange when start date changes', () => {
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    const startDateInput = screen.getByLabelText('Start Date') as HTMLInputElement;
    fireEvent.change(startDateInput, { target: { value: '2024-01-01' } });

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      startDate: '2024-01-01',
    });
  });

  it('should call onFiltersChange when author changes', async () => {
    const user = userEvent.setup();
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    // Click the select trigger to open the dropdown
    const authorTrigger = screen.getByRole('combobox', { name: /author/i });
    await user.click(authorTrigger);

    // Click the option
    const option = screen.getByRole('option', { name: 'User One' });
    await user.click(option);

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      author: 'User One',
    });
  });

  it('should call onFiltersChange when search query changes', () => {
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{}}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    const searchInput = screen.getByLabelText('Search Versions') as HTMLInputElement;
    fireEvent.change(searchInput, { target: { value: 'v2' } });

    expect(mockOnFiltersChange).toHaveBeenCalledWith({
      searchQuery: 'v2',
    });
  });

  it('should call onReset when reset button is clicked', () => {
    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={{ author: 'User One' }}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    const resetButton = screen.getByLabelText('Reset filters');
    fireEvent.click(resetButton);

    expect(mockOnReset).toHaveBeenCalled();
  });

  it('should preserve filter values when provided', () => {
    const initialFilters = {
      startDate: '2024-01-01',
      endDate: '2024-12-31',
      author: 'User Two',
      searchQuery: 'v2',
    };

    render(
      <VersionTimelineFilters
        authors={mockAuthors}
        filters={initialFilters}
        onFiltersChange={mockOnFiltersChange}
        onReset={mockOnReset}
      />
    );

    expect((screen.getByLabelText('Start Date') as HTMLInputElement).value).toBe('2024-01-01');
    expect((screen.getByLabelText('End Date') as HTMLInputElement).value).toBe('2024-12-31');
    // For shadcn Select, check the trigger text instead of value
    expect(screen.getByRole('combobox', { name: /author/i })).toHaveTextContent('User Two');
    expect((screen.getByLabelText('Search Versions') as HTMLInputElement).value).toBe('v2');
  });
});
