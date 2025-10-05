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
    expect(mockGet).toHaveBeenCalledWith('/logs');
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
});
