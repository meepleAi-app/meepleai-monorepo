/**
 * Tests for useEntityActions — quick-action handler side effects (Issue #2776).
 *
 * Drives the hook directly per entity (the only way to exercise the non-`game`
 * branches, which have no rendered consumer today). Asserts the corrected side
 * effects for the 5 placeholder handlers + 2 sibling "Condividi" fixes.
 */
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useEntityActions } from '../useEntityActions';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

const mockGetPdfDownloadUrl = vi.fn((id: string) => `https://api.test/api/v1/pdfs/${id}/download`);
const mockExportChat = vi.fn<(chatId: string, req: { format: string }) => Promise<void>>();
vi.mock('@/lib/api', () => ({
  api: {
    pdf: { getPdfDownloadUrl: (id: string) => mockGetPdfDownloadUrl(id) },
    chat: { exportChat: (chatId: string, req: { format: string }) => mockExportChat(chatId, req) },
  },
}));

// Collection hooks — mocked to keep the hook out of React Query.
vi.mock('@/hooks/queries/useCollections', () => ({
  useAddToCollection: () => ({ mutate: vi.fn() }),
  useCollectionStatus: () => ({ data: undefined }),
  useRemoveFromCollection: () => ({ remove: vi.fn() }),
}));
vi.mock('@/hooks/useCollectionActions', () => ({
  useCollectionActions: () => ({ isInCollection: false, add: vi.fn(), remove: vi.fn() }),
}));

const mockWriteText = vi.fn<(text: string) => Promise<void>>();
const mockOpen = vi.fn();

// ============================================================================
// Helpers
// ============================================================================

const SESSION_ID = '00000000-0000-4000-8000-0000000000s1';
const KB_ID = '00000000-0000-4000-8000-0000000000k1';
const CHAT_ID = '00000000-0000-4000-8000-0000000000c1';
const PLAYER_ID = '00000000-0000-4000-8000-0000000000p1';
const EVENT_ID = '00000000-0000-4000-8000-0000000000e1';
const GAME_ID = '00000000-0000-4000-8000-0000000000g1';

type Entity = Parameters<typeof useEntityActions>[0]['entity'];

function actionsFor(entity: Entity, id: string, data?: Record<string, unknown>) {
  const { result } = renderHook(() => useEntityActions({ entity, id, userId: 'user-1', data }));
  return result.current.quickActions;
}

function clickByLabel(entity: Entity, id: string, label: string, data?: Record<string, unknown>) {
  const action = actionsFor(entity, id, data).find(a => a.label === label);
  if (!action) throw new Error(`action "${label}" not found for entity "${entity}"`);
  action.onClick();
  return action;
}

// ============================================================================
// Tests
// ============================================================================

describe('useEntityActions — quick-action side effects (#2776)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockWriteText.mockResolvedValue(undefined);
    mockExportChat.mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: mockWriteText },
      configurable: true,
    });
    vi.stubGlobal('open', mockOpen);
  });

  describe('session — Condividi codice', () => {
    it('copies the session code and shows a success toast', async () => {
      clickByLabel('session', SESSION_ID, 'Condividi codice', { sessionCode: 'SESS-CODE-123' });

      await waitFor(() => expect(mockWriteText).toHaveBeenCalledWith('SESS-CODE-123'));
      expect(mockToastSuccess).toHaveBeenCalledWith('Codice sessione copiato');
    });

    it('shows an error toast when the clipboard write fails', async () => {
      mockWriteText.mockRejectedValueOnce(new Error('denied'));

      clickByLabel('session', SESSION_ID, 'Condividi codice', { sessionCode: 'SESS-CODE-123' });

      await waitFor(() => expect(mockToastError).toHaveBeenCalled());
      expect(mockToastSuccess).not.toHaveBeenCalled();
    });
  });

  describe('kb — Download', () => {
    it('opens the correct pdf download URL (not the dead /documents route)', () => {
      clickByLabel('kb', KB_ID, 'Download');

      expect(mockGetPdfDownloadUrl).toHaveBeenCalledWith(KB_ID);
      expect(mockOpen).toHaveBeenCalledWith(
        `https://api.test/api/v1/pdfs/${KB_ID}/download`,
        '_blank'
      );
    });
  });

  describe('chat — Esporta', () => {
    it('calls the real export API with default pdf format + success toast', async () => {
      clickByLabel('chat', CHAT_ID, 'Esporta');

      await waitFor(() => expect(mockExportChat).toHaveBeenCalledWith(CHAT_ID, { format: 'pdf' }));
      expect(mockToastSuccess).toHaveBeenCalledWith('Chat esportata');
      expect(mockPush).not.toHaveBeenCalled();
    });

    it('shows an error toast when the export fails', async () => {
      mockExportChat.mockRejectedValueOnce(new Error('boom'));

      clickByLabel('chat', CHAT_ID, 'Esporta');

      await waitFor(() => expect(mockToastError).toHaveBeenCalled());
    });
  });

  describe('player — Invita a Sessione', () => {
    it('navigates to the session wizard with the invitePlayer param', () => {
      clickByLabel('player', PLAYER_ID, 'Invita a Sessione');

      expect(mockPush).toHaveBeenCalledWith(`/sessions/new?invitePlayer=${PLAYER_ID}`);
    });
  });

  describe('event — Partecipa', () => {
    it('navigates to the real game-nights detail route (not the dead /events route)', () => {
      clickByLabel('event', EVENT_ID, 'Partecipa');

      expect(mockPush).toHaveBeenCalledWith(`/game-nights/${EVENT_ID}`);
    });
  });

  describe('event — Condividi (sibling)', () => {
    it('copies the game-nights URL (not the dead /events URL) with a success toast', async () => {
      clickByLabel('event', EVENT_ID, 'Condividi');

      await waitFor(() =>
        expect(mockWriteText).toHaveBeenCalledWith(
          `${window.location.origin}/game-nights/${EVENT_ID}`
        )
      );
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });

  describe('game — Condividi (sibling)', () => {
    it('copies the game URL and shows a success toast', async () => {
      clickByLabel('game', GAME_ID, 'Condividi');

      await waitFor(() =>
        expect(mockWriteText).toHaveBeenCalledWith(`${window.location.origin}/games/${GAME_ID}`)
      );
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });
});
