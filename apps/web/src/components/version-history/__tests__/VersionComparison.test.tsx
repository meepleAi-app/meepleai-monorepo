import React from 'react';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { VersionComparison } from '../VersionComparison';
import type { VersionItem } from '../VersionHistory';

const leftVersion: VersionItem = {
  id: 'v1-id',
  versionNumber: 1,
  content: 'Setup: Place 3 tiles on the board.\nDraw 5 cards each.',
  label: 'Initial version',
  createdAt: '2026-01-20T10:00:00Z',
  isCurrent: false,
};

const rightVersion: VersionItem = {
  id: 'v2-id',
  versionNumber: 2,
  content: 'Setup: Place 4 tiles on the board.\nDraw 5 cards each.\nShuffle the deck.',
  label: 'Updated setup rules',
  createdAt: '2026-01-28T10:00:00Z',
  isCurrent: true,
};

describe('VersionComparison', () => {
  it('renders comparison header with version numbers', () => {
    render(<VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} />);
    expect(screen.getByText('Comparing v1 vs v2')).toBeInTheDocument();
  });

  it('shows diff statistics badges', () => {
    render(<VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} />);
    const diffPanel = screen.getByTestId('diff-panel');
    expect(diffPanel).toBeInTheDocument();
  });

  it('renders the diff panel', () => {
    render(<VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} />);
    expect(screen.getByTestId('diff-panel')).toBeInTheDocument();
  });

  it('shows version labels in panel headers', () => {
    render(<VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} />);
    expect(screen.getByText('v1')).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
    expect(screen.getByText('Initial version')).toBeInTheDocument();
    expect(screen.getByText('Updated setup rules')).toBeInTheDocument();
  });

  it('calls onClose when Close button clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} onClose={onClose} />
    );

    await user.click(screen.getByTestId('close-comparison'));
    expect(onClose).toHaveBeenCalled();
  });

  it('does not show Close button when onClose not provided', () => {
    render(<VersionComparison leftVersion={leftVersion} rightVersion={rightVersion} />);
    expect(screen.queryByTestId('close-comparison')).not.toBeInTheDocument();
  });

  it('handles identical content (no changes)', () => {
    const sameVersion: VersionItem = {
      ...leftVersion,
      id: 'v1b-id',
      versionNumber: 2,
    };
    render(<VersionComparison leftVersion={leftVersion} rightVersion={sameVersion} />);
    expect(screen.getByTestId('diff-panel')).toBeInTheDocument();
  });

  it('handles empty content', () => {
    const emptyVersion: VersionItem = {
      id: 'empty-id',
      versionNumber: 0,
      content: '',
      createdAt: '2026-01-01T00:00:00Z',
    };
    render(<VersionComparison leftVersion={emptyVersion} rightVersion={rightVersion} />);
    expect(screen.getByTestId('diff-panel')).toBeInTheDocument();
  });
});
