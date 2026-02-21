/**
 * useEntityActions - Entity-specific action factory
 *
 * Generates quick actions and menu actions for each MeepleCard entity type.
 * Handles navigation, permissions, and entity-specific operations.
 *
 * @module hooks/use-entity-actions
 * @see Issue #4031 - Entity-Specific Quick Actions
 * @see Issue #4259 - Collection Quick Actions for MeepleCard
 */

import { useMemo } from 'react';

import {
  BarChart3,
  Bookmark,
  Bot,
  CheckCircle,
  Download,
  FileDown,
  Heart,
  HeartHandshake,
  Mail,
  MessageSquare,
  Pin,
  Play,
  PlayCircle,
  Plus,
  Share2,
  Star,
  Trash2,
  UserPlus,
  Wrench,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import type { QuickAction } from '@/components/ui/data-display/meeple-card-quick-actions';
import type { EntityType } from '@/lib/api/schemas/collections.schemas';

import {
  useAddToCollection,
  useCollectionStatus,
  useRemoveFromCollection,
} from './queries/useCollections';
import { useCollectionActions, type AssociatedData } from './use-collection-actions';

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
  /** User role (for role-gated actions) */
  userRole?: 'user' | 'editor' | 'admin' | 'superadmin';
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
// Hook
// ============================================================================

export function useEntityActions({
  entity,
  id,
  entityName: _entityName,
  userId,
  userRole = 'user',
  data,
  onShowRemovalWarning,
  onCreateAgent,
  onAddToCollection,
}: UseEntityActionsProps): EntityActions {
  const router = useRouter();

  // Reserved for future use in moreActions
  const _isAdmin = userRole === 'admin' || userRole === 'superadmin';
  const _isOwner = userId && data?.ownerId && userId === data.ownerId;

  // Issue #4259: Collection actions for game entity (Phase 1)
  // Call hook unconditionally (hooks rules), but only use for entity='game'
  // Pass userId so the hook skips the API call for unauthenticated users
  const gameCollection = useCollectionActions(
    entity === 'game' ? id : '',
    onShowRemovalWarning,
    userId
  );

  // Issue #4263: Generic collection actions for other entities (Phase 2)
  // Map MeepleEntityType to backend EntityType
  const genericEntityType: EntityType | null =
    entity === 'game'
      ? null // Use Phase 1 hooks for games
      : (entity as EntityType);

  // Generic collection status (disabled for games)
  const { data: genericStatus } = useCollectionStatus(
    genericEntityType || 'player', // Fallback to prevent type error
    id,
    { enabled: genericEntityType !== null }
  );

  // Generic collection mutations (disabled for games)
  const addToGenericCollection = useAddToCollection(
    genericEntityType || 'player',
    id
  );
  const removeFromGenericCollection = useRemoveFromCollection(
    genericEntityType || 'player',
    id,
    onShowRemovalWarning
  );

  return useMemo(() => {
    switch (entity) {
      case 'game': {
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

        const hasRag = data?.hasPdfDocuments === true;
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
                  ? router.push(`/sessions/new?gameId=${id}`)
                  : router.push('/login?reason=session'),
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                navigator.clipboard?.writeText(`${window.location.origin}/games/${id}`);
              },
            },
          ],
          // More menu actions can be added later
        };
      }

      case 'session': {
        // Issue #4263: Collection action for session (Save/Remove)
        const saveAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: Bookmark,
                label: 'Rimuovi dai Salvati',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Bookmark,
                label: 'Salva',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            saveAction, // Issue #4263: First action
            {
              icon: PlayCircle,
              label: 'Riprendi',
              onClick: () => router.push(`/sessions/${id}`),
            },
            {
              icon: Wrench,
              label: 'Usa Toolkit',
              onClick: () => router.push(`/sessions/${id}?tab=toolkit`),
            },
            {
              icon: Share2,
              label: 'Condividi codice',
              onClick: () => {
                // TODO: Copy session code to clipboard
                const sessionCode = (data?.sessionCode as string | undefined) || id;
                navigator.clipboard?.writeText(sessionCode);
              },
            },
          ],
        };
      }

      case 'agent': {
        // Issue #4263: Collection action for agent (Favorite/Unfavorite)
        const favoriteAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: Heart,
                label: 'Rimuovi dai Preferiti',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Heart,
                label: 'Aggiungi ai Preferiti',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            favoriteAction, // Issue #4263: First action
            {
              icon: MessageSquare,
              label: 'Chat',
              onClick: () => router.push(`/chat?agentId=${id}`),
            },
            {
              icon: BarChart3,
              label: 'Statistiche',
              onClick: () => router.push(`/agents/${id}?tab=stats`),
            },
          ],
        };
      }

      case 'document': {
        // Issue #4263: Collection action for document (Save/Remove)
        const saveAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: Bookmark,
                label: 'Rimuovi dai Salvati',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Bookmark,
                label: 'Salva',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            saveAction, // Issue #4263: First action
            {
              icon: Download,
              label: 'Download',
              onClick: () => {
                // TODO: Trigger download
                window.open(`/api/v1/documents/${id}/download`, '_blank');
              },
            },
            {
              icon: MessageSquare,
              label: 'Chat sui contenuti',
              onClick: () => router.push(`/chat?documentId=${id}`),
            },
          ],
        };
      }

      case 'chatSession': {
        // Issue #4263: Collection action for chatSession (Pin/Unpin)
        const pinAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: Pin,
                label: 'Rimuovi Pin',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Pin,
                label: 'Fissa',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            pinAction, // Issue #4263: First action
            {
              icon: MessageSquare,
              label: 'Continua Chat',
              onClick: () => router.push(`/chat/${id}`),
            },
            {
              icon: FileDown,
              label: 'Esporta',
              onClick: () => {
                // TODO: Export chat history
                router.push(`/chat/${id}/export`);
              },
            },
          ],
        };
      }

      case 'player': {
        // Issue #4263: Collection action for player (Follow/Unfollow)
        const followAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: HeartHandshake,
                label: 'Smetti di Seguire',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: HeartHandshake,
                label: 'Segui',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            followAction, // Issue #4263: First action
            {
              icon: Mail,
              label: 'Messaggia',
              onClick: () => router.push(`/messages/new?recipientId=${id}`),
            },
            {
              icon: UserPlus,
              label: 'Invita a Sessione',
              onClick: () => {
                // TODO: Open session invite modal
                router.push(`/sessions/new?invitePlayer=${id}`);
              },
            },
          ],
        };
      }

      case 'event': {
        // Issue #4263: Collection action for event (Interested/Remove)
        const interestedAction: QuickAction =
          genericStatus?.inCollection ?? false
            ? {
                icon: Star,
                label: 'Rimuovi Interesse',
                onClick: () => removeFromGenericCollection.remove(undefined),
              }
            : {
                icon: Star,
                label: 'Interessato',
                onClick: () => addToGenericCollection.mutate(undefined),
              };

        return {
          quickActions: [
            interestedAction, // Issue #4263: First action
            {
              icon: CheckCircle,
              label: 'Partecipa',
              onClick: () => {
                // TODO: RSVP to event
                router.push(`/events/${id}/rsvp`);
              },
            },
            {
              icon: Share2,
              label: 'Condividi',
              onClick: () => {
                navigator.clipboard?.writeText(`${window.location.origin}/events/${id}`);
              },
            },
          ],
        };
      }

      case 'custom':
      default:
        return {
          quickActions: [],
        };
    }
  }, [
    entity,
    id,
    router,
    data,
    gameCollection,
    genericStatus,
    addToGenericCollection,
    removeFromGenericCollection,
    onCreateAgent,
    onAddToCollection,
  ]);
}
