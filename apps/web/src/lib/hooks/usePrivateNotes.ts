/**
 * usePrivateNotes Hook (Issue #3344)
 *
 * Manages private notes state, API calls, and SSE event handling.
 *
 * Features:
 * - Create and update encrypted private notes
 * - Reveal notes to all participants
 * - Hide previously revealed notes
 * - Real-time updates via SSE
 *
 * @example
 * ```typescript
 * const { notes, saveNote, revealNote, hideNote, deleteNote } = usePrivateNotes({
 *   sessionId: 'abc123',
 *   participantId: 'participant-id',
 * });
 *
 * await saveNote('My private note', 'Hint: chess move');
 * await revealNote(noteId);
 * ```
 */

import { useState, useCallback, useEffect } from 'react';

import type { SessionNote } from '@/components/session/types';

/**
 * API Response Types
 */
interface SaveNoteResponse {
  noteId: string;
  sessionId: string;
  participantId: string;
  isRevealed: boolean;
  obscuredText?: string;
  createdAt: string;
  updatedAt: string;
}

interface RevealNoteResponse {
  noteId: string;
  sessionId: string;
  participantId: string;
  content: string;
  isRevealed: boolean;
  revealedAt: string;
}

interface HideNoteResponse {
  noteId: string;
  sessionId: string;
  participantId: string;
  isRevealed: boolean;
  hiddenAt: string;
}

interface DeleteNoteResponse {
  noteId: string;
  deleted: boolean;
  deletedAt: string;
}

interface GetSessionNotesResponse {
  sessionId: string;
  notes: SessionNote[];
}

/**
 * SSE Events for notes
 */
interface NoteRevealedEventData {
  noteId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  content: string;
  timestamp: string;
}

interface NoteHiddenEventData {
  noteId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  timestamp: string;
}

interface NoteUpdatedEventData {
  noteId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  obscuredText?: string;
  isRevealed: boolean;
  content?: string;
  timestamp: string;
}

/**
 * Hook options
 */
export interface UsePrivateNotesOptions {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** API base URL */
  apiBaseUrl?: string;

  /** Callback when a note is revealed */
  onNoteRevealed?: (event: NoteRevealedEventData) => void;

  /** Callback when a note is hidden */
  onNoteHidden?: (event: NoteHiddenEventData) => void;

  /** Callback when a note is updated */
  onNoteUpdated?: (event: NoteUpdatedEventData) => void;
}

/**
 * Hook return value
 */
export interface PrivateNotesState {
  /** All visible notes (own + revealed by others) */
  notes: SessionNote[];

  /** Loading state */
  isLoading: boolean;

  /** Error state */
  error: Error | null;

  /** Save or update a note */
  saveNote: (content: string, obscuredText?: string, noteId?: string) => Promise<SaveNoteResponse>;

  /** Reveal a note to all participants */
  revealNote: (noteId: string) => Promise<RevealNoteResponse>;

  /** Hide a previously revealed note */
  hideNote: (noteId: string) => Promise<HideNoteResponse>;

  /** Delete a note */
  deleteNote: (noteId: string) => Promise<DeleteNoteResponse>;

  /** Refresh notes from server */
  refreshNotes: () => Promise<void>;

  /** Handle SSE events */
  addNoteEventFromSSE: (eventType: string, data: unknown) => void;
}

/**
 * usePrivateNotes Hook
 */
export function usePrivateNotes(options: UsePrivateNotesOptions): PrivateNotesState {
  const {
    sessionId,
    participantId,
    apiBaseUrl,
    onNoteRevealed,
    onNoteHidden,
    onNoteUpdated,
  } = options;

  const [notes, setNotes] = useState<SessionNote[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';

  /**
   * Fetch all visible notes
   */
  const refreshNotes = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/private-notes?requesterId=${participantId}`,
        {
          method: 'GET',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch notes: ${response.status}`);
      }

      const data: GetSessionNotesResponse = await response.json();
      setNotes(
        data.notes.map((n) => ({
          ...n,
          createdAt: new Date(n.createdAt),
          updatedAt: new Date(n.updatedAt),
        }))
      );
    } catch (err) {
      console.error('[usePrivateNotes] Failed to fetch notes:', err);
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId, participantId]);

  /**
   * Save or update a note
   */
  const saveNote = useCallback(
    async (
      content: string,
      obscuredText?: string,
      noteId?: string
    ): Promise<SaveNoteResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/private-notes`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              participantId,
              content,
              obscuredText,
              noteId,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Save note failed: ${response.status} - ${errorBody}`);
        }

        const data: SaveNoteResponse = await response.json();

        // Refresh notes to get the updated list
        await refreshNotes();

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId, refreshNotes]
  );

  /**
   * Reveal a note to all participants
   */
  const revealNote = useCallback(
    async (noteId: string): Promise<RevealNoteResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/private-notes/${noteId}/reveal`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              noteId,
              sessionId,
              participantId,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Reveal note failed: ${response.status} - ${errorBody}`);
        }

        const data: RevealNoteResponse = await response.json();

        // Update local state
        setNotes((prev) =>
          prev.map((n) =>
            n.id === noteId
              ? { ...n, isRevealed: true, content: data.content, updatedAt: new Date(data.revealedAt) }
              : n
          )
        );

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId]
  );

  /**
   * Hide a previously revealed note
   */
  const hideNote = useCallback(
    async (noteId: string): Promise<HideNoteResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/private-notes/${noteId}/hide`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              noteId,
              sessionId,
              participantId,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Hide note failed: ${response.status} - ${errorBody}`);
        }

        const data: HideNoteResponse = await response.json();

        // Update local state - hide content from non-owners
        setNotes((prev) =>
          prev.map((n) =>
            n.id === noteId
              ? {
                  ...n,
                  isRevealed: false,
                  content: n.isOwner ? n.content : undefined,
                  updatedAt: new Date(data.hiddenAt),
                }
              : n
          )
        );

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId]
  );

  /**
   * Delete a note
   */
  const deleteNote = useCallback(
    async (noteId: string): Promise<DeleteNoteResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/private-notes/${noteId}?participantId=${participantId}`,
          {
            method: 'DELETE',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Delete note failed: ${response.status} - ${errorBody}`);
        }

        const data: DeleteNoteResponse = await response.json();

        // Remove from local state
        setNotes((prev) => prev.filter((n) => n.id !== noteId));

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId]
  );

  /**
   * Handle SSE events
   */
  const addNoteEventFromSSE = useCallback(
    (eventType: string, data: unknown) => {
      switch (eventType) {
        case 'NoteRevealedEvent': {
          const event = data as NoteRevealedEventData;
          // Add or update the revealed note
          setNotes((prev) => {
            const existingIndex = prev.findIndex((n) => n.id === event.noteId);
            if (existingIndex >= 0) {
              // Update existing note
              const updated = [...prev];
              // eslint-disable-next-line security/detect-object-injection
              updated[existingIndex] = {
                // eslint-disable-next-line security/detect-object-injection
                ...updated[existingIndex],
                isRevealed: true,
                content: event.content,
                updatedAt: new Date(event.timestamp),
              };
              return updated;
            }
            // Add new revealed note from another participant
            return [
              {
                id: event.noteId,
                sessionId: event.sessionId,
                participantId: event.participantId,
                content: event.content,
                isOwner: false,
                isRevealed: true,
                createdAt: new Date(event.timestamp),
                updatedAt: new Date(event.timestamp),
              },
              ...prev,
            ];
          });
          onNoteRevealed?.(event);
          break;
        }
        case 'NoteHiddenEvent': {
          const event = data as NoteHiddenEventData;
          // Remove content for non-owned notes or remove if not owner
          setNotes((prev) =>
            prev
              .map((n) =>
                n.id === event.noteId
                  ? {
                      ...n,
                      isRevealed: false,
                      content: n.isOwner ? n.content : undefined,
                      updatedAt: new Date(event.timestamp),
                    }
                  : n
              )
              .filter((n) => n.isOwner || n.isRevealed)
          );
          onNoteHidden?.(event);
          break;
        }
        case 'NoteUpdatedEvent': {
          const event = data as NoteUpdatedEventData;
          setNotes((prev) =>
            prev.map((n) =>
              n.id === event.noteId
                ? {
                    ...n,
                    isRevealed: event.isRevealed,
                    content: event.content,
                    obscuredText: event.obscuredText,
                    updatedAt: new Date(event.timestamp),
                  }
                : n
            )
          );
          onNoteUpdated?.(event);
          break;
        }
      }
    },
    [onNoteRevealed, onNoteHidden, onNoteUpdated]
  );

  // Fetch notes on mount
  useEffect(() => {
    void refreshNotes();
  }, [refreshNotes]);

  return {
    notes,
    isLoading,
    error,
    saveNote,
    revealNote,
    hideNote,
    deleteNote,
    refreshNotes,
    addNoteEventFromSSE,
  };
}
