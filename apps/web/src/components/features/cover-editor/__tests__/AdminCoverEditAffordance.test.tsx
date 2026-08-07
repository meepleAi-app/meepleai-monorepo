/**
 * @vitest-environment jsdom
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/useAdminRole', () => ({ useAdminRole: vi.fn() }));

// Stub the dialog so this unit test isolates the trigger + role gate + open/close.
vi.mock('../AdminCoverSourceDialog', () => ({
  AdminCoverSourceDialog: ({ open, onClose }: { open: boolean; onClose: () => void }) =>
    open ? (
      <div data-testid="cover-dialog">
        <button type="button" onClick={onClose}>
          close
        </button>
      </div>
    ) : null,
}));

import { useAdminRole } from '@/hooks/useAdminRole';

import { AdminCoverEditAffordance } from '../AdminCoverEditAffordance';

const mockUseAdminRole = useAdminRole as unknown as ReturnType<typeof vi.fn>;
const GID = '550e8400-e29b-41d4-a716-446655440000';

function setRole(isEditorOrAbove: boolean) {
  mockUseAdminRole.mockReturnValue({
    user: null,
    isSuperAdmin: false,
    isAdminOrAbove: isEditorOrAbove,
    isEditorOrAbove,
    hasRole: () => false,
    isLoading: false,
  });
}

beforeEach(() => vi.clearAllMocks());

describe('AdminCoverEditAffordance', () => {
  it('renders nothing for a non-admin (invisible on public surfaces)', () => {
    setRole(false);
    const { container } = render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.queryByRole('button', { name: /copertina/i })).not.toBeInTheDocument();
    expect(container).toBeEmptyDOMElement();
  });

  it('renders a labeled edit trigger for an editor-or-above', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.getByRole('button', { name: /copertina/i })).toBeInTheDocument();
    expect(screen.queryByTestId('cover-dialog')).not.toBeInTheDocument();
  });

  it('opens the source dialog on click and closes it via onClose', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);

    fireEvent.click(screen.getByRole('button', { name: /copertina/i }));
    expect(screen.getByTestId('cover-dialog')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(screen.queryByTestId('cover-dialog')).not.toBeInTheDocument();
  });

  it('è visibile a riposo su desktop: niente opacity-0 (#3611)', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.getByRole('button', { name: /copertina/i }).className).not.toMatch(/opacity-0/);
  });

  it('marca la cover da sistemare quando needsAttention', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" needsAttention />);
    expect(screen.getByTestId('cover-needs-attention')).toBeInTheDocument();
  });

  it('non marca nulla senza needsAttention', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.queryByTestId('cover-needs-attention')).not.toBeInTheDocument();
  });
});
