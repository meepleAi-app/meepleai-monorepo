/**
 * useEntityActions - Quick-action factory for the MeepleCard 'game' entity.
 *
 * Generates the hover-reveal quick actions for game catalog cards: collection
 * membership, agent creation, chat, start-session, and share-with-toast.
 *
 * @module hooks/use-entity-actions
 * @see Issue #4031 - Entity-Specific Quick Actions
 * @see Issue #4259 - Collection Quick Actions for MeepleCard
 * @see Issue #2776 - the non-'game' switch branches (session/agent/kb/chat/player/event)
 *   and the unreferenced `useContextualActions` wrapper were removed as dead code — the
 *   only consumer (`MeepleGameCard`) always passes entity='game'. Non-'game' entities now
 *   yield an empty action set.
 */

import { useMemo } from 'react';

import { Bot, MessageSquare, Play, Plus, Share2, Trash2 } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import type { QuickAction } from '@/types/quick-action';

import { useCollectionActions, type AssociatedData } from './useCollectionActions';

// ============================================================================
// Types
// ============================================================================

export interface UseEntityActionsProps {
  /** Entity type */
  entity: MeepleEntityType;
  /** Entity ID */
  id: string;
  /** Entity name (for warning modal) */
  entityName?: string;
  /** Current user ID (for ownership checks) */
  userId?: string;
  /** Entity-specific data (optional) */
  data?: {
    ownerId?: string;
    isShared?: boolean;
    status?: string;
    [key: string]: unknown;
  };
  /** Callback to show removal warning modal (Issue #4259) */
  onShowRemovalWarning?: (data: AssociatedData, onConfirm: () => void) => void;
  /** Callback to open agent creation wizard (Issue #4777) */
  onCreateAgent?: () => void;
  /** Callback to open collection wizard instead of direct add (Issue #4822) */
  onAddToCollection?: () => void;
}

export interface EntityActions {
  /** Quick actions (hover-reveal buttons) */
  quickActions: QuickAction[];
  /** More menu actions (secondary dropdown) */
  moreActions?: Array<{
    label: string;
    onClick: () => void;
    icon?: React.ComponentType;
    adminOnly?: boolean;
    destructive?: boolean;
    separator?: boolean;
  }>;
}

// ============================================================================
// Handler helpers
// ============================================================================

/**
 * Copy text to the clipboard with success/error toast feedback.
 * Guards against restricted contexts where the Clipboard API is unavailable.
 */
async function copyWithToast(text: string, successMessage: string): Promise<void> {
  try {
    if (typeof navigator === 'undefined' || !navigator.clipboard) {
      throw new Error('Clipboard API unavailable');
    }
    await navigator.clipboard.writeText(text);
    toast.success(successMessage);
  } catch {
    toast.error('Copia non riuscita');
  }
}

// ============================================================================
// Hook
// ============================================================================

export function useEntityActions({
  entity,
  id,
  entityName,
  userId,
  data,
  onShowRemovalWarning,
  onCreateAgent,
  onAddToCollection,
}: UseEntityActionsProps): EntityActions {
  const router = useRouter();

  // Issue #4259: Collection actions for the game entity.
  // Call hook unconditionally (hooks rules); pass an empty id for non-game entities
  // so the hook skips the API call.
  const gameCollection = useCollectionActions(
    entity === 'game' ? id : '',
    onShowRemovalWarning,
    userId
  );

  return useMemo(() => {
    // Only the 'game' entity is rendered today (via MeepleGameCard). The former
    // multi-entity branches were removed as dead code (#2776).
    if (entity !== 'game') {
      return { quickActions: [] };
    }

    const isAuthenticated = !!userId;

    // Build collection action — redirect to login if unauthenticated
    const collectionAction: QuickAction = !isAuthenticated
      ? {
          icon: Plus,
          label: 'Aggiungi a Collezione',
          onClick: () => router.push('/login?reason=collection'),
        }
      : gameCollection.isInCollection
        ? {
            icon: Trash2,
            label: 'Rimuovi da Collezione',
            onClick: () => gameCollection.remove(),
          }
        : {
            icon: Plus,
            label: 'Aggiungi a Collezione',
            onClick: () => (onAddToCollection ? onAddToCollection() : gameCollection.add()),
          };

    const hasRag = data?.hasKb === true;
    const hasAgent = data?.hasAgent === true;

    return {
      quickActions: [
        collectionAction, // Issue #4259: First action
        {
          icon: Bot,
          label: 'Crea Agente',
          onClick: () => onCreateAgent?.(),
          hidden: hasAgent || !onCreateAgent,
        },
        {
          icon: MessageSquare,
          label: 'Chat con Agent',
          onClick: () => router.push(`/chat/new?game=${id}`),
          hidden: !hasRag,
        },
        {
          icon: Play,
          label: 'Avvia Sessione',
          onClick: () =>
            isAuthenticated
              ? router.push(
                  `/sessions/new?gameId=${encodeURIComponent(id)}${entityName ? `&gameName=${encodeURIComponent(entityName)}` : ''}`
                )
              : router.push('/login?reason=session'),
        },
        {
          icon: Share2,
          label: 'Condividi',
          onClick: () => {
            void copyWithToast(
              `${window.location.origin}/games/${id}`,
              'Link copiato negli appunti'
            );
          },
        },
      ],
    };
  }, [
    entity,
    id,
    router,
    data,
    userId,
    gameCollection,
    onCreateAgent,
    onAddToCollection,
    entityName,
  ]);
}
