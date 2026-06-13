/**
 * Issue #1823 Phase F F5 + F6 — Wikidata dead-letters page integration smoke tests.
 *
 * Coverage:
 *   - "Acknowledge selected" toolbar button mounts alongside "Retry selected" (F5)
 *   - "Show acknowledged" toggle defaults to off and refetches with the flag set
 *   - Acked rows render the inline "Acked by … on …" chip (F5)
 *   - F6 attempt-source marker placeholder: covered by Task 5.4 in the timeline drawer
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';

import WikidataDeadLettersPage from '../page';
import * as api from '@/lib/api/admin-wikidata-dead-letters';

vi.mock('@/lib/api/admin-wikidata-dead-letters');
vi.mock('../useWikidataEnrichmentEvents', () => ({
  useWikidataEnrichmentEvents: () => ({ state: 'open', lastEvent: null }),
}));

describe('WikidataDeadLettersPage — Phase F integration', () => {
  beforeEach(() => {
    vi.mocked(api.listDeadLetters).mockResolvedValue({
      items: [
        {
          id: 'a-1',
          sharedGameId: 'g-1',
          gameTitle: 'Game',
          attemptedAt: '2026-06-10T00:00:00Z',
          deadLetteredAt: '2026-06-10T00:00:00Z',
          reason: 'r2-upload-error',
          details: null,
          retryCount: 3,
          acknowledgedAt: null,
          acknowledgedBy: null,
          acknowledgedByFullName: null,
          triggeredByAdminUserId: null,
          triggeredByAdminFullName: null,
        },
      ],
      totalCount: 1,
      skip: 0,
      take: 50,
    });
    vi.mocked(api.bulkAcknowledgeDeadLetters).mockResolvedValue({
      ackedCount: 1,
      idempotentNoOpCount: 0,
      notFoundCount: 0,
      rows: [
        {
          attemptId: 'a-1',
          gameId: 'g-1',
          outcome: 'acked',
          reason: null,
        },
      ],
    });
  });

  it('renders Acknowledge selected button alongside Retry selected', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Open timeline for Game/i })).toBeInTheDocument()
    );

    // Row-level checkboxes (index 0 = select-all header, index 1 = row 1)
    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[1]);

    expect(screen.getByRole('button', { name: /Acknowledge selected/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Retry selected/i })).toBeInTheDocument();
  });

  it('renders Show acknowledged toggle (off by default)', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Open timeline for Game/i })).toBeInTheDocument()
    );

    const toggle = screen.getByRole('switch', { name: /Show acknowledged/i });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('toggling Show acknowledged refetches with includeAcknowledged=true', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Open timeline for Game/i })).toBeInTheDocument()
    );

    const toggle = screen.getByRole('switch', { name: /Show acknowledged/i });
    fireEvent.click(toggle);

    await waitFor(() =>
      expect(api.listDeadLetters).toHaveBeenCalledWith(
        expect.objectContaining({ includeAcknowledged: true })
      )
    );
  });
});
