/**
 * @vitest-environment jsdom
 *
 * KbFeedbackVersionSummary — #4469 (ADR-095 fetta 7a): i 👍👎 per versione del profilo.
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { AdminKbFeedbackVersion } from '@/lib/api/schemas/knowledge-base.schemas';

import { KbFeedbackVersionSummary } from '../kb-feedback-version-summary';

const V4 = '44444444-4444-4444-4444-444444444444';
const V3 = '33333333-3333-3333-3333-333333333333';

const VERSIONS: AdminKbFeedbackVersion[] = [
  {
    profileVersionId: V4,
    versionNumber: 4,
    status: 'Published',
    publishedAt: '2026-10-15T08:00:00Z',
    helpful: 3,
    notHelpful: 1,
  },
  {
    profileVersionId: V3,
    versionNumber: 3,
    status: 'Archived',
    publishedAt: '2026-10-10T08:00:00Z',
    helpful: 0,
    notHelpful: 2,
  },
  {
    profileVersionId: null,
    versionNumber: null,
    status: null,
    publishedAt: null,
    helpful: 1,
    notHelpful: 0,
  },
];

describe('KbFeedbackVersionSummary', () => {
  it('shows each version with its status and counts, and the feedback without a version apart', () => {
    render(<KbFeedbackVersionSummary versions={VERSIONS} selected={null} onSelect={vi.fn()} />);

    const rows = screen.getAllByRole('button');
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent('v4');
    expect(rows[0]).toHaveTextContent('Pubblicata');
    expect(within(rows[0]).getByLabelText('3 utili')).toBeInTheDocument();
    expect(within(rows[0]).getByLabelText('1 non utili')).toBeInTheDocument();
    expect(rows[1]).toHaveTextContent('v3');
    expect(rows[1]).toHaveTextContent('Archiviata');
    expect(within(rows[1]).getByLabelText('2 non utili')).toBeInTheDocument();
    expect(rows[2]).toHaveTextContent('Senza versione');
    expect(within(rows[2]).getByLabelText('1 utili')).toBeInTheDocument();
  });

  it('selects a version on click, and clears the filter on a second click', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const { rerender } = render(
      <KbFeedbackVersionSummary versions={VERSIONS} selected={null} onSelect={onSelect} />
    );

    await user.click(screen.getByRole('button', { name: /v3/ }));
    expect(onSelect).toHaveBeenLastCalledWith(V3);

    await user.click(screen.getByRole('button', { name: /Senza versione/ }));
    expect(onSelect).toHaveBeenLastCalledWith('none');

    rerender(<KbFeedbackVersionSummary versions={VERSIONS} selected={V3} onSelect={onSelect} />);
    const selected = screen.getByRole('button', { name: /v3/ });
    expect(selected).toHaveAttribute('aria-pressed', 'true');
    await user.click(selected);
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });

  it('says there is no feedback yet when the list is empty', () => {
    render(<KbFeedbackVersionSummary versions={[]} selected={null} onSelect={vi.fn()} />);

    expect(screen.getByText(/nessun feedback/i)).toBeInTheDocument();
  });
});
