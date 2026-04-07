import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbSelector } from '@/components/kb/KbSelector';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const docs: GameDocument[] = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000001',
    title: 'Rules v1',
    status: 'indexed',
    pageCount: 38,
    createdAt: '2025-06-01T00:00:00Z',
    category: 'Rulebook',
    versionLabel: '1st Edition',
  },
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000002',
    title: 'Rules v2',
    status: 'indexed',
    pageCount: 45,
    createdAt: '2026-01-01T00:00:00Z',
    category: 'Rulebook',
    versionLabel: '2nd Edition',
  },
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000003',
    title: 'FAQ',
    status: 'indexed',
    pageCount: 12,
    createdAt: '2026-02-01T00:00:00Z',
    category: 'Reference',
    versionLabel: null,
  },
];

describe('KbSelector', () => {
  it('pre-selects latest rulebook (by createdAt)', () => {
    render(<KbSelector documents={docs} onConfirm={() => {}} />);
    // Rules v2 (createdAt 2026-01-01) is later than Rules v1 (2025-06-01)
    const radioV2 = screen.getByRole('radio', { name: /Rules v2/ });
    expect(radioV2).toBeChecked();

    const radioV1 = screen.getByRole('radio', { name: /Rules v1/ });
    expect(radioV1).not.toBeChecked();
  });

  it('pre-selects all other docs (checkboxes)', () => {
    render(<KbSelector documents={docs} onConfirm={() => {}} />);
    const faqCheckbox = screen.getByRole('checkbox', { name: /FAQ/ });
    expect(faqCheckbox).toBeChecked();
  });

  it('calls onConfirm with selected IDs (latest rulebook + all others)', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);
    fireEvent.click(screen.getByText(/Avvia Chat/));
    // Should include Rules v2 ID + FAQ ID
    expect(onConfirm).toHaveBeenCalledWith(
      expect.arrayContaining([
        'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000002',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000003',
      ])
    );
    expect(onConfirm.mock.calls[0][0]).toHaveLength(2);
  });

  it('allows switching rulebook version', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);

    // Switch to Rules v1
    fireEvent.click(screen.getByRole('radio', { name: /Rules v1/ }));
    expect(screen.getByRole('radio', { name: /Rules v1/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /Rules v2/ })).not.toBeChecked();

    fireEvent.click(screen.getByText(/Avvia Chat/));
    expect(onConfirm).toHaveBeenCalledWith(
      expect.arrayContaining([
        'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000001',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000003',
      ])
    );
  });

  it('allows deselecting other docs', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);

    // Deselect FAQ
    fireEvent.click(screen.getByRole('checkbox', { name: /FAQ/ }));
    expect(screen.getByRole('checkbox', { name: /FAQ/ })).not.toBeChecked();

    fireEvent.click(screen.getByText(/Avvia Chat/));
    // Only rulebook selected
    expect(onConfirm).toHaveBeenCalledWith(['aaaaaaaa-aaaa-aaaa-aaaa-aaa000000002']);
  });

  it('filters out non-indexed docs', () => {
    const mixedDocs: GameDocument[] = [
      ...docs,
      {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000004',
        title: 'Processing Doc',
        status: 'processing',
        pageCount: 0,
        createdAt: '2026-03-01T00:00:00Z',
        category: 'Errata',
        versionLabel: null,
      },
    ];
    render(<KbSelector documents={mixedDocs} onConfirm={() => {}} />);
    // Processing doc should not appear
    expect(screen.queryByText('Processing Doc')).not.toBeInTheDocument();
  });

  it('shows cancel button when onCancel provided', () => {
    const onCancel = vi.fn();
    render(<KbSelector documents={docs} onConfirm={() => {}} onCancel={onCancel} />);
    fireEvent.click(screen.getByText('Annulla'));
    expect(onCancel).toHaveBeenCalled();
  });
});
