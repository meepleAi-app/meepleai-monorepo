/**
 * ReviewConflictDialog Component Tests
 *
 * Test Coverage:
 * - Dialog open/close states
 * - Admin name display
 * - Fallback for unknown admin
 * - OK button interaction
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 * Target: ≥85% coverage
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ReviewConflictDialog } from '../ReviewConflictDialog';

describe('ReviewConflictDialog', () => {
  it('does not render when closed', () => {
    render(
      <ReviewConflictDialog
        open={false}
        onClose={vi.fn()}
        conflictDetails={{ adminName: 'Sarah Johnson', adminId: 'admin-123' }}
      />
    );

    expect(screen.queryByText('Review Already In Progress')).not.toBeInTheDocument();
  });

  it('renders when open with admin name', () => {
    render(
      <ReviewConflictDialog
        open={true}
        onClose={vi.fn()}
        conflictDetails={{ adminName: 'Sarah Johnson', adminId: 'admin-123' }}
      />
    );

    expect(screen.getByText('Review Already In Progress')).toBeInTheDocument();
    expect(screen.getByText(/Sarah Johnson/)).toBeInTheDocument();
  });

  it('shows fallback for unknown admin', () => {
    render(
      <ReviewConflictDialog open={true} onClose={vi.fn()} conflictDetails={null} />
    );

    expect(screen.getByText(/another admin/)).toBeInTheDocument();
  });

  it('calls onClose when OK button is clicked', () => {
    const onClose = vi.fn();

    render(
      <ReviewConflictDialog
        open={true}
        onClose={onClose}
        conflictDetails={{ adminName: 'Sarah Johnson', adminId: 'admin-123' }}
      />
    );

    const okButton = screen.getByRole('button', { name: /OK/i });
    fireEvent.click(okButton);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders conflict message with visual warning', () => {
    render(
      <ReviewConflictDialog
        open={true}
        onClose={vi.fn()}
        conflictDetails={{ adminName: 'Sarah', adminId: 'admin-1' }}
      />
    );

    // Verify meaningful content rather than icon implementation
    expect(screen.getByText('Review Already In Progress')).toBeInTheDocument();
    expect(screen.getByText(/currently being reviewed by/i)).toBeInTheDocument();
    expect(screen.getByText('Sarah')).toBeInTheDocument();
  });
});
