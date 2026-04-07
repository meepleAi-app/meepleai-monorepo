import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbBottomSheet } from '@/components/kb/KbBottomSheet';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const mockDocs: GameDocument[] = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000001',
    title: 'Rules v2',
    status: 'indexed',
    pageCount: 45,
    createdAt: '2026-01-01T00:00:00Z',
    category: 'Rulebook',
    versionLabel: '2nd Edition',
  },
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000002',
    title: 'Rules v1',
    status: 'indexed',
    pageCount: 38,
    createdAt: '2025-06-01T00:00:00Z',
    category: 'Rulebook',
    versionLabel: '1st Edition',
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

describe('KbBottomSheet', () => {
  it('renders game title in header', () => {
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Gloomhaven"
        documents={mockDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByText(/Knowledge Base — Gloomhaven/)).toBeInTheDocument();
  });

  it('groups documents by Rulebook and Other', () => {
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Gloomhaven"
        documents={mockDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByText('Regolamenti')).toBeInTheDocument();
    expect(screen.getByText('Altre Knowledge Base')).toBeInTheDocument();
  });

  it('hides Regolamenti section when no rulebooks', () => {
    const nonRulebookDocs: GameDocument[] = [
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
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={nonRulebookDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.queryByText('Regolamenti')).not.toBeInTheDocument();
    expect(screen.getByText('Altre Knowledge Base')).toBeInTheDocument();
  });

  it('calls onStartChat when CTA is clicked', () => {
    const onStartChat = vi.fn();
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={mockDocs}
        onStartChat={onStartChat}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Chatta/i }));
    expect(onStartChat).toHaveBeenCalled();
  });

  it('disables CTA when no indexed documents', () => {
    const processingDocs: GameDocument[] = [
      {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000001',
        title: 'Processing',
        status: 'processing',
        pageCount: 0,
        createdAt: '2026-01-01T00:00:00Z',
        category: 'Rulebook',
        versionLabel: null,
      },
    ];
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={processingDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: /Chatta/i })).toBeDisabled();
  });

  it('disables CTA when isLoading is true', () => {
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={mockDocs}
        onStartChat={() => {}}
        isLoading={true}
      />
    );
    expect(screen.getByRole('button', { name: /Chatta/i })).toBeDisabled();
  });

  it('does not render content when closed', () => {
    render(
      <KbBottomSheet
        open={false}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={mockDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.queryByText(/Knowledge Base/)).not.toBeInTheDocument();
  });
});
