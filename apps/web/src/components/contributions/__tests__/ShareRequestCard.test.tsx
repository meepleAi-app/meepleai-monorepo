/**
 * ShareRequestCard Component Tests
 *
 * Issue #2744: Frontend - Dashboard Contributi Utente
 *
 * Tests:
 * - Card rendering (thumbnail, title, status)
 * - Metadata display (dates, document count)
 * - Admin feedback display
 * - Action buttons visibility
 * - Link navigation
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ShareRequestCard } from '../ShareRequestCard';
import type { UserShareRequestDto } from '@/lib/api/schemas/share-requests.schemas';

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '2 days ago'),
}));

// Test data
const mockRequestPending: UserShareRequestDto = {
  id: 'req-1',
  sourceGameId: 'game-1',
  gameTitle: 'Catan',
  gameThumbnailUrl: 'https://example.com/catan.jpg',
  status: 'Pending',
  contributionType: 'NewGame',
  userNotes: 'Great game for families',
  adminFeedback: null,
  attachedDocumentCount: 2,
  createdAt: '2024-01-15T10:00:00Z',
  resolvedAt: null,
  resultingSharedGameId: null,
};

const mockRequestChangesRequested: UserShareRequestDto = {
  ...mockRequestPending,
  id: 'req-2',
  status: 'ChangesRequested',
  adminFeedback: 'Please add more detailed rules',
};

const mockRequestApproved: UserShareRequestDto = {
  ...mockRequestPending,
  id: 'req-3',
  status: 'Approved',
  resolvedAt: '2024-01-20T14:00:00Z',
  resultingSharedGameId: 'shared-1',
};

describe('ShareRequestCard', () => {
  it('renders game title and thumbnail', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByAltText('Catan')).toBeInTheDocument();
  });

  it('displays contribution status badge', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByText('Pending')).toBeInTheDocument();
  });

  it('displays contribution type badge', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByText('New Game')).toBeInTheDocument();
  });

  it('displays document count', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByText(/2 documents/i)).toBeInTheDocument();
  });

  it('displays user notes when present', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByText('Great game for families')).toBeInTheDocument();
  });

  it('displays admin feedback when present', () => {
    render(<ShareRequestCard request={mockRequestChangesRequested} />);

    expect(screen.getByText('Please add more detailed rules')).toBeInTheDocument();
  });

  it('shows Update button for ChangesRequested status', () => {
    render(<ShareRequestCard request={mockRequestChangesRequested} />);

    const updateButton = screen.getByText('Update');
    expect(updateButton).toBeInTheDocument();
    expect(updateButton.closest('a')).toHaveAttribute(
      'href',
      '/contributions/requests/req-2/edit'
    );
  });

  it('shows View Game button for Approved status', () => {
    render(<ShareRequestCard request={mockRequestApproved} />);

    const viewButton = screen.getByText('View Game');
    expect(viewButton).toBeInTheDocument();
    expect(viewButton.closest('a')).toHaveAttribute('href', '/games/catalog/shared-1');
  });

  it('always shows Details button', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    const detailsButton = screen.getByText('Details');
    expect(detailsButton).toBeInTheDocument();
    expect(detailsButton.closest('a')).toHaveAttribute('href', '/contributions/requests/req-1');
  });

  it('renders fallback icon when no thumbnail', () => {
    const requestNoThumb = { ...mockRequestPending, gameThumbnailUrl: null };
    const { container } = render(<ShareRequestCard request={requestNoThumb} />);

    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  it('displays resolved date for resolved requests', () => {
    render(<ShareRequestCard request={mockRequestApproved} />);

    expect(screen.getByText(/Resolved/i)).toBeInTheDocument();
  });
});
