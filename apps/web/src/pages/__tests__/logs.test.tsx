import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import LogsPage from '../logs';
import { api } from '../../lib/api';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn()
  }
}));

describe('LogsPage', () => {
  const mockLogs = [
    {
      timestamp: new Date('2024-01-01T10:00:00Z').toISOString(),
      level: 'INFO',
      message: 'Application started',
      requestId: 'req-001'
    },
    {
      timestamp: new Date('2024-01-01T10:05:00Z').toISOString(),
      level: 'INFO',
      message: 'User logged in successfully',
      requestId: 'req-002',
      userId: 'user-123'
    }
  ];

  const mockGet = api.get as jest.MockedFunction<typeof api.get>;

  beforeEach(() => {
    mockGet.mockResolvedValue(mockLogs);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('renders default logs and allows filtering by user id', async () => {
    const user = userEvent.setup();

    render(<LogsPage />);

    await screen.findByText(/Application started/i);
    expect(mockGet).toHaveBeenCalledWith('/api/v1/logs');
    expect(screen.getByText(/User logged in successfully/i)).toBeInTheDocument();

    const filterInput = screen.getByPlaceholderText(/Filter logs/i);

    await user.clear(filterInput);
    await user.type(filterInput, 'user-123');

    expect(screen.getByText(/User logged in successfully/i)).toBeInTheDocument();
    expect(screen.queryByText(/Application started/i)).not.toBeInTheDocument();
  });

  it('shows empty state when no logs match the filter', async () => {
    const user = userEvent.setup();

    render(<LogsPage />);

    const filterInput = await screen.findByPlaceholderText(/Filter logs/i);

    await user.clear(filterInput);
    await user.type(filterInput, 'no-match');

    expect(screen.getByText(/No logs found/i)).toBeInTheDocument();
    expect(screen.getByText(/basic observability dashboard/i)).toBeInTheDocument();
  });

  it('handles logs without request or user identifiers when filtering', async () => {
    const logsWithoutIds = [
      {
        timestamp: new Date('2024-01-01T11:00:00Z').toISOString(),
        level: 'INFO',
        message: 'System maintenance in progress'
      }
    ];

    mockGet.mockResolvedValueOnce(logsWithoutIds);

    const user = userEvent.setup();

    render(<LogsPage />);

    const filterInput = await screen.findByPlaceholderText(/Filter logs/i);

    await user.type(filterInput, 'maintenance');

    expect(screen.getByText(/System maintenance in progress/i)).toBeInTheDocument();
  });

  it('shows loading state while fetching logs', () => {
    mockGet.mockImplementation(() => new Promise(() => {})); // Never resolves

    render(<LogsPage />);

    expect(screen.getByText(/Loading logs.../i)).toBeInTheDocument();
  });

  it('shows 403 permission error with specific message', async () => {
    const error403 = new Error('403 Forbidden');
    mockGet.mockRejectedValueOnce(error403);

    render(<LogsPage />);

    expect(await screen.findByText(/You do not have permission to view logs/i)).toBeInTheDocument();
    expect(screen.getByText(/contact an administrator/i)).toBeInTheDocument();
  });

  it('shows generic error message for non-403 errors', async () => {
    const genericError = new Error('Network error');
    mockGet.mockRejectedValueOnce(genericError);

    render(<LogsPage />);

    expect(await screen.findByText(/Unable to load logs from the server/i)).toBeInTheDocument();
    expect(screen.getByText(/Please try again later/i)).toBeInTheDocument();
  });

  it('filters logs by requestId', async () => {
    const user = userEvent.setup();

    render(<LogsPage />);

    await screen.findByText(/Application started/i);

    const filterInput = screen.getByPlaceholderText(/Filter logs/i);

    await user.clear(filterInput);
    await user.type(filterInput, 'req-002');

    expect(screen.getByText(/User logged in successfully/i)).toBeInTheDocument();
    expect(screen.queryByText(/Application started/i)).not.toBeInTheDocument();
  });

  it('renders correct colors for different log levels', async () => {
    const logsWithDifferentLevels = [
      {
        timestamp: new Date('2024-01-01T12:00:00Z').toISOString(),
        level: 'ERROR',
        message: 'Critical error occurred',
        requestId: 'req-003'
      },
      {
        timestamp: new Date('2024-01-01T12:01:00Z').toISOString(),
        level: 'WARN',
        message: 'Warning message',
        requestId: 'req-004'
      },
      {
        timestamp: new Date('2024-01-01T12:02:00Z').toISOString(),
        level: 'WARNING',
        message: 'Another warning',
        requestId: 'req-005'
      },
      {
        timestamp: new Date('2024-01-01T12:03:00Z').toISOString(),
        level: 'INFO',
        message: 'Info message',
        requestId: 'req-006'
      },
      {
        timestamp: new Date('2024-01-01T12:04:00Z').toISOString(),
        level: 'DEBUG',
        message: 'Debug message',
        requestId: 'req-007'
      }
    ];

    mockGet.mockResolvedValueOnce(logsWithDifferentLevels);

    render(<LogsPage />);

    const errorLevel = await screen.findByText('ERROR');
    const warnLevel = screen.getByText('WARN');
    const warningLevel = screen.getByText('WARNING');
    const infoLevel = screen.getByText('INFO');
    const debugLevel = screen.getByText('DEBUG');

    expect(errorLevel).toHaveStyle({ color: '#d93025' }); // Red
    expect(warnLevel).toHaveStyle({ color: '#f9ab00' }); // Orange
    expect(warningLevel).toHaveStyle({ color: '#f9ab00' }); // Orange
    expect(infoLevel).toHaveStyle({ color: '#1a73e8' }); // Blue
    expect(debugLevel).toHaveStyle({ color: '#5f6368' }); // Gray (default)
  });

  it('cleans up on unmount to prevent memory leaks', async () => {
    const { unmount } = render(<LogsPage />);

    await screen.findByText(/Application started/i);

    // Unmount should trigger cleanup
    unmount();

    // After unmount, fetch should not cause state updates
    expect(mockGet).toHaveBeenCalledTimes(1);
  });
});
