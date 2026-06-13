import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AttemptTimelineDrawer } from '../AttemptTimelineDrawer';
import * as api from '@/lib/api/admin-wikidata-dead-letters';

vi.mock('@/lib/api/admin-wikidata-dead-letters');

// Stub Radix Drawer with a simple wrapper that mounts children inline (no portal,
// no focus trap, no aria-hidden) so queries can find the inner content
// synchronously in the test DOM. Behaviour of Phase F badge is independent of
// the drawer chrome.
vi.mock('@/components/ui/drawer', () => ({
  Drawer: ({ children, open }: { children: ReactNode; open: boolean }) =>
    open ? <div data-testid="drawer">{children}</div> : null,
  DrawerContent: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DrawerHeader: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  DrawerTitle: ({ children }: { children: ReactNode }) => <h2>{children}</h2>,
  DrawerDescription: ({ children }: { children: ReactNode }) => <p>{children}</p>,
}));

describe('AttemptTimelineDrawer — F6 admin badge', () => {
  it('shows admin badge when triggeredByAdminUserId is non-null', async () => {
    vi.mocked(api.getAttemptTimeline).mockResolvedValue({
      gameId: 'g-1',
      items: [
        {
          id: 'a-1',
          attemptedAt: '2026-06-10T00:00:00Z',
          outcome: 'Success',
          reason: 'success',
          details: null,
          retryCount: 0,
          nextRetryAt: null,
          deadLetteredAt: null,
          triggeredByAdminUserId: 'admin-1',
          triggeredByAdminFullName: 'Alice Admin',
        },
      ],
    });
    render(<AttemptTimelineDrawer gameId="g-1" gameTitle="Test Game" open onClose={() => {}} />);

    // Wait for the async timeline load to complete (ol with aria-label renders post-fetch)
    await screen.findByRole('list', { name: /Attempt history/i });

    const badge = screen.getByText(
      (_, node) => node?.textContent === 'admin' && node.tagName === 'SPAN'
    );
    expect(badge).toBeInTheDocument();
    expect(badge.getAttribute('title')).toBe('Triggered by admin Alice Admin');
  });

  it('hides admin badge when triggeredByAdminUserId is null', async () => {
    vi.mocked(api.getAttemptTimeline).mockResolvedValue({
      gameId: 'g-1',
      items: [
        {
          id: 'a-1',
          attemptedAt: '2026-06-10T00:00:00Z',
          outcome: 'Success',
          reason: 'success',
          details: null,
          retryCount: 0,
          nextRetryAt: null,
          deadLetteredAt: null,
          triggeredByAdminUserId: null,
          triggeredByAdminFullName: null,
        },
      ],
    });
    render(<AttemptTimelineDrawer gameId="g-1" gameTitle="Test Game" open onClose={() => {}} />);

    await screen.findByRole('list', { name: /Attempt history/i });

    expect(
      screen.queryByText((_, node) => node?.textContent === 'admin' && node.tagName === 'SPAN')
    ).not.toBeInTheDocument();
  });

  it('shows fallback tooltip when admin user is deleted (id non-null but fullName null)', async () => {
    vi.mocked(api.getAttemptTimeline).mockResolvedValue({
      gameId: 'g-1',
      items: [
        {
          id: 'a-1',
          attemptedAt: '2026-06-10T00:00:00Z',
          outcome: 'Success',
          reason: 'success',
          details: null,
          retryCount: 0,
          nextRetryAt: null,
          deadLetteredAt: null,
          triggeredByAdminUserId: 'deleted-admin-id',
          triggeredByAdminFullName: null,
        },
      ],
    });
    render(<AttemptTimelineDrawer gameId="g-1" gameTitle="Test Game" open onClose={() => {}} />);

    await screen.findByRole('list', { name: /Attempt history/i });

    const badge = screen.getByText(
      (_, node) => node?.textContent === 'admin' && node.tagName === 'SPAN'
    );
    expect(badge.getAttribute('title')).toBe('Triggered by admin (deleted user)');
  });
});
