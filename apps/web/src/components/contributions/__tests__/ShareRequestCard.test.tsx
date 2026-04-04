/**
 * ShareRequestCard Component Tests
 * Issue #2744: Frontend - Dashboard Contributi Utente
 * Issue #4860: Updated for MeepleCard migration
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ShareRequestCard } from '../ShareRequestCard';
import type { UserShareRequestDto } from '@/lib/api/schemas/share-requests.schemas';

// Mock MeepleCard
const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return <div data-testid={props['data-testid'] as string}>MeepleCard</div>;
  },
}));

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '2 days ago'),
}));

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
  beforeEach(() => {
    mockMeepleCard.mockClear();
    mockPush.mockClear();
  });

  it('renders with entity="game" and variant="list"', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'game',
        variant: 'list',
      })
    );
  });

  it('passes game title', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ title: 'Catan' }));
  });

  it('passes contribution type and date in subtitle', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        subtitle: expect.stringContaining('New Game'),
      })
    );
  });

  it('passes AdditionalContent type in subtitle', () => {
    const additionalContentReq = {
      ...mockRequestPending,
      contributionType: 'AdditionalContent' as const,
    };
    render(<ShareRequestCard request={additionalContentReq} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        subtitle: expect.stringContaining('Additional Content'),
      })
    );
  });

  it('passes thumbnail as imageUrl', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ imageUrl: 'https://example.com/catan.jpg' })
    );
  });

  it('passes undefined imageUrl when thumbnail is null', () => {
    const noThumbReq = { ...mockRequestPending, gameThumbnailUrl: null };
    render(<ShareRequestCard request={noThumbReq} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ imageUrl: undefined }));
  });

  it('passes status as badge', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ badge: 'In attesa' }));
  });

  it('passes Approved status badge', () => {
    render(<ShareRequestCard request={mockRequestApproved} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ badge: 'Approvato' }));
  });

  it('passes document count in metadata', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata).toHaveLength(1);
    expect(metadata[0].label).toBe('2 docs');
  });

  it('omits metadata when no documents', () => {
    const noDocsReq = { ...mockRequestPending, attachedDocumentCount: 0 };
    render(<ShareRequestCard request={noDocsReq} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ metadata: undefined }));
  });

  it('includes Details action for all requests', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;

    const detailsAction = actions.find(a => a.label === 'Details');
    expect(detailsAction).toBeDefined();

    detailsAction?.onClick();
    expect(mockPush).toHaveBeenCalledWith('/contributions/requests/req-1');
  });

  it('includes Update action for ChangesRequested status', () => {
    render(<ShareRequestCard request={mockRequestChangesRequested} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;

    const updateAction = actions.find(a => a.label === 'Update');
    expect(updateAction).toBeDefined();

    updateAction?.onClick();
    expect(mockPush).toHaveBeenCalledWith('/contributions/requests/req-2/edit');
  });

  it('includes View Game action for Approved status with result', () => {
    render(<ShareRequestCard request={mockRequestApproved} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;

    const viewGameAction = actions.find(a => a.label === 'View Game');
    expect(viewGameAction).toBeDefined();

    viewGameAction?.onClick();
    expect(mockPush).toHaveBeenCalledWith('/library/games/shared-1');
  });

  it('does not include Update action for Pending status', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string }>;

    expect(actions.find(a => a.label === 'Update')).toBeUndefined();
  });

  it('enables preview when user notes exist', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        showPreview: true,
        previewData: expect.objectContaining({
          description: expect.stringContaining('Great game for families'),
        }),
      })
    );
  });

  it('includes admin feedback in preview', () => {
    render(<ShareRequestCard request={mockRequestChangesRequested} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        previewData: expect.objectContaining({
          description: expect.stringContaining('Please add more detailed rules'),
        }),
      })
    );
  });

  it('disables preview when no notes or feedback', () => {
    const noNotesReq = { ...mockRequestPending, userNotes: null };
    render(<ShareRequestCard request={noNotesReq} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(expect.objectContaining({ showPreview: false }));
  });

  it('sets correct data-testid', () => {
    render(<ShareRequestCard request={mockRequestPending} />);

    expect(screen.getByTestId('share-request-card-req-1')).toBeInTheDocument();
  });
});
