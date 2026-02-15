/**
 * NotificationItem Component Tests (Issue #4412)
 *
 * Tests for individual notification card:
 * - Message truncation at 50 characters with ellipsis
 * - Short messages displayed intact
 * - Click to mark as read and navigate
 * - Severity-based styling
 * - Unread indicator
 *
 * Coverage target: ≥85%
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { NotificationItem } from '../NotificationItem';
import type { NotificationDto } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
const mockMarkAsRead = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('@/components/pdf', () => ({
  PdfStatusBadge: ({ state }: { state: string }) => (
    <span data-testid="pdf-status-badge">{state}</span>
  ),
}));

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: Record<string, unknown>) => unknown) => {
    const state = { markAsRead: mockMarkAsRead };
    return typeof selector === 'function' ? selector(state) : state;
  }),
}));

// ============================================================================
// Helpers
// ============================================================================

function createNotification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    type: 'pdf_upload_completed',
    severity: 'success',
    title: 'PDF Ready',
    message: 'Short message',
    link: null,
    metadata: null,
    isRead: false,
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('NotificationItem', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ---- Message Truncation ----

  it('should truncate messages longer than 50 characters with ellipsis', () => {
    const longMessage = 'A'.repeat(60); // 60 chars
    const notification = createNotification({ message: longMessage });

    render(<NotificationItem notification={notification} />);

    const truncated = 'A'.repeat(50) + '...';
    expect(screen.getByText(truncated)).toBeInTheDocument();
  });

  it('should display short messages (≤50 chars) intact without ellipsis', () => {
    const shortMessage = 'This is a short message';
    const notification = createNotification({ message: shortMessage });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByText(shortMessage)).toBeInTheDocument();
    expect(screen.queryByText(/\.\.\./)).not.toBeInTheDocument();
  });

  it('should show tooltip title only for truncated messages', () => {
    const longMessage = 'A'.repeat(60);
    const notification = createNotification({ message: longMessage });

    const { container } = render(<NotificationItem notification={notification} />);

    const messageEl = container.querySelector('p[title]');
    expect(messageEl).toBeInTheDocument();
    expect(messageEl).toHaveAttribute('title', longMessage);
  });

  it('should not show tooltip title for short messages', () => {
    const shortMessage = 'Short';
    const notification = createNotification({ message: shortMessage });

    const { container } = render(<NotificationItem notification={notification} />);

    const messageEl = container.querySelector('.text-xs.text-muted-foreground.line-clamp-2');
    expect(messageEl).toBeInTheDocument();
    expect(messageEl).not.toHaveAttribute('title');
  });

  it('should handle exactly 50-character messages without truncation', () => {
    const exactMessage = 'A'.repeat(50);
    const notification = createNotification({ message: exactMessage });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByText(exactMessage)).toBeInTheDocument();
  });

  // ---- Click Behavior ----

  it('should mark unread notification as read on click', async () => {
    const user = userEvent.setup();
    const notification = createNotification({ isRead: false });

    render(<NotificationItem notification={notification} />);

    const button = screen.getByRole('button');
    await user.click(button);

    expect(mockMarkAsRead).toHaveBeenCalledWith(notification.id);
  });

  it('should not call markAsRead for already-read notifications', async () => {
    const user = userEvent.setup();
    const notification = createNotification({ isRead: true });

    render(<NotificationItem notification={notification} />);

    await user.click(screen.getByRole('button'));

    expect(mockMarkAsRead).not.toHaveBeenCalled();
  });

  it('should navigate to link on click when link exists', async () => {
    const user = userEvent.setup();
    const notification = createNotification({ link: '/games/123', isRead: true });

    render(<NotificationItem notification={notification} />);

    await user.click(screen.getByRole('button'));

    expect(mockPush).toHaveBeenCalledWith('/games/123');
  });

  it('should not navigate when no link exists', async () => {
    const user = userEvent.setup();
    const notification = createNotification({ link: null, isRead: true });

    render(<NotificationItem notification={notification} />);

    await user.click(screen.getByRole('button'));

    expect(mockPush).not.toHaveBeenCalled();
  });

  // ---- Visual States ----

  it('should show unread indicator dot for unread notifications', () => {
    const notification = createNotification({ isRead: false });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByLabelText('Unread')).toBeInTheDocument();
  });

  it('should not show unread indicator for read notifications', () => {
    const notification = createNotification({ isRead: true });

    render(<NotificationItem notification={notification} />);

    expect(screen.queryByLabelText('Unread')).not.toBeInTheDocument();
  });

  it('should display notification title', () => {
    const notification = createNotification({ title: 'Upload Complete' });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByText('Upload Complete')).toBeInTheDocument();
  });

  it('should show PdfStatusBadge for pdf_upload_completed type', () => {
    const notification = createNotification({ type: 'pdf_upload_completed' });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByTestId('pdf-status-badge')).toHaveTextContent('ready');
  });

  it('should show PdfStatusBadge for processing_failed type', () => {
    const notification = createNotification({ type: 'processing_failed', severity: 'error' });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByTestId('pdf-status-badge')).toHaveTextContent('failed');
  });

  it('should have correct ARIA label', () => {
    const notification = createNotification({ title: 'My Notification' });

    render(<NotificationItem notification={notification} />);

    expect(screen.getByRole('button')).toHaveAttribute(
      'aria-label',
      'Notification: My Notification'
    );
  });
});
