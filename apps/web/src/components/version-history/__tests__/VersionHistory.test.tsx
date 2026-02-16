import React from 'react';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { VersionHistory, type VersionItem } from '../VersionHistory';

const mockVersions: VersionItem[] = [
  {
    id: 'v3-id',
    versionNumber: 3,
    content: 'Setup: Place 4 tiles on the board.',
    label: 'Updated setup rules',
    createdAt: '2026-02-01T10:00:00Z',
    createdBy: 'editor@meepleai.dev',
    isCurrent: true,
  },
  {
    id: 'v2-id',
    versionNumber: 2,
    content: 'Setup: Place 3 tiles on the board.\nAdded FAQ section.',
    label: 'Added FAQ section',
    createdAt: '2026-01-28T10:00:00Z',
    createdBy: 'editor@meepleai.dev',
    isCurrent: false,
  },
  {
    id: 'v1-id',
    versionNumber: 1,
    content: 'Setup: Place 3 tiles on the board.',
    label: 'Initial version',
    createdAt: '2026-01-20T10:00:00Z',
    isCurrent: false,
  },
];

describe('VersionHistory', () => {
  it('renders loading state', () => {
    render(<VersionHistory versions={[]} isLoading={true} />);
    expect(screen.getByTestId('version-history-loading')).toBeInTheDocument();
  });

  it('renders empty state when no versions', () => {
    render(<VersionHistory versions={[]} />);
    expect(screen.getByTestId('version-history-empty')).toBeInTheDocument();
    expect(screen.getByText('No versions available.')).toBeInTheDocument();
  });

  it('renders all versions', () => {
    render(<VersionHistory versions={mockVersions} />);
    expect(screen.getByTestId('version-history-list')).toBeInTheDocument();
    expect(screen.getByText('v3')).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
    expect(screen.getByText('v1')).toBeInTheDocument();
  });

  it('shows Current badge only for current version', () => {
    render(<VersionHistory versions={mockVersions} />);
    const badges = screen.getAllByTestId('current-badge');
    expect(badges).toHaveLength(1);
    expect(badges[0]).toHaveTextContent('Current');
  });

  it('displays version labels', () => {
    render(<VersionHistory versions={mockVersions} />);
    expect(screen.getByText(/Updated setup rules/)).toBeInTheDocument();
    expect(screen.getByText(/Added FAQ section/)).toBeInTheDocument();
    expect(screen.getByText(/Initial version/)).toBeInTheDocument();
  });

  it('displays createdBy when provided', () => {
    render(<VersionHistory versions={mockVersions} />);
    const byLabels = screen.getAllByText(/by editor@meepleai.dev/);
    expect(byLabels.length).toBe(2);
  });

  it('displays custom title', () => {
    render(<VersionHistory versions={mockVersions} title="Document History" />);
    expect(screen.getByText('Document History')).toBeInTheDocument();
  });

  it('calls onCompare when Compare button clicked', async () => {
    const user = userEvent.setup();
    const onCompare = vi.fn();
    render(<VersionHistory versions={mockVersions} onCompare={onCompare} />);

    await user.click(screen.getByTestId('compare-btn-2'));
    expect(onCompare).toHaveBeenCalledWith('v2-id');
  });

  it('calls onView when View button clicked', async () => {
    const user = userEvent.setup();
    const onView = vi.fn();
    render(<VersionHistory versions={mockVersions} onView={onView} />);

    await user.click(screen.getByTestId('view-btn-1'));
    expect(onView).toHaveBeenCalledWith('v1-id');
  });

  it('calls onRestore when Restore button clicked', async () => {
    const user = userEvent.setup();
    const onRestore = vi.fn();
    render(<VersionHistory versions={mockVersions} onRestore={onRestore} />);

    await user.click(screen.getByTestId('restore-btn-2'));
    expect(onRestore).toHaveBeenCalledWith('v2-id');
  });

  it('does not show Restore button for current version', () => {
    const onRestore = vi.fn();
    render(<VersionHistory versions={mockVersions} onRestore={onRestore} />);

    expect(screen.queryByTestId('restore-btn-3')).not.toBeInTheDocument();
    expect(screen.getByTestId('restore-btn-2')).toBeInTheDocument();
    expect(screen.getByTestId('restore-btn-1')).toBeInTheDocument();
  });

  it('does not show action buttons when callbacks not provided', () => {
    render(<VersionHistory versions={mockVersions} />);

    expect(screen.queryByTestId('compare-btn-1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('view-btn-1')).not.toBeInTheDocument();
    expect(screen.queryByTestId('restore-btn-1')).not.toBeInTheDocument();
  });
});
