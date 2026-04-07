import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbBottomSheet } from '@/components/kb/KbBottomSheet';
import { KbSelector } from '@/components/kb/KbSelector';
import { mapLibraryEntryToLinkedEntities } from '@/components/library/mapLinkedEntities';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

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
    title: 'FAQ',
    status: 'indexed',
    pageCount: 12,
    createdAt: '2026-02-01T00:00:00Z',
    category: 'Reference',
    versionLabel: null,
  },
];

const singleDoc: GameDocument[] = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaa000000001',
    title: 'Rules v1',
    status: 'indexed',
    pageCount: 30,
    createdAt: '2026-01-01T00:00:00Z',
    category: 'Rulebook',
    versionLabel: '1st Edition',
  },
];

function makeEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    userId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    gameId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    gameTitle: 'Test Game',
    addedAt: '2026-01-01T00:00:00Z',
    isFavorite: false,
    currentState: 'Owned' as const,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  } as UserLibraryEntry;
}

// ---------------------------------------------------------------------------
// Integration: Library -> KB -> Chat
// ---------------------------------------------------------------------------

describe('Library -> KB -> Chat Integration Flow', () => {
  // Step 1: mapLinkedEntities drives pip visibility
  describe('Step 1: ManaPip visibility from library entry', () => {
    it('hides KB pip when game has no indexed documents', () => {
      const entry = makeEntry({ kbIndexedCount: 0 });
      const pips = mapLibraryEntryToLinkedEntities(entry);
      expect(pips.find(p => p.entityType === 'kb')).toBeUndefined();
    });

    it('shows KB pip with count when game has indexed documents', () => {
      const entry = makeEntry({ hasKb: true, kbIndexedCount: 2 });
      const pips = mapLibraryEntryToLinkedEntities(entry);
      const kbPip = pips.find(p => p.entityType === 'kb');
      expect(kbPip).toBeDefined();
      expect(kbPip!.count).toBe(2);
    });
  });

  // Step 2: KbBottomSheet shows grouped documents and chat CTA
  describe('Step 2: KbBottomSheet document grouping', () => {
    it('groups rulebooks and other docs separately', () => {
      render(
        <KbBottomSheet
          open={true}
          onOpenChange={() => {}}
          gameTitle="Test Game"
          documents={mockDocs}
          onStartChat={() => {}}
        />
      );
      expect(screen.getByText('Regolamenti')).toBeInTheDocument();
      expect(screen.getByText('Altre Knowledge Base')).toBeInTheDocument();
      expect(screen.getByText('Rules v2')).toBeInTheDocument();
      expect(screen.getByText('FAQ')).toBeInTheDocument();
    });

    it('enables chat CTA when indexed docs exist', () => {
      render(
        <KbBottomSheet
          open={true}
          onOpenChange={() => {}}
          gameTitle="Test"
          documents={mockDocs}
          onStartChat={() => {}}
        />
      );
      const chatButton = screen.getByRole('button', { name: /Chatta/i });
      expect(chatButton).not.toBeDisabled();
    });
  });

  // Step 3: KbSelector pre-selection and confirmation
  describe('Step 3: KbSelector pre-selection and confirmation', () => {
    it('pre-selects latest rulebook and all other docs, then confirms with correct IDs', () => {
      const onConfirm = vi.fn();
      render(<KbSelector documents={mockDocs} onConfirm={onConfirm} />);

      // Latest rulebook (Rules v2) should be pre-selected
      const rulebookRadio = screen.getByRole('radio', { name: /Rules v2/ });
      expect(rulebookRadio).toBeChecked();

      // FAQ should be pre-selected (all other docs default selected)
      const faqCheckbox = screen.getByRole('checkbox', { name: /FAQ/ });
      expect(faqCheckbox).toBeChecked();

      // Confirm sends both IDs
      fireEvent.click(screen.getByRole('button', { name: /Avvia Chat/ }));
      expect(onConfirm).toHaveBeenCalledWith(
        expect.arrayContaining([mockDocs[0].id, mockDocs[1].id])
      );
    });

    it('allows deselecting other docs before confirming', () => {
      const onConfirm = vi.fn();
      render(<KbSelector documents={mockDocs} onConfirm={onConfirm} />);

      // Uncheck FAQ
      const faqCheckbox = screen.getByRole('checkbox', { name: /FAQ/ });
      fireEvent.click(faqCheckbox);
      expect(faqCheckbox).not.toBeChecked();

      // Confirm sends only rulebook ID
      fireEvent.click(screen.getByRole('button', { name: /Avvia Chat/ }));
      expect(onConfirm).toHaveBeenCalledWith([mockDocs[0].id]);
    });
  });

  // Step 4: Single-document shortcut
  describe('Step 4: Single document triggers direct chat', () => {
    it('bottom sheet with single doc fires onStartChat on CTA click', () => {
      const onStartChat = vi.fn();
      render(
        <KbBottomSheet
          open={true}
          onOpenChange={() => {}}
          gameTitle="Simple Game"
          documents={singleDoc}
          onStartChat={onStartChat}
        />
      );

      fireEvent.click(screen.getByRole('button', { name: /Chatta/i }));
      expect(onStartChat).toHaveBeenCalledOnce();
      // The parent component (PersonalLibraryPage) handles the "skip selector" logic
      // by checking indexedDocs.length === 1 before opening KbSelector
    });
  });

  // Step 5: End-to-end data flow (pure logic, no rendering)
  describe('Step 5: Data flow from library entry to selector output', () => {
    it('full chain: entry with KB -> pip visible -> docs render -> selector outputs IDs', () => {
      // 1. Library entry has KB
      const entry = makeEntry({ hasKb: true, kbIndexedCount: 2 });
      const pips = mapLibraryEntryToLinkedEntities(entry);
      const kbPip = pips.find(p => p.entityType === 'kb');
      expect(kbPip).toBeDefined();

      // 2. KbPip click would open bottom sheet (tested in Step 2)

      // 3. KbSelector confirms with document IDs
      const onConfirm = vi.fn();
      render(<KbSelector documents={mockDocs} onConfirm={onConfirm} />);
      fireEvent.click(screen.getByRole('button', { name: /Avvia Chat/ }));

      // 4. Verify IDs are valid UUIDs that could be passed to /chat/new?kbIds=...
      const confirmedIds = onConfirm.mock.calls[0][0] as string[];
      expect(confirmedIds.length).toBeGreaterThan(0);
      confirmedIds.forEach(id => {
        expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
      });
    });
  });
});
