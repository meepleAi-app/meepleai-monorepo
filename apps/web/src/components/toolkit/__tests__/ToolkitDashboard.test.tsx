/**
 * Unit tests for ToolkitDashboard.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

import type { ToolkitDashboardDto } from '@/lib/api/schemas/toolkit.schemas';
import { ToolkitDashboard } from '../ToolkitDashboard';

const mockToolkit: ToolkitDashboardDto = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: '00000000-0000-0000-0000-000000000002',
  ownerUserId: null,
  isDefault: true,
  displayName: 'Test Toolkit',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  widgets: [
    {
      id: '00000000-0000-0000-0000-000000000010',
      type: 'RandomGenerator',
      isEnabled: true,
      displayOrder: 1,
      config: '{}',
    },
    {
      id: '00000000-0000-0000-0000-000000000011',
      type: 'TurnManager',
      isEnabled: true,
      displayOrder: 2,
      config: '{}',
    },
    {
      id: '00000000-0000-0000-0000-000000000012',
      type: 'ScoreTracker',
      isEnabled: false,
      displayOrder: 3,
      config: '{}',
    },
    {
      id: '00000000-0000-0000-0000-000000000013',
      type: 'ResourceManager',
      isEnabled: true,
      displayOrder: 4,
      config: '{}',
    },
    {
      id: '00000000-0000-0000-0000-000000000014',
      type: 'NoteManager',
      isEnabled: true,
      displayOrder: 5,
      config: '{}',
    },
    {
      id: '00000000-0000-0000-0000-000000000015',
      type: 'Whiteboard',
      isEnabled: true,
      displayOrder: 6,
      config: '{}',
    },
  ],
};

vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...p }: { children?: React.ReactNode }) => <div {...p}>{children}</div>,
  },
  AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
}));

describe('ToolkitDashboard', () => {
  it('renders loading spinner when isLoading', () => {
    render(<ToolkitDashboard toolkit={null} isLoading={true} />);
    expect(screen.getByTestId('toolkit-dashboard-loading')).toBeInTheDocument();
  });

  it('renders empty state when toolkit is null', () => {
    render(<ToolkitDashboard toolkit={null} />);
    expect(screen.getByTestId('toolkit-dashboard-empty')).toBeInTheDocument();
    expect(screen.getByText(/no toolkit configured/i)).toBeInTheDocument();
  });

  it('renders all widgets from toolkit', () => {
    render(<ToolkitDashboard toolkit={mockToolkit} />);
    expect(screen.getByTestId('toolkit-random-generator')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-turn-manager')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-score-tracker')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-resource-manager')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-note-manager')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-whiteboard')).toBeInTheDocument();
  });

  it('renders dashboard grid container with data-testid', () => {
    render(<ToolkitDashboard toolkit={mockToolkit} data-testid="my-dashboard" />);
    expect(screen.getByTestId('my-dashboard')).toBeInTheDocument();
  });

  it('passes players to TurnManager and ScoreTracker', () => {
    const players = [{ id: 'p1', name: 'Zara' }];
    render(<ToolkitDashboard toolkit={mockToolkit} players={players} />);
    expect(screen.getAllByText('Zara').length).toBeGreaterThanOrEqual(1);
  });

  it('shows no-widgets message when all disabled', () => {
    const allDisabled: ToolkitDashboardDto = {
      ...mockToolkit,
      widgets: mockToolkit.widgets.map(w => ({ ...w, isEnabled: false })),
    };
    render(<ToolkitDashboard toolkit={allDisabled} />);
    expect(screen.getByTestId('toolkit-no-widgets')).toBeInTheDocument();
  });
});
