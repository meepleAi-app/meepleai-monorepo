/**
 * Unit tests for VersionComparison component (Issue #3355)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { VersionComparison } from '../VersionComparison';

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

const mockDiff = {
  gameId: 'game-123',
  fromVersion: '1.0.0',
  toVersion: '1.1.0',
  fromCreatedAt: '2026-01-20T10:00:00Z',
  toCreatedAt: '2026-01-28T10:00:00Z',
  summary: {
    totalChanges: 6,
    added: 2,
    modified: 3,
    deleted: 1,
    unchanged: 5,
  },
  changes: [
    {
      type: 'Added',
      newAtom: 'rule-1',
      newValue: { id: '1', text: 'New setup rule', section: 'Setup' },
    },
    {
      type: 'Added',
      newAtom: 'rule-2',
      newValue: { id: '2', text: 'Another new rule', section: 'Gameplay' },
    },
    {
      type: 'Modified',
      oldAtom: 'rule-3',
      newAtom: 'rule-3',
      oldValue: { id: '3', text: 'Old text', section: 'Setup' },
      newValue: { id: '3', text: 'Updated text', section: 'Setup' },
      fieldChanges: [{ fieldName: 'text', oldValue: 'Old text', newValue: 'Updated text' }],
    },
    {
      type: 'Deleted',
      oldAtom: 'rule-4',
      oldValue: { id: '4', text: 'Removed rule', section: 'Legacy' },
    },
    {
      type: 'Unchanged',
      oldAtom: 'rule-5',
      oldValue: { id: '5', text: 'Unchanged rule', section: 'Core' },
    },
  ],
};

describe('VersionComparison', () => {
  const defaultProps = {
    gameId: 'game-123',
    fromVersion: '1.0.0',
    toVersion: '1.1.0',
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
    mockFetch.mockImplementation(() => new Promise(() => {}));

    render(<VersionComparison {...defaultProps} />);

    expect(screen.getByText('Comparing Versions')).toBeInTheDocument();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('fetches and displays diff summary', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockDiff),
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByText('Version Comparison')).toBeInTheDocument();
    });

    // Check version badges
    expect(screen.getByText('1.0.0')).toBeInTheDocument();
    expect(screen.getByText('1.1.0')).toBeInTheDocument();

    // Check summary
    expect(screen.getByText('6 total changes')).toBeInTheDocument();
    expect(screen.getByText('2 Added')).toBeInTheDocument();
    expect(screen.getByText('3 Modified')).toBeInTheDocument();
    expect(screen.getByText('1 Deleted')).toBeInTheDocument();
  });

  it('shows error state on fetch failure', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByText(/Error:/)).toBeInTheDocument();
      expect(screen.getByText(/Failed to fetch version diff/)).toBeInTheDocument();
    });
  });

  it('filters changes by type', async () => {
    const user = userEvent.setup();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockDiff),
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-comparison-changes')).toBeInTheDocument();
    });

    // Filter to only Added
    await user.click(screen.getByRole('button', { name: 'Added' }));

    // Should show only Added items
    const changeItems = screen.getAllByTestId(/change-item-/);
    expect(changeItems.every(item => item.getAttribute('data-testid') === 'change-item-added')).toBe(true);
  });

  it('toggles unchanged visibility', async () => {
    const user = userEvent.setup();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockDiff),
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-comparison-changes')).toBeInTheDocument();
    });

    // Initially unchanged are hidden
    expect(screen.queryByTestId('change-item-unchanged')).not.toBeInTheDocument();

    // Show unchanged
    await user.click(screen.getByRole('button', { name: 'Show Unchanged' }));

    expect(screen.getByTestId('change-item-unchanged')).toBeInTheDocument();

    // Hide unchanged
    await user.click(screen.getByRole('button', { name: 'Hide Unchanged' }));

    expect(screen.queryByTestId('change-item-unchanged')).not.toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockDiff),
    });

    render(<VersionComparison {...defaultProps} onClose={onClose} />);

    await waitFor(() => {
      expect(screen.getByText('Version Comparison')).toBeInTheDocument();
    });

    // Find and click close button
    const closeButton = screen.getByRole('button', { name: '' }); // X icon button
    await user.click(closeButton);

    expect(onClose).toHaveBeenCalled();
  });

  it('renders change items correctly', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockDiff),
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-comparison-changes')).toBeInTheDocument();
    });

    // Check that Added, Modified, and Deleted items are present
    expect(screen.getAllByTestId('change-item-added')).toHaveLength(2);
    expect(screen.getByTestId('change-item-modified')).toBeInTheDocument();
    expect(screen.getByTestId('change-item-deleted')).toBeInTheDocument();
  });

  it('shows empty message when no changes match filter', async () => {
    const user = userEvent.setup();
    const noDeletedDiff = {
      ...mockDiff,
      summary: { ...mockDiff.summary, deleted: 0 },
      changes: mockDiff.changes.filter(c => c.type !== 'Deleted'),
    };
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(noDeletedDiff),
    });

    render(<VersionComparison {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('version-comparison-changes')).toBeInTheDocument();
    });

    // Filter to Deleted (which has no items)
    await user.click(screen.getByRole('button', { name: 'Deleted' }));

    expect(screen.getByText('No changes match the current filter.')).toBeInTheDocument();
  });
});
