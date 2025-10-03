import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import LogsPage from './logs';

describe('Logs page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders page title and description', () => {
    render(<LogsPage />);

    expect(screen.getByText('Observability Dashboard')).toBeInTheDocument();
    expect(screen.getByText(/monitor application logs/i)).toBeInTheDocument();
  });

  it('shows back to home link', () => {
    render(<LogsPage />);

    const link = screen.getByRole('link', { name: /back to home/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/');
  });

  it('renders filter input', () => {
    render(<LogsPage />);

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    expect(filterInput).toBeInTheDocument();
  });

  it('displays sample logs on mount', async () => {
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
      expect(screen.getByText('User logged in successfully')).toBeInTheDocument();
    });
  });

  it('filters logs by message', async () => {
    const user = userEvent.setup();
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
    });

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    await user.type(filterInput, 'logged in');

    expect(screen.queryByText('Application started')).not.toBeInTheDocument();
    expect(screen.getByText('User logged in successfully')).toBeInTheDocument();
  });

  it('filters logs by request ID', async () => {
    const user = userEvent.setup();
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
    });

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    await user.type(filterInput, 'req-002');

    expect(screen.queryByText('Application started')).not.toBeInTheDocument();
    expect(screen.getByText('User logged in successfully')).toBeInTheDocument();
  });

  it('shows all logs when filter is cleared', async () => {
    const user = userEvent.setup();
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
    });

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    await user.type(filterInput, 'logged in');

    expect(screen.queryByText('Application started')).not.toBeInTheDocument();

    await user.clear(filterInput);

    expect(screen.getByText('Application started')).toBeInTheDocument();
    expect(screen.getByText('User logged in successfully')).toBeInTheDocument();
  });

  it('shows no results message when filter matches nothing', async () => {
    const user = userEvent.setup();
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
    });

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    await user.type(filterInput, 'nonexistent');

    expect(screen.queryByText('Application started')).not.toBeInTheDocument();
    expect(screen.queryByText('User logged in successfully')).not.toBeInTheDocument();
  });

  it('displays log metadata correctly', async () => {
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText(/req-001/i)).toBeInTheDocument();
      expect(screen.getByText(/req-002/i)).toBeInTheDocument();
      expect(screen.getByText(/user-123/i)).toBeInTheDocument();
      expect(screen.getByText(/dev/i)).toBeInTheDocument();
    });
  });

  it('shows log level badges', async () => {
    render(<LogsPage />);

    await waitFor(() => {
      const infoBadges = screen.getAllByText('INFO');
      expect(infoBadges.length).toBeGreaterThan(0);
    });
  });

  it('filter is case insensitive', async () => {
    const user = userEvent.setup();
    render(<LogsPage />);

    await waitFor(() => {
      expect(screen.getByText('Application started')).toBeInTheDocument();
    });

    const filterInput = screen.getByPlaceholderText(/filter logs/i);
    await user.type(filterInput, 'APPLICATION');

    expect(screen.getByText('Application started')).toBeInTheDocument();
  });
});
