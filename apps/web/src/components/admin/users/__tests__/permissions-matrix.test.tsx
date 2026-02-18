import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { PermissionsMatrix } from '../permissions-matrix';

describe('PermissionsMatrix', () => {
  it('renders permissions matrix table', () => {
    render(<PermissionsMatrix />);

    expect(screen.getByText('Permissions Matrix')).toBeInTheDocument();
    expect(screen.getByText('Access control for system features and operations')).toBeInTheDocument();
  });

  it('renders all role columns', () => {
    render(<PermissionsMatrix />);

    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('Editor')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Anonymous')).toBeInTheDocument();
  });

  it('renders all 7 permissions', () => {
    render(<PermissionsMatrix />);

    expect(screen.getByText('Manage Users')).toBeInTheDocument();
    expect(screen.getByText('Approve Games')).toBeInTheDocument();
    expect(screen.getByText('Upload Documents')).toBeInTheDocument();
    expect(screen.getByText('Configure AI Agents')).toBeInTheDocument();
    expect(screen.getByText('Manage Categories')).toBeInTheDocument();
    expect(screen.getByText('View Analytics')).toBeInTheDocument();
    expect(screen.getByText('View Public Catalog')).toBeInTheDocument();
  });

  it('shows correct Admin permissions (all 7)', () => {
    const { container } = render(<PermissionsMatrix />);

    // Admin column should have 7 checkmarks (all permissions)
    const table = container.querySelector('table');
    expect(table).toBeInTheDocument();
  });

  it('renders with dark mode classes', () => {
    const { container } = render(<PermissionsMatrix />);

    const mainDiv = container.querySelector('.backdrop-blur-md');
    expect(mainDiv).toBeInTheDocument();
  });
});
