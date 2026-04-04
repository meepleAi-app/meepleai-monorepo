import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TechActionsBar } from '../TechActionsBar';

describe('TechActionsBar', () => {
  it('renders all 4 action labels', () => {
    render(<TechActionsBar />);

    expect(screen.getByText('Clear Cache')).toBeInTheDocument();
    expect(screen.getByText('Reindex All')).toBeInTheDocument();
    expect(screen.getByText('System Health')).toBeInTheDocument();
    expect(screen.getByText('Export Users')).toBeInTheDocument();
  });

  it('renders the bar container with correct test id', () => {
    render(<TechActionsBar />);

    expect(screen.getByTestId('tech-actions-bar')).toBeInTheDocument();
  });

  it('renders all actions as links', () => {
    render(<TechActionsBar />);

    const actions = [
      { id: 'clear-cache', href: '/admin/config' },
      { id: 'reindex-all', href: '/admin/knowledge-base/vectors' },
      { id: 'system-health', href: '/admin/monitor' },
      { id: 'export-users', href: '/admin/users' },
    ];

    for (const action of actions) {
      const el = screen.getByTestId(`tech-action-${action.id}`);
      expect(el.tagName).toBe('A');
      expect(el).toHaveAttribute('href', action.href);
    }
  });

  it('renders separator dots between actions', () => {
    render(<TechActionsBar />);

    const separators = screen.getAllByText('·');
    // 4 actions → 3 separators
    expect(separators).toHaveLength(3);
  });
});
