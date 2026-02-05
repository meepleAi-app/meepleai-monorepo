/**
 * Unit tests for VersionHistory component (Issue #3355)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { VersionHistory } from '../VersionHistory';

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

const mockVersions = [
  {
    id: '1',
    version: '1.2.0',
    title: 'Current Version',
    description: 'Updated setup rules',
    author: 'user@test.com',
    createdAt: '2026-02-01T10:00:00Z',
    changeCount: 5,
    isCurrentVersion: true,
  },
  {
    id: '2',
    version: '1.1.0',
    title: 'Previous Version',
    description: 'Added FAQ section',
    author: 'user@test.com',
    createdAt: '2026-01-28T10:00:00Z',
    changeCount: 3,
    isCurrentVersion: false,
  },
  {
    id: '3',
    version: '1.0.0',
    title: 'Initial Version',
    description: 'Initial version',
    author: 'admin@test.com',
    createdAt: '2026-01-20T10:00:00Z',
    changeCount: 10,
    isCurrentVersion: false,
  },
];

const createMockHistoryResponse = (overrides = {}) => ({
  gameId: 'game-123',
  versions: mockVersions,
  authors: ['user@test.com', 'admin@test.com'],
  totalVersions: 3,
  ...overrides,
});

describe('VersionHistory', () => {
  const defaultProps = {
    gameId: 'game-123',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders loading state initially', () => {
    mockFetch.mockImplementation(() => new Promise(() => {})); // Never resolves

    render(<VersionHistory {...defaultProps} />);

    expect(screen.getByText('Version History')).toBeInTheDocument();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('fetches and displays version history', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse()),
    });

    render(<VersionHistory {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    expect(screen.getByText('1.2.0')).toBeInTheDocument();
    expect(screen.getByText('1.1.0')).toBeInTheDocument();
    expect(screen.getByText('1.0.0')).toBeInTheDocument();
    expect(screen.getByText('Current')).toBeInTheDocument();
    expect(screen.getByText('Initial')).toBeInTheDocument();
  });

  it('shows error state on fetch failure', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
    });

    render(<VersionHistory {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByText(/Error:/)).toBeInTheDocument();
      expect(screen.getByText(/Failed to fetch version history/)).toBeInTheDocument();
    });
  });

  it('shows empty state when no versions', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse({ versions: [] })),
    });

    render(<VersionHistory {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByText('No version history available.')).toBeInTheDocument();
    });
  });

  it('calls onViewVersion when View button is clicked', async () => {
    const user = userEvent.setup();
    const onViewVersion = vi.fn();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse()),
    });

    render(<VersionHistory {...defaultProps} onViewVersion={onViewVersion} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    const viewButtons = screen.getAllByTitle('View this version');
    await user.click(viewButtons[0]);

    expect(onViewVersion).toHaveBeenCalledWith('1.2.0');
  });

  it('handles compare flow correctly', async () => {
    const user = userEvent.setup();
    const onCompareVersions = vi.fn();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse()),
    });

    render(<VersionHistory {...defaultProps} onCompareVersions={onCompareVersions} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    // First click selects for comparison
    const compareButtons = screen.getAllByTitle(/Select for comparison|Compare with selected/);
    await user.click(compareButtons[0]);

    expect(screen.getByText('Select another version to compare')).toBeInTheDocument();

    // Second click on different version triggers comparison
    await user.click(compareButtons[1]);

    expect(onCompareVersions).toHaveBeenCalledWith('1.1.0', '1.2.0');
  });

  it('shows restore confirmation dialog', async () => {
    const user = userEvent.setup();
    const onRestoreVersion = vi.fn().mockResolvedValue(undefined);
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse()),
    });

    render(<VersionHistory {...defaultProps} onRestoreVersion={onRestoreVersion} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    // Click restore on a non-current version
    const restoreButtons = screen.getAllByTitle('Restore this version');
    await user.click(restoreButtons[0]);

    expect(screen.getByText('Restore Version')).toBeInTheDocument();
    expect(screen.getByText(/Are you sure you want to restore version/)).toBeInTheDocument();
  });

  it('executes restore when confirmed', async () => {
    const user = userEvent.setup();
    const onRestoreVersion = vi.fn().mockResolvedValue(undefined);
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(createMockHistoryResponse()),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(createMockHistoryResponse()),
      });

    render(<VersionHistory {...defaultProps} onRestoreVersion={onRestoreVersion} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    const restoreButtons = screen.getAllByTitle('Restore this version');
    await user.click(restoreButtons[0]);

    // Confirm restore
    const confirmButton = screen.getByRole('button', { name: 'Restore' });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(onRestoreVersion).toHaveBeenCalledWith('1.1.0');
    });
  });

  it('displays version metadata correctly', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(createMockHistoryResponse()),
    });

    render(<VersionHistory {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    });

    // Check description is displayed (with quotes in component)
    expect(screen.getByText(/Updated setup rules/)).toBeInTheDocument();

    // Check author is displayed
    expect(screen.getAllByText('user@test.com').length).toBeGreaterThan(0);

    // Check change count is displayed
    expect(screen.getByText('5 changes')).toBeInTheDocument();
  });
});
