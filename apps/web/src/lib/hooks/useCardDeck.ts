/**
 * useCardDeck Hook (Issue #3343)
 *
 * Manages card deck state, API calls, and SSE event handling.
 *
 * Features:
 * - Create standard or custom decks
 * - Draw, discard, and shuffle cards
 * - Track player hands (private) and discard pile (shared)
 * - Real-time updates via SSE
 *
 * @example
 * ```typescript
 * const { decks, hand, discardPile, drawCards, discardCards, shuffleDeck } = useCardDeck({
 *   sessionId: 'abc123',
 *   participantId: 'participant-id',
 * });
 *
 * await drawCards(deckId, 5);
 * await discardCards(deckId, [cardId1, cardId2]);
 * ```
 */

import { useState, useCallback, useEffect } from 'react';

import type { Card, SessionDeck, PlayerHand, DiscardPile } from '@/components/session/types';

/**
 * API Response Types
 */
interface CreateDeckResponse {
  deckId: string;
  name: string;
  deckType: string;
  cardCount: number;
  createdAt: string;
}

interface DrawCardsResponse {
  deckId: string;
  participantId: string;
  cards: Card[];
  remainingCards: number;
}

interface DiscardCardsResponse {
  deckId: string;
  participantId: string;
  discardedCards: Card[];
  handSize: number;
}

interface ShuffleDeckResponse {
  deckId: string;
  cardsInDrawPile: number;
  shuffledAt: string;
}

/**
 * SSE Events for cards
 */
interface CardsDrawnEventData {
  deckId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  cardCount: number;
  cardIds: string[];
  remainingCards: number;
  timestamp: string;
}

interface CardsDiscardedEventData {
  deckId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  cards: Card[];
  timestamp: string;
}

interface DeckShuffledEventData {
  deckId: string;
  sessionId: string;
  deckName: string;
  cardsInDrawPile: number;
  timestamp: string;
}

/**
 * Hook options
 */
export interface UseCardDeckOptions {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** API base URL */
  apiBaseUrl?: string;

  /** Callback when cards are drawn by any participant */
  onCardsDrawn?: (event: CardsDrawnEventData) => void;

  /** Callback when cards are discarded */
  onCardsDiscarded?: (event: CardsDiscardedEventData) => void;

  /** Callback when deck is shuffled */
  onDeckShuffled?: (event: DeckShuffledEventData) => void;
}

/**
 * Hook return value
 */
export interface CardDeckState {
  /** All decks in the session */
  decks: SessionDeck[];

  /** Current participant's hand per deck */
  hands: Record<string, PlayerHand>;

  /** Discard piles per deck */
  discardPiles: Record<string, DiscardPile>;

  /** Loading state */
  isLoading: boolean;

  /** Error state */
  error: Error | null;

  /** Create a new deck */
  createDeck: (name: string, deckType: 'standard' | 'custom', options?: {
    includeJokers?: boolean;
    customCards?: { name: string; imageUrl?: string; suit?: string; value?: string }[];
  }) => Promise<CreateDeckResponse>;

  /** Draw cards from a deck */
  drawCards: (deckId: string, count: number) => Promise<DrawCardsResponse>;

  /** Discard cards from hand */
  discardCards: (deckId: string, cardIds: string[]) => Promise<DiscardCardsResponse>;

  /** Shuffle a deck */
  shuffleDeck: (deckId: string, includeDiscard?: boolean) => Promise<ShuffleDeckResponse>;

  /** Refresh decks from server */
  refreshDecks: () => Promise<void>;

  /** Refresh player hand from server */
  refreshHand: (deckId: string) => Promise<void>;

  /** Refresh discard pile from server */
  refreshDiscardPile: (deckId: string) => Promise<void>;

  /** Handle SSE events */
  addDeckEventFromSSE: (eventType: string, data: unknown) => void;
}

/**
 * useCardDeck Hook
 */
export function useCardDeck(options: UseCardDeckOptions): CardDeckState {
  const {
    sessionId,
    participantId,
    apiBaseUrl,
    onCardsDrawn,
    onCardsDiscarded,
    onDeckShuffled,
  } = options;

  const [decks, setDecks] = useState<SessionDeck[]>([]);
  const [hands, setHands] = useState<Record<string, PlayerHand>>({});
  const [discardPiles, setDiscardPiles] = useState<Record<string, DiscardPile>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';

  /**
   * Fetch all decks in the session
   */
  const refreshDecks = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/decks`,
        {
          method: 'GET',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch decks: ${response.status}`);
      }

      const data: SessionDeck[] = await response.json();
      setDecks(data.map(d => ({
        ...d,
        createdAt: new Date(d.createdAt),
        lastShuffledAt: d.lastShuffledAt ? new Date(d.lastShuffledAt) : undefined,
      })));
    } catch (err) {
      console.error('[useCardDeck] Failed to fetch decks:', err);
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId]);

  /**
   * Fetch player's hand for a specific deck
   */
  const refreshHand = useCallback(async (deckId: string) => {
    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/decks/${deckId}/hand/${participantId}`,
        {
          method: 'GET',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch hand: ${response.status}`);
      }

      const data: PlayerHand = await response.json();
      setHands(prev => ({ ...prev, [deckId]: data }));
    } catch (err) {
      console.error('[useCardDeck] Failed to fetch hand:', err);
    }
  }, [baseUrl, sessionId, participantId]);

  /**
   * Fetch discard pile for a specific deck
   */
  const refreshDiscardPile = useCallback(async (deckId: string) => {
    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/decks/${deckId}/discard?limit=20`,
        {
          method: 'GET',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch discard pile: ${response.status}`);
      }

      const data: DiscardPile = await response.json();
      setDiscardPiles(prev => ({ ...prev, [deckId]: data }));
    } catch (err) {
      console.error('[useCardDeck] Failed to fetch discard pile:', err);
    }
  }, [baseUrl, sessionId]);

  /**
   * Create a new deck
   */
  const createDeck = useCallback(
    async (
      name: string,
      deckType: 'standard' | 'custom',
      createOptions?: {
        includeJokers?: boolean;
        customCards?: { name: string; imageUrl?: string; suit?: string; value?: string }[];
      }
    ): Promise<CreateDeckResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/decks`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              name,
              deckType,
              includeJokers: createOptions?.includeJokers ?? false,
              customCards: createOptions?.customCards,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Create deck failed: ${response.status} - ${errorBody}`);
        }

        const data: CreateDeckResponse = await response.json();

        // Refresh decks list
        await refreshDecks();

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, refreshDecks]
  );

  /**
   * Draw cards from a deck
   */
  const drawCards = useCallback(
    async (deckId: string, count: number): Promise<DrawCardsResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/decks/${deckId}/draw`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              deckId,
              participantId,
              count,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Draw cards failed: ${response.status} - ${errorBody}`);
        }

        const data: DrawCardsResponse = await response.json();

        // Update local hand state
        setHands(prev => {
          const currentHand = prev[deckId]?.cards || [];
          return {
            ...prev,
            [deckId]: {
              deckId,
              participantId,
              cards: [...currentHand, ...data.cards],
            },
          };
        });

        // Update deck info
        setDecks(prev =>
          prev.map(d =>
            d.id === deckId
              ? { ...d, cardsInDrawPile: data.remainingCards }
              : d
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
   * Discard cards from hand
   */
  const discardCards = useCallback(
    async (deckId: string, cardIds: string[]): Promise<DiscardCardsResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/decks/${deckId}/discard`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              deckId,
              participantId,
              cardIds,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Discard cards failed: ${response.status} - ${errorBody}`);
        }

        const data: DiscardCardsResponse = await response.json();

        // Update local hand state (remove discarded cards)
        setHands(prev => {
          const currentHand = prev[deckId]?.cards || [];
          const cardIdSet = new Set(cardIds);
          return {
            ...prev,
            [deckId]: {
              deckId,
              participantId,
              cards: currentHand.filter(c => !cardIdSet.has(c.id)),
            },
          };
        });

        // Update discard pile
        setDiscardPiles(prev => {
          const currentPile = prev[deckId]?.cards || [];
          return {
            ...prev,
            [deckId]: {
              deckId,
              cards: [...data.discardedCards, ...currentPile],
              totalCount: (prev[deckId]?.totalCount || 0) + data.discardedCards.length,
            },
          };
        });

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
   * Shuffle a deck
   */
  const shuffleDeck = useCallback(
    async (deckId: string, includeDiscard = false): Promise<ShuffleDeckResponse> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/decks/${deckId}/shuffle`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              deckId,
              includeDiscard,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Shuffle deck failed: ${response.status} - ${errorBody}`);
        }

        const data: ShuffleDeckResponse = await response.json();

        // Update deck info
        setDecks(prev =>
          prev.map(d =>
            d.id === deckId
              ? {
                  ...d,
                  cardsInDrawPile: data.cardsInDrawPile,
                  cardsInDiscardPile: includeDiscard ? 0 : d.cardsInDiscardPile,
                  lastShuffledAt: new Date(data.shuffledAt),
                }
              : d
          )
        );

        // Clear discard pile if included in shuffle
        if (includeDiscard) {
          setDiscardPiles(prev => ({
            ...prev,
            [deckId]: { deckId, cards: [], totalCount: 0 },
          }));
        }

        return data;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId]
  );

  /**
   * Handle SSE events
   */
  const addDeckEventFromSSE = useCallback(
    (eventType: string, data: unknown) => {
      switch (eventType) {
        case 'CardsDrawnEvent': {
          const event = data as CardsDrawnEventData;
          // Update deck card count
          setDecks(prev =>
            prev.map(d =>
              d.id === event.deckId
                ? { ...d, cardsInDrawPile: event.remainingCards }
                : d
            )
          );
          onCardsDrawn?.(event);
          break;
        }
        case 'CardsDiscardedEvent': {
          const event = data as CardsDiscardedEventData;
          // Update discard pile
          setDiscardPiles(prev => {
            const currentPile = prev[event.deckId]?.cards || [];
            return {
              ...prev,
              [event.deckId]: {
                deckId: event.deckId,
                cards: [...event.cards, ...currentPile],
                totalCount: (prev[event.deckId]?.totalCount || 0) + event.cards.length,
              },
            };
          });
          onCardsDiscarded?.(event);
          break;
        }
        case 'DeckShuffledEvent': {
          const event = data as DeckShuffledEventData;
          // Update deck info
          setDecks(prev =>
            prev.map(d =>
              d.id === event.deckId
                ? {
                    ...d,
                    cardsInDrawPile: event.cardsInDrawPile,
                    lastShuffledAt: new Date(event.timestamp),
                  }
                : d
            )
          );
          onDeckShuffled?.(event);
          break;
        }
      }
    },
    [onCardsDrawn, onCardsDiscarded, onDeckShuffled]
  );

  // Fetch decks on mount
  useEffect(() => {
    void refreshDecks();
  }, [refreshDecks]);

  return {
    decks,
    hands,
    discardPiles,
    isLoading,
    error,
    createDeck,
    drawCards,
    discardCards,
    shuffleDeck,
    refreshDecks,
    refreshHand,
    refreshDiscardPile,
    addDeckEventFromSSE,
  };
}
