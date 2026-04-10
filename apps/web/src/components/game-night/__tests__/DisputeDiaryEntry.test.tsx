/**
 * DisputeDiaryEntry — unit tests (GAP-006)
 *
 * Tests the `useDisputeDiary` hook directly: verifies that `createEntry`
 * calls `api.sessions.saveNote` with the correct parameters.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ---------------------------------------------------------------------------
// Mock api module — use vi.hoisted so the variable is available in the factory
// ---------------------------------------------------------------------------
const { mockSaveNote } = vi.hoisted(() => {
  const mockSaveNote = vi.fn().mockResolvedValue({
    id: 'note-1',
    noteType: 'dispute_resolved',
    content: '',
    isHidden: false,
    templateKey: null,
    createdAt: new Date().toISOString(),
  });
  return { mockSaveNote };
});

vi.mock('@/lib/api', () => ({
  api: {
    sessionTracking: {
      saveNote: mockSaveNote,
    },
  },
}));

// ---------------------------------------------------------------------------
// Import hook after mocks are set up
// ---------------------------------------------------------------------------
import { useDisputeDiary } from '@/lib/domain-hooks/useDisputeDiary';

// ---------------------------------------------------------------------------
// Helper: render hook in a proper React context
// ---------------------------------------------------------------------------
function renderDisputeDiaryHook() {
  const { result } = renderHook(() => useDisputeDiary());
  return result.current;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('useDisputeDiary', () => {
  beforeEach(() => {
    mockSaveNote.mockClear();
  });

  it('exports a createEntry function', () => {
    const { createEntry } = renderDisputeDiaryHook();
    expect(typeof createEntry).toBe('function');
  });

  it('calls api.sessions.saveNote with dispute_resolved noteType', async () => {
    const { createEntry } = renderDisputeDiaryHook();

    await createEntry({
      sessionId: 'session-abc',
      question: 'Can I move diagonally?',
      ruling: 'No, only orthogonal movement is allowed.',
    });

    expect(mockSaveNote).toHaveBeenCalledTimes(1);
    expect(mockSaveNote).toHaveBeenCalledWith(
      'session-abc',
      expect.objectContaining({
        noteType: 'dispute_resolved',
      })
    );
  });

  it('includes the question and ruling in the note content', async () => {
    const { createEntry } = renderDisputeDiaryHook();

    await createEntry({
      sessionId: 'session-abc',
      question: 'Chi ha ragione?',
      ruling: 'Il giocatore A ha ragione.',
    });

    const [, command] = mockSaveNote.mock.calls[0];
    expect(command.content).toContain('Chi ha ragione?');
    expect(command.content).toContain('Il giocatore A ha ragione.');
  });

  it('omits question line when question is empty', async () => {
    const { createEntry } = renderDisputeDiaryHook();

    await createEntry({
      sessionId: 'session-xyz',
      question: '',
      ruling: 'Regola confermata.',
    });

    const [, command] = mockSaveNote.mock.calls[0];
    expect(command.content).not.toContain('**Domanda**');
    expect(command.content).toContain('Regola confermata.');
  });

  it('includes sourceChunkId in content when provided', async () => {
    const { createEntry } = renderDisputeDiaryHook();

    await createEntry({
      sessionId: 'session-abc',
      question: 'Quante carte?',
      ruling: 'Sette carte.',
      sourceChunkId: 'chunk-42',
    });

    const [, command] = mockSaveNote.mock.calls[0];
    expect(command.content).toContain('chunk-42');
  });

  it('propagates API errors', async () => {
    mockSaveNote.mockRejectedValueOnce(new Error('Network error'));
    const { createEntry } = renderDisputeDiaryHook();

    await expect(
      createEntry({
        sessionId: 'session-abc',
        question: 'Question?',
        ruling: 'Ruling.',
      })
    ).rejects.toThrow('Network error');
  });
});
